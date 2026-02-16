using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Web.KaxServer.Models;
using Web.KaxServer.Services.Queries;
using Web.KaxServer.Services.Repositorys;

namespace Web.KaxServer.Services
{
    public class CdkService : ICdkService
    {
        private readonly StoreService _storeService;
        private readonly string _dataDirectory;

        public CdkService(StoreService storeService, IWebHostEnvironment env)
        {
            _storeService = storeService;
            _dataDirectory = Path.Combine(env.ContentRootPath, "Data", "Cdk");
            Directory.CreateDirectory(_dataDirectory); // 确保目录存在
        }

        public List<Cdk> CreateCdks(int quantity, CdkType type, int? assetId, decimal? coinAmount, int? durationValue = null, DurationUnit? durationUnit = null)
        {
            var allCdks = CdkRepository.GetAllCdks();
            var newCdks = new List<Cdk>();
            var batchId = Guid.NewGuid().ToString("N");

            for (int i = 0; i < quantity; i++)
            {
                string uniqueCode;
                do
                {
                    uniqueCode = GenerateFormattedCode(type, assetId, coinAmount, durationValue, durationUnit);
                } while (allCdks.Any(c => c.Code == uniqueCode));

                var cdk = new Cdk
                {
                    Code = uniqueCode,
                    Type = type,
                    IsUsed = false,
                    CreationDate = DateTime.UtcNow,
                    BatchId = batchId
                };

                if (type == CdkType.Asset)
                {
                    cdk.AssetId = assetId;
                    cdk.DurationValue = durationValue;
                    cdk.DurationUnit = durationUnit;
                }
                else if (type == CdkType.Coins)
                {
                    cdk.CoinAmount = coinAmount;
                }
                
                newCdks.Add(cdk);
                CdkRepository.SaveCdk(cdk);
            }

            if (newCdks.Any())
            {
                SaveCdkBatchFile(newCdks, type, assetId, coinAmount, durationValue, durationUnit);
            }
            
            return newCdks;
        }

        public CdkQueryResult QueryCdks(CdkQueryParameters parameters)
        {
            var allCdks = CdkRepository.GetAllCdks();

            // 1. 筛选
            var filteredCdks = allCdks;
            if (!string.IsNullOrWhiteSpace(parameters.SearchTerm))
            {
                var storeItemsLookup = _storeService.GetAllItems().ToDictionary(i => i.Id);
                filteredCdks = allCdks.Where(c =>
                {
                    if (c.Code.Contains(parameters.SearchTerm, StringComparison.OrdinalIgnoreCase)) return true;
                    if (c.Type == Models.CdkType.Asset && c.AssetId.HasValue)
                    {
                        if (storeItemsLookup.TryGetValue(c.AssetId.Value, out var item) && item.Title.Contains(parameters.SearchTerm, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                    else if (c.Type == Models.CdkType.Coins && c.CoinAmount.HasValue)
                    {
                        if (c.CoinAmount.Value.ToString().Contains(parameters.SearchTerm)) return true;
                    }
                    return false;
                }).ToList();
            }

            // 2. 排序
            var sortedCdks = (parameters.SortBy switch
            {
                "type" => filteredCdks.OrderBy(c => c.Type).ThenByDescending(c => c.CreationDate),
                "status" => filteredCdks.OrderBy(c => c.IsUsed).ThenByDescending(c => c.CreationDate),
                _ => filteredCdks.OrderByDescending(c => c.CreationDate)
            }).AsQueryable();

            // 3. 分页
            var totalCount = sortedCdks.Count();
            var pagedCdks = sortedCdks.Skip((parameters.Page - 1) * parameters.PageSize).Take(parameters.PageSize).ToList();

            return new CdkQueryResult
            {
                Cdks = pagedCdks,
                TotalCount = totalCount
            };
        }

        public Cdk? GetCdkByCode(string code)
        {
            // 先尝试从持久层查找
            var stored = CdkRepository.GetCdk(code);
            if (stored != null) return stored;

            // 仅从持久化层查找；对于带后缀（token）的 CDK，解析/映射已迁移到 KaxSocket 的 /api/cdk/inspect 实现。
            // 保持此方法只返回持久化的 Cdk 对象。
            return null;
        }

        public Cdk? VerifyCdk(string code)
        {
            var cdk = GetCdkByCode(code);
            if (cdk == null || cdk.IsUsed)
            {
                return null;
            }
            return cdk;
        }

        public Cdk? ActivateCdk(string code, UserSession userSession)
        {
            var cdk = GetCdkByCode(code);
            if (cdk == null || cdk.IsUsed)
            {
                return null;
            }

            // 判断是否为已持久化的 CDK（token 型 CDK 通常不会在仓库中存在）
            var persisted = CdkRepository.GetCdk(code) != null;

            switch (cdk.Type)
            {
                case CdkType.Asset:
                    if (cdk.AssetId.HasValue)
                    {
                        var storeItem = _storeService.GetItemById(cdk.AssetId.Value);
                        if (storeItem != null)
                        {
                            if (cdk.DurationValue.HasValue && cdk.DurationUnit.HasValue)
                            {
                                userSession.AddOrUpdateAsset(storeItem.Id, cdk.DurationValue.Value, cdk.DurationUnit.Value);
                            }
                            else
                            {
                                userSession.AddOrUpdateAsset(storeItem.Id, storeItem.DurationDays, DurationUnit.Day);
                            }
                        }
                    }
                    break;
                case CdkType.Coins:
                    if (cdk.CoinAmount.HasValue)
                    {
                        userSession.Coins += cdk.CoinAmount.Value;
                    }
                    break;
            }

            cdk.IsUsed = true;
            cdk.UsedByUsername = userSession.Username;
            cdk.UsedDate = DateTime.UtcNow;

            // 仅当 CDK 在仓库中存在时才保存（避免把短期 JWT token 写入持久化存储）
            if (persisted)
            {
                CdkRepository.SaveCdk(cdk);
            }
            else
            {
                // 非持久化 CDK：记录日志（可选），返回临时对象
                // logger 可在未来扩展以跟踪 token 激活历史
            }

            return cdk;
        }

        public void DeleteCdksByBatchId(string batchId)
        {
            CdkRepository.DeleteCdk(batchId);
        }

        public string? GetBatchIdFromCdkCode(string code)
        {
            var cdk = GetCdkByCode(code);
            return cdk?.BatchId;
        }

        private string GenerateFormattedCode(CdkType type, int? assetId, decimal? coinAmount, int? durationValue, DurationUnit? durationUnit)
        {
            string randomPart = GenerateRandomPart(16);
            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            string timestampHash = ComputeShortHash(timestamp);

            string typeDetails;
            if (type == CdkType.Asset)
            {
                typeDetails = $"Asset:{assetId?.ToString() ?? "N/A"}:{durationValue?.ToString() ?? "N/A"}:{durationUnit?.ToString() ?? "N/A"}";
            }
            else // CdkType.Coins
            {
                typeDetails = $"Coins:{coinAmount?.ToString("F0") ?? "0"}";
            }
            string typeHash = ComputeShortHash(typeDetails);

            return $"KAX-{randomPart}-{timestampHash}-{typeHash}";
        }

        private string GenerateRandomPart(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var result = new StringBuilder(length);
            using (var rng = RandomNumberGenerator.Create())
            {
                for (int i = 0; i < length; i++)
                {
                    var buffer = new byte[sizeof(uint)];
                    rng.GetBytes(buffer);
                    uint randomInt = BitConverter.ToUInt32(buffer, 0);
                    result.Append(chars[(int)(randomInt % (uint)chars.Length)]);
                }
            }
            return result.ToString();
        }
        
        private string ComputeShortHash(string input)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(bytes, 0, 4).Replace("-", "");
            }
        }
        
        private void SaveCdkBatchFile(List<Cdk> cdks, CdkType type, int? assetId, decimal? coinAmount, int? durationValue, DurationUnit? durationUnit)
        {
            if (!cdks.Any()) return;

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var typeStr = type.ToString();
            var count = cdks.Count;
            
            string details;
            if (type == CdkType.Asset)
            {
                string assetName = "UnknownAsset";
                if (assetId.HasValue)
                {
                    var storeItem = _storeService.GetItemById(assetId.Value);
                    if (storeItem != null)
                    {
                        // 清理资产名称，使其适用于文件名
                        assetName = string.Join("_", storeItem.Title.Split(Path.GetInvalidFileNameChars()));
                    }
                }

                string duration = "DefaultDuration";
                if (durationValue.HasValue && durationUnit.HasValue)
                {
                    duration = $"{durationValue.Value}{durationUnit.Value}";
                }
                details = $"{assetName}_{duration}";
            }
            else // CdkType.Coins
            {
                details = $"{coinAmount?.ToString("F0") ?? "0"}Coins";
            }
            
            var batchFileName = $"{timestamp}_{typeStr}_{details}_{count}Count.txt";
            var batchFilePath = Path.Combine(_dataDirectory, "Batches", batchFileName);

            CdkRepository.CreateBatchFile(cdks, batchFilePath);
        }
    }
} 
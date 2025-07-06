using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Drx.Sdk.Network.DataBase;
using Microsoft.AspNetCore.Hosting;
using Web.KaxServer.Models;
using Web.KaxServer.Services.Queries;

namespace Web.KaxServer.Services
{
    public class CdkService : ICdkService
    {
        private readonly StoreService _storeService;
        private readonly string _filePath;
        private readonly XmlDatabase _database;
        private static readonly object FileLock = new object();

        public CdkService(StoreService storeService, IWebHostEnvironment env)
        {
            _storeService = storeService;
            _database = new XmlDatabase();
            var dataDirectory = Path.Combine(env.ContentRootPath, "Data");
            Directory.CreateDirectory(dataDirectory); // Ensure the directory exists
            _filePath = Path.Combine(dataDirectory, "cdks.xml");
        }

        public List<Cdk> CreateCdks(int quantity, CdkType type, int? assetId, decimal? coinAmount, int? durationValue = null, DurationUnit? durationUnit = null)
        {
            var allCdks = LoadCdksFromFile();
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
            }

            if (newCdks.Any())
            {
                allCdks.AddRange(newCdks);
                SaveCdksToFile(allCdks);
                SaveCdkBatchFile(newCdks, type, assetId, coinAmount, durationValue, durationUnit);
            }
            return newCdks;
        }

        public CdkQueryResult QueryCdks(CdkQueryParameters parameters)
        {
            var allCdks = LoadCdksFromFile();

            // 1. Filtering
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

            // 2. Sorting
            var sortedCdks = (parameters.SortBy switch
            {
                "type" => filteredCdks.OrderBy(c => c.Type).ThenByDescending(c => c.CreationDate),
                "status" => filteredCdks.OrderBy(c => c.IsUsed).ThenByDescending(c => c.CreationDate),
                _ => filteredCdks.OrderByDescending(c => c.CreationDate)
            }).AsQueryable();

            // 3. Pagination
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
            return LoadCdksFromFile().FirstOrDefault(c => c.Code == code);
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
            var allCdks = LoadCdksFromFile();
            var cdk = allCdks.FirstOrDefault(c => c.Code == code);
            
            if (cdk == null || cdk.IsUsed)
            {
                return null;
            }

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
            
            SaveCdksToFile(allCdks);

            return cdk;
        }

        public void DeleteCdksByBatchId(string batchId)
        {
            var allCdks = LoadCdksFromFile();
            var updatedCdks = allCdks.Where(c => c.BatchId != batchId).ToList();

            if (allCdks.Count == updatedCdks.Count)
            {
                return;
            }

            SaveCdksToFile(updatedCdks);
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

        private List<Cdk> LoadCdksFromFile()
        {
            lock (FileLock)
            {
                try
                {
                    if (!File.Exists(_filePath)) return new List<Cdk>();

                    var rootNode = _database.CreateRoot(_filePath);
                    var cdkList = rootNode.DeserializeList<Cdk>("cdks");

                    return cdkList ?? new List<Cdk>();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading CDKs from file: {_filePath}. Error: {ex.Message}");
                    return new List<Cdk>();
                }
            }
        }

        private void SaveCdksToFile(List<Cdk> cdkList)
        {
            lock (FileLock)
            {
                try
                {
                    var rootNode = _database.CreateRoot(_filePath);
                    rootNode.SerializeList("cdks", cdkList);
                    _database.SaveChanges();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving CDKs to file: {_filePath}. Error: {ex.Message}");
                }
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
                        // Sanitize asset name for use in a filename
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
            var batchFilePath = Path.Combine(Path.GetDirectoryName(_filePath), batchFileName);

            try
            {
                var codes = cdks.Select(c => c.Code);
                File.WriteAllLines(batchFilePath, codes);
            }
            catch (Exception ex)
            {
                // Log the error, but don't let it crash the main operation
                Console.WriteLine($"Error saving CDK batch file to: {batchFilePath}. Error: {ex.Message}");
            }
        }
    }
} 
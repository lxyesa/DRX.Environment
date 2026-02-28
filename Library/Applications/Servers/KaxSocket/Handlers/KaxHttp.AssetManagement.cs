using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Drx.Sdk.Network.Http;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.Results;
using Drx.Sdk.Network.Http.Configs;
using Drx.Sdk.Shared;
using KaxSocket;
using KaxSocket.Model;
using static KaxSocket.Model.AssetModel;

namespace KaxSocket.Handlers;

/// <summary>
/// 资源管理模块 - 处理资源的创建、更新、查询、删除等功能
/// </summary>
public partial class KaxHttp
{
    #region 资源管理 (Asset Administration)

    /// <summary>
    /// 确保 asset.Specs 已初始化（一对一子表），返回可直接读写的 Specs 实例
    /// </summary>
    private static AssetSpecs EnsureSpecs(Model.AssetModel asset)
    {
        if (asset.Specs == null)
            asset.Specs = new AssetSpecs { ParentId = asset.Id };
        return asset.Specs;
    }

    /// <summary>
    /// 从JSON请求体中提取价格信息并创建/更新资产的价格方案
    /// 支持向后兼容的参数：price, originalPrice, discountRate, salePrice
    /// </summary>
    private static void ApplyPriceInfoToAsset(Model.AssetModel asset, JsonNode body)
    {
        if (body == null) return;

        // 优先处理 prices 数组（新的价格计划管理方式）
        var pricesNode = body["prices"] as JsonArray;
        if (pricesNode != null && pricesNode.Count > 0)
        {
            // 初始化Prices列表
            if (asset.Prices == null)
                asset.Prices = new Drx.Sdk.Network.DataBase.TableList<AssetPrice>();

            // 清空现有价格方案，用新的替换
            asset.Prices.Clear();

            foreach (var priceNode in pricesNode)
            {
                if (priceNode == null) continue;

                var priceObj = priceNode as JsonObject;
                if (priceObj == null) continue;

                // 解析价格数据
                int price = 0;
                int originalPrice = 0;
                double discountRate = 0;
                string unit = "month";
                int duration = 1;
                string id = string.Empty;
                int stock = -1;

                if (priceObj["price"] != null && int.TryParse(priceObj["price"]?.ToString(), out var priceVal) && priceVal >= 0)
                    price = priceVal;

                if (priceObj["originalPrice"] != null && int.TryParse(priceObj["originalPrice"]?.ToString(), out var origPriceVal) && origPriceVal >= 0)
                    originalPrice = origPriceVal;

                if (priceObj["discountRate"] != null && double.TryParse(priceObj["discountRate"]?.ToString(), out var discountVal))
                    discountRate = Math.Max(0.0, Math.Min(1.0, discountVal));

                if (priceObj["unit"] != null)
                    unit = priceObj["unit"]?.ToString() ?? "month";

                if (priceObj["duration"] != null && int.TryParse(priceObj["duration"]?.ToString(), out var durVal) && durVal > 0)
                    duration = durVal;

                if (priceObj["id"] != null)
                    id = priceObj["id"]?.ToString() ?? string.Empty;

                if (priceObj["stock"] != null && int.TryParse(priceObj["stock"]?.ToString(), out var stockVal))
                    stock = stockVal;

                // 创建价格方案
                var assetPrice = new AssetPrice
                {
                    ParentId = asset.Id,
                    Price = price,
                    OriginalPrice = originalPrice > 0 ? originalPrice : price,
                    DiscountRate = discountRate,
                    Unit = unit,
                    Duration = duration,
                    Stock = stock
                };

                // 如果ID不是临时ID（new_开头），则保留原ID
                if (!string.IsNullOrEmpty(id) && !id.StartsWith("new_"))
                {
                    assetPrice.Id = id;
                }

                asset.Prices.Add(assetPrice);
            }

            return; // 已处理prices数组，不再处理单个价格字段
        }

        // 回退：处理单个价格字段（向后兼容）
        int? price_single = null;
        int? originalPrice_single = null;
        double? discountRate_single = null;

        if (body["price"] != null && int.TryParse(body["price"]?.ToString(), out var p) && p >= 0)
            price_single = p;

        if (body["originalPrice"] != null && int.TryParse(body["originalPrice"]?.ToString(), out var op) && op >= 0)
            originalPrice_single = op;

        if (body["discountRate"] != null && double.TryParse(body["discountRate"]?.ToString(), out var dr))
            discountRate_single = Math.Max(0.0, Math.Min(1.0, dr));

        if (price_single.HasValue || originalPrice_single.HasValue || discountRate_single.HasValue)
        {
            if (asset.Prices == null)
                asset.Prices = new Drx.Sdk.Network.DataBase.TableList<AssetPrice>();

            AssetPrice firstPrice = asset.Prices.FirstOrDefault();
            if (firstPrice == null)
            {
                firstPrice = new AssetPrice { ParentId = asset.Id };
                asset.Prices.Add(firstPrice);
            }

            if (price_single.HasValue) firstPrice.Price = price_single.Value;
            if (originalPrice_single.HasValue) firstPrice.OriginalPrice = originalPrice_single.Value;
            if (discountRate_single.HasValue) firstPrice.DiscountRate = discountRate_single.Value;

            if (firstPrice.OriginalPrice == 0 && firstPrice.Price > 0)
                firstPrice.OriginalPrice = firstPrice.Price;
        }
    }

    /// <summary>
    /// 从 JSON 请求体中提取规格信息（文件大小、评分、兼容性、许可证等）并写入 asset.Specs 子表
    /// </summary>
    private static void ApplySpecsInfoToAsset(Model.AssetModel asset, JsonNode body, bool isCreate = false)
    {
        if (body == null) return;
        var specs = EnsureSpecs(asset);

        if (body["fileSize"] != null && long.TryParse(body["fileSize"]?.ToString(), out var fs) && fs >= 0)
            specs.FileSize = fs;
        if (body["rating"] != null && double.TryParse(body["rating"]?.ToString(), out var rating))
            specs.Rating = Math.Max(0.0, Math.Min(5.0, rating));
        if (body["reviewCount"] != null && int.TryParse(body["reviewCount"]?.ToString(), out var rc) && rc >= 0)
            specs.ReviewCount = rc;
        if (body["compatibility"] != null)
            specs.Compatibility = body["compatibility"]?.ToString() ?? string.Empty;
        if (body["downloads"] != null && int.TryParse(body["downloads"]?.ToString(), out var dl) && dl >= 0)
            specs.Downloads = dl;
        if (body["uploadDate"] != null && long.TryParse(body["uploadDate"]?.ToString(), out var ud) && ud > 0)
            specs.UploadDate = ud;
        if (body["license"] != null)
            specs.License = body["license"]?.ToString() ?? string.Empty;
        if (body["downloadUrl"] != null)
            specs.DownloadUrl = body["downloadUrl"]?.ToString() ?? string.Empty;

        specs.LastUpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    [HttpHandle("/api/asset/admin/create", "POST", RateLimitMaxRequests = 10, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_CreateAsset(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        if (principal == null) return new JsonResult(new { message = "无效的登录令牌" }, 401);

        var userName = principal.Identity?.Name;
        if (await KaxGlobal.IsUserBanned(userName!))
            return new JsonResult(new { message = "您的账号已被封禁" }, 403);
        if (!await IsAssetAdminUser(userName))
            return new JsonResult(new { message = "权限不足" }, 403);

        if (string.IsNullOrEmpty(request.Body))
            return new JsonResult(new { message = "请求体不能为空" }, 400);

        try
        {
            var body = JsonNode.Parse(request.Body);
            if (body == null) return new JsonResult(new { message = "无法解析请求体" }, 400);

            var name = body["name"]?.ToString();
            var version = body["version"]?.ToString();
            var author = body["author"]?.ToString();
            var description = body["description"]?.ToString() ?? "";

            if (string.IsNullOrEmpty(name) || name.Length > 100)
                return new JsonResult(new { message = "资源名称无效（1-100字符）" }, 400);
            if (string.IsNullOrEmpty(version) || version.Length > 50)
                return new JsonResult(new { message = "版本字段无效（1-50字符）" }, 400);
            if (string.IsNullOrEmpty(author) || author.Length > 100)
                return new JsonResult(new { message = "作者字段无效（1-100字符）" }, 400);
            if (description.Length > 500)
                return new JsonResult(new { message = "描述过长（最多500字符）" }, 400);

            var asset = new Model.AssetModel
            {
                Name = name,
                Version = version,
                Author = author,
                Description = description
            };

            // 应用价格信息（支持向后兼容的参数）
            ApplyPriceInfoToAsset(asset, body);

            // 可选字段：分类
            if (body["category"] != null)
            {
                asset.Category = body["category"]?.ToString() ?? string.Empty;
            }

            // 应用规格信息（文件大小、评分、兼容性、许可证等）
            ApplySpecsInfoToAsset(asset, body, isCreate: true);

            KaxGlobal.AssetDataBase.Insert(asset);

            Logger.Info($"用户 {userName} 创建了资源: {name} (v{version})");
            return new JsonResult(new { message = "资源创建成功", id = asset.Id });
        }
        catch (Exception ex)
        {
            Logger.Error($"创建资源失败: {ex.Message}");
            return new JsonResult(new { message = "服务器错误" }, 500);
        }
    }

    [HttpHandle("/api/asset/admin/update", "POST", RateLimitMaxRequests = 10, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_UpdateAsset(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        if (principal == null) return new JsonResult(new { message = "无效的登录令牌" }, 401);

        var userName = principal.Identity?.Name;
        if (await KaxGlobal.IsUserBanned(userName!))
            return new JsonResult(new { message = "您的账号已被封禁" }, 403);
        if (!await IsAssetAdminUser(userName))
            return new JsonResult(new { message = "权限不足" }, 403);

        if (string.IsNullOrEmpty(request.Body))
            return new JsonResult(new { message = "请求体不能为空" }, 400);

        try
        {
            var body = JsonNode.Parse(request.Body);
            if (body == null) return new JsonResult(new { message = "无法解析请求体" }, 400);

            if (!int.TryParse(body["id"]?.ToString(), out var id) || id <= 0)
                return new JsonResult(new { message = "资源ID无效" }, 400);

            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(id);
            if (asset == null)
                return new JsonResult(new { message = "资源不存在" }, 404);

            var version = body["version"]?.ToString();
            var author = body["author"]?.ToString();
            var description = body["description"]?.ToString() ?? "";

            if (!string.IsNullOrEmpty(version) && version.Length > 50)
                return new JsonResult(new { message = "版本字段无效（最多50字符）" }, 400);
            if (!string.IsNullOrEmpty(author) && author.Length > 100)
                return new JsonResult(new { message = "作者字段无效（最多100字符）" }, 400);
            if (description.Length > 500)
                return new JsonResult(new { message = "描述过长（最多500字符）" }, 400);

            if (!string.IsNullOrEmpty(version)) asset.Version = version;
            if (!string.IsNullOrEmpty(author)) asset.Author = author;
            asset.Description = description;

            // 应用价格信息（支持向后兼容的参数）
            ApplyPriceInfoToAsset(asset, body);

            // 可选更新：分类
            if (body["category"] != null)
            {
                asset.Category = body["category"]?.ToString() ?? string.Empty;
            }

            // 应用规格信息
            ApplySpecsInfoToAsset(asset, body);

            await KaxGlobal.AssetDataBase.UpdateAsync(asset);

            Logger.Info($"用户 {userName} 修改了资源: {asset.Name} (id={id})");
            return new JsonResult(new { message = "资源已更新" });
        }
        catch (Exception ex)
        {
            Logger.Error($"更新资源失败: {ex}");
            return new JsonResult(new { message = "服务器错误" }, 500);
        }
    }

    [HttpHandle("/api/asset/admin/inspect", "POST", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_InspectAsset(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        if (principal == null) return new JsonResult(new { message = "无效的登录令牌" }, 401);

        var userName = principal.Identity?.Name;
        if (await KaxGlobal.IsUserBanned(userName!))
            return new JsonResult(new { message = "您的账号已被封禁" }, 403);
        if (!await IsAssetAdminUser(userName))
            return new JsonResult(new { message = "权限不足" }, 403);

        if (string.IsNullOrEmpty(request.Body))
            return new JsonResult(new { message = "请求体不能为空" }, 400);

        try
        {
            var body = JsonNode.Parse(request.Body);
            if (body == null) return new JsonResult(new { message = "无法解析请求体" }, 400);

            if (!int.TryParse(body["id"]?.ToString(), out var id) || id <= 0)
                return new JsonResult(new { message = "资源ID无效" }, 400);

            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(id);
            if (asset == null)
                return new JsonResult(new { message = "资源不存在" }, 404);

            // 将价格方案列表转换为可序列化的格式，并从第一个方案派生兼容字段
            var pricesList = asset.Prices?.ToList();
            var primaryPrice = (pricesList != null && pricesList.Any()) ? pricesList.First() : null;
            var specs = asset.Specs;

            return new JsonResult(new
            {
                data = new
                {
                    id = asset.Id,
                    name = asset.Name,
                    version = asset.Version,
                    author = asset.Author,
                    description = asset.Description,
                    price = primaryPrice != null ? primaryPrice.Price : 0,
                    originalPrice = primaryPrice != null ? primaryPrice.OriginalPrice : 0,
                    salePrice = primaryPrice != null ? (int)Math.Round(primaryPrice.Price * (1.0 - primaryPrice.DiscountRate)) : 0,
                    category = asset.Category,
                    discountRate = primaryPrice != null ? primaryPrice.DiscountRate : 0.0,
                    // 规格信息（来自 Specs 子表）
                    specs = specs != null ? new
                    {
                        fileSize = specs.FileSize,
                        rating = specs.Rating,
                        reviewCount = specs.ReviewCount,
                        compatibility = specs.Compatibility,
                        downloads = specs.Downloads,
                        uploadDate = specs.UploadDate,
                        license = specs.License,
                        downloadUrl = specs.DownloadUrl,
                        purchaseCount = specs.PurchaseCount,
                        favoriteCount = specs.FavoriteCount,
                        viewCount = specs.ViewCount,
                        lastUpdatedAt = specs.LastUpdatedAt
                    } : null,
                    // 向后兼容：平铺规格字段
                    fileSize = specs?.FileSize ?? 0,
                    rating = specs?.Rating ?? 0.0,
                    reviewCount = specs?.ReviewCount ?? 0,
                    compatibility = specs?.Compatibility ?? string.Empty,
                    downloads = specs?.Downloads ?? 0,
                    uploadDate = specs?.UploadDate ?? 0,
                    license = specs?.License ?? string.Empty,
                    downloadUrl = specs?.DownloadUrl ?? string.Empty,
                    purchaseCount = specs?.PurchaseCount ?? 0,
                    favoriteCount = specs?.FavoriteCount ?? 0,
                    viewCount = specs?.ViewCount ?? 0,
                    lastUpdatedAt = specs?.LastUpdatedAt ?? 0,
                    isDeleted = asset.IsDeleted,
                    deletedAt = asset.DeletedAt,
                    prices = pricesList
                }
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"查询资源失败: {ex.Message}");
            return new JsonResult(new { message = "服务器错误" }, 500);
        }
    }

    [HttpHandle("/api/asset/admin/delete", "POST", RateLimitMaxRequests = 10, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_DeleteAsset(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        if (principal == null) return new JsonResult(new { message = "无效的登录令牌" }, 401);

        var userName = principal.Identity?.Name;
        if (await KaxGlobal.IsUserBanned(userName!))
            return new JsonResult(new { message = "您的账号已被封禁" }, 403);
        if (!await IsAssetAdminUser(userName))
            return new JsonResult(new { message = "权限不足" }, 403);

        if (string.IsNullOrEmpty(request.Body))
            return new JsonResult(new { message = "请求体不能为空" }, 400);

        try
        {
            var body = JsonNode.Parse(request.Body);
            if (body == null) return new JsonResult(new { message = "无法解析请求体" }, 400);

            if (!int.TryParse(body["id"]?.ToString(), out var id) || id <= 0)
                return new JsonResult(new { message = "资源ID无效" }, 400);

            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(id);
            if (asset == null)
                return new JsonResult(new { message = "资源不存在" }, 404);

            // 软删除：标记为已删除并记录时间
            asset.IsDeleted = true;
            asset.DeletedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var specs = EnsureSpecs(asset);
            specs.LastUpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await KaxGlobal.AssetDataBase.UpdateAsync(asset);

            Logger.Info($"用户 {userName} 软删除了资源: {asset.Name} (id={id})");
            return new JsonResult(new { message = "资源已标记为已删除" });
        }
        catch (Exception ex)
        {
            Logger.Error($"删除资源失败: {ex.Message}");
            return new JsonResult(new { message = "服务器错误" }, 500);
        }
    }

    [HttpHandle("/api/asset/admin/restore", "POST", RateLimitMaxRequests = 10, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_RestoreAsset(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        if (principal == null) return new JsonResult(new { message = "无效的登录令牌" }, 401);

        var userName = principal.Identity?.Name;
        if (await KaxGlobal.IsUserBanned(userName!))
            return new JsonResult(new { message = "您的账号已被封禁" }, 403);
        if (!await IsAssetAdminUser(userName))
            return new JsonResult(new { message = "权限不足" }, 403);

        if (string.IsNullOrEmpty(request.Body))
            return new JsonResult(new { message = "请求体不能为空" }, 400);

        try
        {
            var body = JsonNode.Parse(request.Body);
            if (body == null) return new JsonResult(new { message = "无法解析请求体" }, 400);

            if (!int.TryParse(body["id"]?.ToString(), out var id) || id <= 0)
                return new JsonResult(new { message = "资源ID无效" }, 400);

            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(id);
            if (asset == null)
                return new JsonResult(new { message = "资源不存在" }, 404);

            asset.IsDeleted = false;
            asset.DeletedAt = 0;
            var specs = EnsureSpecs(asset);
            specs.LastUpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await KaxGlobal.AssetDataBase.UpdateAsync(asset);

            Logger.Info($"用户 {userName} 恢复了资源: {asset.Name} (id={id})");
            return new JsonResult(new { message = "资源已恢复" });
        }
        catch (Exception ex)
        {
            Logger.Error($"恢复资源失败: {ex.Message}");
            return new JsonResult(new { message = "服务器错误" }, 500);
        }
    }

    [HttpHandle("/api/asset/admin/list", "GET", RateLimitMaxRequests = 0, RateLimitWindowSeconds = 0, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Get_AssetList(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        if (principal == null) return new JsonResult(new { message = "无效的登录令牌" }, 401);

        var userName = principal.Identity?.Name;
        if (!await IsAssetAdminUser(userName))
            return new JsonResult(new { message = "权限不足" }, 403);

        try
        {
            // 支持查询参数：q (搜索), author, version, page, pageSize, includeDeleted
            var q = request.Query[("q")] ?? string.Empty;
            var authorFilter = request.Query[("author")] ?? string.Empty;
            var versionFilter = request.Query[("version")] ?? string.Empty;
            var includeDeleted = (request.Query[("includeDeleted")] ?? "false").ToLower() == "true";
            int page = 1;
            int pageSize = 20;
            if (!int.TryParse(request.Query[("page")], out page) || page <= 0) page = 1;
            if (!int.TryParse(request.Query[("pageSize")], out pageSize) || pageSize <= 0) pageSize = 20;

            var all = await KaxGlobal.AssetDataBase.SelectAllAsync();

            var filtered = all.AsQueryable();
            if (!includeDeleted)
            {
                filtered = filtered.Where(a => !a.IsDeleted).AsQueryable();
            }
            if (!string.IsNullOrEmpty(q))
            {
                var qlow = q.ToLowerInvariant();
                filtered = filtered.Where(a => (a.Name ?? string.Empty).ToLowerInvariant().Contains(qlow)
                    || (a.Author ?? string.Empty).ToLowerInvariant().Contains(qlow)
                    || (a.Version ?? string.Empty).ToLowerInvariant().Contains(qlow)).AsQueryable();
            }
            if (!string.IsNullOrEmpty(authorFilter))
            {
                var alow = authorFilter.ToLowerInvariant();
                filtered = filtered.Where(a => (a.Author ?? string.Empty).ToLowerInvariant().Contains(alow)).AsQueryable();
            }
            if (!string.IsNullOrEmpty(versionFilter))
            {
                var vlow = versionFilter.ToLowerInvariant();
                filtered = filtered.Where(a => (a.Version ?? string.Empty).ToLowerInvariant().Contains(vlow)).AsQueryable();
            }

            var total = filtered.Count();
            var items = filtered.OrderByDescending(a => a.Specs != null ? a.Specs.LastUpdatedAt : 0)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(a => new
                {
                    id = a.Id,
                    name = a.Name,
                    version = a.Version,
                    author = a.Author,
                    description = a.Description,
                    lastUpdatedAt = a.Specs != null ? a.Specs.LastUpdatedAt : 0,
                    isDeleted = a.IsDeleted
                })
                .ToList<object>();

            return new JsonResult(new { data = items, page, pageSize, total });
        }
        catch (Exception ex)
        {
            Logger.Error($"读取资源列表失败: {ex.Message}");
            return new JsonResult(new { message = "无法读取资源列表" }, 500);
        }
    }

    #endregion
}

using System;
using System.Linq;
using System.Threading.Tasks;
using Drx.Sdk.Network.Http;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.Results;
using Drx.Sdk.Network.Http.Configs;
using Drx.Sdk.Shared;
using KaxSocket;

namespace KaxSocket.Handlers;

/// <summary>
/// 公开资源查询模块 - 处理公开的资源列表查询、资源详情等功能
/// </summary>
public partial class KaxHttp
{
    #region 公开资源查询 (Public Asset Queries)

    // 公共接口：前端商品列表（分页，支持 q 搜索）
    [HttpHandle("/api/asset/list", "GET", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60)]
    public static async Task<IActionResult> Get_PublicAssetList(HttpRequest request)
    {
        try
        {
            var q = request.Query[("q")] ?? string.Empty;
            var categoryFilter = request.Query[("category")] ?? string.Empty;
            var minPriceStr = request.Query[("minPrice")] ?? string.Empty;
            var maxPriceStr = request.Query[("maxPrice")] ?? string.Empty;
            var sort = request.Query[("sort")] ?? "updated"; // updated | price_asc | price_desc

            int page = 1;
            int pageSize = 24;
            if (!int.TryParse(request.Query[("page")], out page) || page <= 0) page = 1;
            if (!int.TryParse(request.Query[("pageSize")], out pageSize) || pageSize <= 0) pageSize = 24;

            int.TryParse(minPriceStr, out var minPrice);
            int.TryParse(maxPriceStr, out var maxPrice);

            var all = await KaxGlobal.AssetDataBase.SelectAllAsync();
            var filtered = all.AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var qLower = q.ToLower();
                filtered = filtered.Where(a => (a.Name ?? string.Empty).ToLower().Contains(qLower) || (a.Description ?? string.Empty).ToLower().Contains(qLower));
            }

            if (!string.IsNullOrWhiteSpace(categoryFilter))
            {
                var cat = categoryFilter.ToLowerInvariant();
                filtered = filtered.Where(a => (a.Category ?? string.Empty).ToLowerInvariant() == cat).AsQueryable();
            }

            // 排序（使用第一个价格方案作为资源价格进行排序）
            IQueryable<Model.AssetModel> ordered;
            if (sort == "price_asc" || sort == "price_desc")
            {
                // 先将集合 materialize 成内存列表，避免在表达式树中访问子集合引发翻译/空引用问题
                var tempList = filtered.ToList();
                if (sort == "price_asc") tempList = tempList.OrderBy(a => (a.Prices != null && a.Prices.Any()) ? a.Prices.First().Price : 0).ToList();
                else tempList = tempList.OrderByDescending(a => (a.Prices != null && a.Prices.Any()) ? a.Prices.First().Price : 0).ToList();
                ordered = tempList.AsQueryable();
            }
            else ordered = filtered.OrderByDescending(a => a.LastUpdatedAt).AsQueryable();

            var total = ordered.Count();
            var pageItems = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var result = pageItems.Select(a => {
                var p = (a.Prices != null && a.Prices.Any()) ? a.Prices.First() : null;
                return new
                {
                    id = a.Id,
                    name = a.Name,
                    version = a.Version,
                    author = a.Author,
                    description = a.Description,
                    category = a.Category,
                    fileSize = a.FileSize,
                    // 价格相关字段：从第一个价格方案获取，向前兼容之前的单价格字段
                    rating = a.Rating,
                    reviewCount = a.ReviewCount,
                    downloads = a.Downloads,
                    license = a.License,
                    downloadUrl = a.DownloadUrl,
                    stock = a.Stock,
                    purchaseCount = a.PurchaseCount,
                    favoriteCount = a.FavoriteCount,
                    viewCount = a.ViewCount,
                    lastUpdatedAt = a.LastUpdatedAt,
                    price = p != null ? p.Price : 0,
                    originalPrice = p != null ? p.OriginalPrice : 0,
                    discountRate = p != null ? p.DiscountRate : 0.0,
                    salePrice = p != null ? (int)Math.Round(p.Price * (1.0 - p.DiscountRate)) : 0
                };
            }).ToList();

            return new JsonResult(new { code = 0, message = "成功", data = new { total = total, page = page, pageSize = pageSize, items = result } }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"获取公共资产列表失败: {ex.Message}");
            return new JsonResult(new { code = 500, message = "服务器错误" }, 500);
        }
    }

    // 按分类获取资源列表
    [HttpHandle("/api/asset/category/{category}", "GET", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60)]
    public static async Task<IActionResult> Get_AssetsByCategory(HttpRequest request)
    {
        if (!request.PathParameters.TryGetValue("category", out var category) || string.IsNullOrWhiteSpace(category))
            return new JsonResult(new { message = "category 参数不能为空" }, 400);

        try
        {
            int page = 1;
            int pageSize = 24;
            if (!int.TryParse(request.Query[("page")], out page) || page <= 0) page = 1;
            if (!int.TryParse(request.Query[("pageSize")], out pageSize) || pageSize <= 0) pageSize = 24;

            var all = await KaxGlobal.AssetDataBase.SelectAllAsync();
            var filtered = all.Where(a => string.Equals(a.Category ?? string.Empty, category, StringComparison.OrdinalIgnoreCase)).AsQueryable();
            var total = filtered.Count();
            var items = filtered.OrderByDescending(a => a.LastUpdatedAt).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(a => new
                {
                    id = a.Id,
                    name = a.Name,
                    version = a.Version,
                    author = a.Author,
                    description = a.Description,
                    category = a.Category,
                    fileSize = a.FileSize,
                    stock = a.Stock,
                    purchaseCount = a.PurchaseCount,
                    favoriteCount = a.FavoriteCount,
                    viewCount = a.ViewCount,
                    lastUpdatedAt = a.LastUpdatedAt
                }).ToList();

            return new JsonResult(new { code = 0, message = "成功", data = new { total = total, page = page, pageSize = pageSize, items = items } }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"按分类获取资源列表失败: {ex.Message}");
            return new JsonResult(new { code = 500, message = "服务器错误" }, 500);
        }
    }

    [HttpHandle("/api/asset/name/{assetId}", "GET", RateLimitMaxRequests = 120, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Get_AssetName(HttpRequest request)
    {
        if (!request.PathParameters.TryGetValue("assetId", out var assetIdString) || !int.TryParse(assetIdString, out var assetId) || assetId <= 0)
            return new JsonResult(new { message = "assetId 参数必须是大于 0 的整数" }, 400);

        try
        {
            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(assetId);
            if (asset == null)
            {
                return new JsonResult(new { assetId = assetId, name = string.Empty, code = 2004 }, 404);
            }

            return new JsonResult(new { assetId = asset.Id, name = asset.Name ?? string.Empty, code = 0 }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"Get_AssetName 失败: {ex.Message}");
            return new JsonResult(new { message = "服务器错误" }, 500);
        }
    }

    // 公共接口：获取资源详情（包含价格/库存/统计信息）
    [HttpHandle("/api/asset/detail/{id}", "GET", RateLimitMaxRequests = 120, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Get_AssetDetail(HttpRequest request)
    {
        if (!request.PathParameters.TryGetValue("id", out var idStr) || !int.TryParse(idStr, out var assetId) || assetId <= 0)
            return new JsonResult(new { message = "id 参数必须是大于 0 的整数" }, 400);

        try
        {
            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(assetId);
            if (asset == null)
                return new JsonResult(new { message = "资源不存在", id = assetId }, 404);

            // 增加一次浏览量（尝试更新，不阻塞主响应）
            try
            {
                asset.ViewCount = (asset.ViewCount <= 0) ? 1 : asset.ViewCount + 1;
                await KaxGlobal.AssetDataBase.UpdateAsync(asset);
            }
            catch (Exception ex)
            {
                Logger.Warn($"更新资源 {assetId} viewCount 失败: {ex.Message}");
            }

            // 将价格方案列表转换为可序列化的格式
            var pricesList = asset.Prices?.ToList();

            var result = new
            {
                id = asset.Id,
                name = asset.Name,
                version = asset.Version,
                author = asset.Author,
                description = asset.Description,
                category = asset.Category,
                fileSize = asset.FileSize,
                rating = asset.Rating,
                reviewCount = asset.ReviewCount,
                compatibility = asset.Compatibility,
                downloads = asset.Downloads,
                uploadDate = asset.UploadDate,
                license = asset.License,
                downloadUrl = asset.DownloadUrl,
                stock = asset.Stock,
                purchaseCount = asset.PurchaseCount,
                favoriteCount = asset.FavoriteCount,
                viewCount = asset.ViewCount,
                lastUpdatedAt = asset.LastUpdatedAt,
                isDeleted = asset.IsDeleted,
                prices = pricesList
            };

            return new JsonResult(new { code = 0, message = "成功", data = result }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"Get_AssetDetail 失败: {ex.Message}");
            return new JsonResult(new { message = "服务器错误" }, 500);
        }
    }

    #endregion
}

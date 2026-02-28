using System;
using System.Linq;
using System.Threading.Tasks;
using Drx.Sdk.Network.Http;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.Results;
using Drx.Sdk.Network.Http.Configs;
using Drx.Sdk.Shared;
using KaxSocket;
using static KaxSocket.Model.AssetModel;

namespace KaxSocket.Handlers;

/// <summary>
/// 公开资源查询模块：资源列表、详情、分类筛选及相关推荐。
/// </summary>
public partial class KaxHttp
{
    #region 公开资源查询

    /// <summary>
    /// 获取资源列表，支持关键词搜索、分类筛选和排序（updated / price_asc / price_desc），带分页。
    /// </summary>
    [HttpHandle("/api/asset/list", "GET", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60)]
    public static async Task<IActionResult> Get_PublicAssetList(HttpRequest request)
    {
        try
        {
            var q              = request.Query[("q")]         ?? string.Empty;
            var categoryFilter = request.Query[("category")]  ?? string.Empty;
            var minPriceStr    = request.Query[("minPrice")]  ?? string.Empty;
            var maxPriceStr    = request.Query[("maxPrice")]  ?? string.Empty;
            var sort           = request.Query[("sort")]      ?? "updated"; // updated | price_asc | price_desc

            int page = 1, pageSize = 24;
            if (!int.TryParse(request.Query[("page")],     out page)     || page     <= 0) page     = 1;
            if (!int.TryParse(request.Query[("pageSize")], out pageSize) || pageSize <= 0) pageSize = 24;

            int.TryParse(minPriceStr, out var minPrice);
            int.TryParse(maxPriceStr, out var maxPrice);

            var all      = await KaxGlobal.AssetDataBase.SelectAllAsync();
            var filtered = all.AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var qLower = q.ToLower();
                filtered = filtered.Where(a =>
                    (a.Name        ?? string.Empty).ToLower().Contains(qLower) ||
                    (a.Description ?? string.Empty).ToLower().Contains(qLower));
            }

            if (!string.IsNullOrWhiteSpace(categoryFilter))
            {
                var cat = categoryFilter.ToLowerInvariant();
                filtered = filtered.Where(a => (a.Category ?? string.Empty).ToLowerInvariant() == cat).AsQueryable();
            }

            // 价格排序需先 materialize，避免表达式树访问子集合时的翻译 / 空引用问题
            IQueryable<Model.AssetModel> ordered;
            if (sort == "price_asc" || sort == "price_desc")
            {
                var tempList = filtered.ToList();
                tempList = sort == "price_asc"
                    ? tempList.OrderBy(a => a.Prices != null && a.Prices.Any() ? a.Prices.First().Price : 0).ToList()
                    : tempList.OrderByDescending(a => a.Prices != null && a.Prices.Any() ? a.Prices.First().Price : 0).ToList();
                ordered = tempList.AsQueryable();
            }
            else
            {
                ordered = filtered.OrderByDescending(a => a.Specs != null ? a.Specs.LastUpdatedAt : 0).AsQueryable();
            }

            var total     = ordered.Count();
            var pageItems = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var result = pageItems.Select(a =>
            {
                var p = a.Prices != null && a.Prices.Any() ? a.Prices.First() : null;
                var s = a.Specs;
                return new
                {
                    id           = a.Id,
                    name         = a.Name,
                    version      = a.Version,
                    author       = a.Author,
                    description  = a.Description,
                    category     = a.Category,
                    primaryImage   = a.PrimaryImage,
                    thumbnailImage = a.ThumbnailImage,
                    fileSize       = s?.FileSize      ?? 0,
                    rating         = s?.Rating        ?? 0.0,
                    reviewCount    = s?.ReviewCount   ?? 0,
                    downloads      = s?.Downloads     ?? 0,
                    license        = s?.License       ?? string.Empty,
                    downloadUrl    = s?.DownloadUrl   ?? string.Empty,
                    purchaseCount  = s?.PurchaseCount ?? 0,
                    favoriteCount  = s?.FavoriteCount ?? 0,
                    viewCount      = s?.ViewCount     ?? 0,
                    lastUpdatedAt  = s?.LastUpdatedAt ?? 0,
                    price         = p != null ? p.Price         : 0,
                    originalPrice = p != null ? p.OriginalPrice : 0,
                    discountRate  = p != null ? p.DiscountRate  : 0.0,
                    salePrice     = p != null ? (int)Math.Round(p.Price * (1.0 - p.DiscountRate)) : 0,
                    specs = s != null ? new
                    {
                        fileSize      = s.FileSize,
                        rating        = s.Rating,
                        reviewCount   = s.ReviewCount,
                        downloads     = s.Downloads,
                        license       = s.License,
                        downloadUrl   = s.DownloadUrl,
                        purchaseCount = s.PurchaseCount,
                        favoriteCount = s.FavoriteCount,
                        viewCount     = s.ViewCount,
                        lastUpdatedAt = s.LastUpdatedAt
                    } : null
                };
            }).ToList();

            return new JsonResult(new { code = 0, message = "成功", data = new { total, page, pageSize, items = result } }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"获取公共资产列表失败: {ex.Message}");
            return new JsonResult(new { code = 500, message = "服务器错误" }, 500);
        }
    }

    /// <summary>
    /// 按分类获取资源列表，按最后更新时间倒序，带分页。
    /// </summary>
    [HttpHandle("/api/asset/category/{category}", "GET", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60)]
    public static async Task<IActionResult> Get_AssetsByCategory(HttpRequest request)
    {
        if (!request.PathParameters.TryGetValue("category", out var category) || string.IsNullOrWhiteSpace(category))
            return new JsonResult(new { message = "category 参数不能为空" }, 400);

        try
        {
            int page = 1, pageSize = 24;
            if (!int.TryParse(request.Query[("page")],     out page)     || page     <= 0) page     = 1;
            if (!int.TryParse(request.Query[("pageSize")], out pageSize) || pageSize <= 0) pageSize = 24;

            var all      = await KaxGlobal.AssetDataBase.SelectAllAsync();
            var filtered = all
                .Where(a => string.Equals(a.Category ?? string.Empty, category, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var total = filtered.Count;
            var items = filtered
                .OrderByDescending(a => a.Specs?.LastUpdatedAt ?? 0)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a =>
                {
                    var s = a.Specs;
                    return new
                    {
                        id            = a.Id,
                        name          = a.Name,
                        version       = a.Version,
                        author        = a.Author,
                        description   = a.Description,
                        category      = a.Category,
                        fileSize      = s?.FileSize      ?? 0,
                        purchaseCount = s?.PurchaseCount ?? 0,
                        favoriteCount = s?.FavoriteCount ?? 0,
                        viewCount     = s?.ViewCount     ?? 0,
                        lastUpdatedAt = s?.LastUpdatedAt ?? 0
                    };
                }).ToList();

            return new JsonResult(new { code = 0, message = "成功", data = new { total, page, pageSize, items } }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"按分类获取资源列表失败: {ex.Message}");
            return new JsonResult(new { code = 500, message = "服务器错误" }, 500);
        }
    }

    /// <summary>
    /// 根据资源 ID 获取资源名称。
    /// </summary>
    [HttpHandle("/api/asset/name/{assetId}", "GET", RateLimitMaxRequests = 120, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Get_AssetName(HttpRequest request)
    {
        if (!request.PathParameters.TryGetValue("assetId", out var assetIdString) || !int.TryParse(assetIdString, out var assetId) || assetId <= 0)
            return new JsonResult(new { message = "assetId 参数必须是大于 0 的整数" }, 400);

        try
        {
            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(assetId);
            if (asset == null)
                return new JsonResult(new { assetId, name = string.Empty, code = 2004 }, 404);

            return new JsonResult(new { assetId = asset.Id, name = asset.Name ?? string.Empty, code = 0 }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"Get_AssetName 失败: {ex.Message}");
            return new JsonResult(new { message = "服务器错误" }, 500);
        }
    }

    /// <summary>
    /// 获取资源详情，包含价格方案、规格信息和统计数据，同时增加一次浏览量。
    /// </summary>
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

            var specs = EnsureSpecs(asset);

            try
            {
                specs.ViewCount = specs.ViewCount <= 0 ? 1 : specs.ViewCount + 1;
                await KaxGlobal.AssetDataBase.UpdateAsync(asset);
            }
            catch (Exception ex)
            {
                Logger.Warn($"更新资源 {assetId} viewCount 失败: {ex.Message}");
            }

            var pricesList = asset.Prices?.ToList();
            // 截图与标签均以分隔符存储，输出时转为数组
            var screenshotList = (asset.Screenshots ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var tagList        = (asset.Tags        ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var specsDto = new
            {
                fileSize      = specs.FileSize,
                rating        = specs.Rating,
                reviewCount   = specs.ReviewCount,
                compatibility = specs.Compatibility,
                downloads     = specs.Downloads,
                uploadDate    = specs.UploadDate,
                license       = specs.License,
                downloadUrl   = specs.DownloadUrl,
                purchaseCount = specs.PurchaseCount,
                favoriteCount = specs.FavoriteCount,
                viewCount     = specs.ViewCount,
                lastUpdatedAt = specs.LastUpdatedAt
            };

            var result = new
            {
                id          = asset.Id,
                name        = asset.Name,
                version     = asset.Version,
                author      = asset.Author,
                description = asset.Description,
                category    = asset.Category,
                isDeleted   = asset.IsDeleted,
                primaryImage   = asset.PrimaryImage,
                thumbnailImage = asset.ThumbnailImage,
                screenshots    = screenshotList,
                tags           = tagList,
                prices         = pricesList,
                specs          = specsDto
            };

            return new JsonResult(new { code = 0, message = "成功", data = result }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"Get_AssetDetail 失败: {ex.Message}");
            return new JsonResult(new { message = "服务器错误" }, 500);
        }
    }

    /// <summary>
    /// 获取相关推荐资源：优先同分类，按热度（downloads×0.6 + rating×10 + purchaseCount×0.3）排序，
    /// 同分类不足时从全站补充，最多返回 top 条（默认 4，最大 20）。
    /// </summary>
    [HttpHandle("/api/asset/related/{id}", "GET", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60)]
    public static async Task<IActionResult> Get_RelatedAssets(HttpRequest request)
    {
        if (!request.PathParameters.TryGetValue("id", out var idStr) || !int.TryParse(idStr, out var assetId) || assetId <= 0)
            return new JsonResult(new { code = 400, message = "id 参数必须是大于 0 的整数" }, 400);

        int top = 4;
        if (int.TryParse(request.Query[("top")], out var topParam) && topParam > 0 && topParam <= 20)
            top = topParam;

        try
        {
            var currentAsset = await KaxGlobal.AssetDataBase.SelectByIdAsync(assetId);
            var all          = await KaxGlobal.AssetDataBase.SelectAllAsync();
            var category     = currentAsset?.Category ?? string.Empty;

            static double HotScore(Model.AssetModel a) =>
                (a.Specs?.Downloads ?? 0) * 0.6 + (a.Specs?.Rating ?? 0) * 10 + (a.Specs?.PurchaseCount ?? 0) * 0.3;

            var candidates = all
                .Where(a => !a.IsDeleted && a.Id != assetId)
                .Where(a => string.IsNullOrEmpty(category) || string.Equals(a.Category ?? string.Empty, category, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(HotScore)
                .Take(top)
                .ToList();

            if (candidates.Count < top)
            {
                var existingIds = candidates.Select(c => c.Id).ToHashSet();
                existingIds.Add(assetId);
                var extras = all
                    .Where(a => !a.IsDeleted && !existingIds.Contains(a.Id))
                    .OrderByDescending(HotScore)
                    .Take(top - candidates.Count)
                    .ToList();
                candidates.AddRange(extras);
            }

            var items = candidates.Select(a =>
            {
                var p = a.Prices != null && a.Prices.Any() ? a.Prices.First() : null;
                var s = a.Specs;
                return new
                {
                    id             = a.Id,
                    name           = a.Name,
                    category       = a.Category,
                    author         = a.Author,
                    primaryImage   = a.PrimaryImage,
                    thumbnailImage = a.ThumbnailImage,
                    rating         = s?.Rating    ?? 0.0,
                    downloads      = s?.Downloads ?? 0,
                    price         = p != null ? p.Price         : 0,
                    originalPrice = p != null ? p.OriginalPrice : 0,
                    discountRate  = p != null ? p.DiscountRate  : 0.0,
                    salePrice     = p != null ? (int)Math.Round(p.Price * (1.0 - p.DiscountRate)) : 0
                };
            }).ToList();

            return new JsonResult(new { code = 0, message = "成功", data = items }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"获取相关推荐失败: {ex.Message}");
            return new JsonResult(new { code = 500, message = "服务器错误" }, 500);
        }
    }

    #endregion
}

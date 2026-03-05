using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Drx.Sdk.Network.Http;
using Drx.Sdk.Network.Http.Api;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.Results;
using Drx.Sdk.Network.Http.Configs;
using Drx.Sdk.Shared;
using KaxSocket;
using KaxSocket.Handlers.Helpers;
using KaxSocket.Model;
using static KaxSocket.Model.AssetModel;

namespace KaxSocket.Handlers;

/// <summary>
/// 开发者中心模块 — 用户自助资产管理 + 管理员审核
/// </summary>
public partial class KaxHttp
{
    /// <summary>重新提交审核的冷却时间（4 小时，单位：秒）</summary>
    private const long REVIEW_COOLDOWN_SECONDS = 4 * 60 * 60;

    /// <summary>将资产状态转为中文文本</summary>
    private static string AssetStatusText(AssetStatus status) => status switch
    {
        AssetStatus.PendingReview => "审核中",
        AssetStatus.Rejected => "审核拒绝",
        AssetStatus.ApprovedPendingPublish => "待发布",
        AssetStatus.Active => "已上线",
        AssetStatus.OffShelf => "已下架",
        _ => "未知"
    };

    /// <summary>用于前端“提交日期”展示：始终优先首次提交时间</summary>
    private static long DisplaySubmittedAt(Model.AssetModel asset)
        => asset.FirstSubmittedAt > 0 ? asset.FirstSubmittedAt : asset.LastSubmittedAt;

    #region 开发者中心 — 用户端

    /// <summary>
    /// 获取当前用户自己的资产列表（开发者中心 - 我的资产）
    /// </summary>
    [HttpHandle("/api/developer/assets", "GET", RateLimitMaxRequests = 30, RateLimitWindowSeconds = 60)]
    public static async Task<IActionResult> Get_DeveloperAssets(HttpRequest request)
    {
        var (user, authError) = await Api.GetUserAsync(request);
        if (authError != null) return authError;
        var userName = user!.UserName;

        try
        {
            var (page, pageSize) = ApiPagination.Parse(request, defaultPageSize: 20);

            var statusFilter = request.Query["status"] ?? string.Empty;

            var all = await KaxGlobal.AssetDataBase.SelectAllAsync();
            var myAssets = all.Where(a => a.AuthorId == user.Id && !a.IsDeleted);

            if (!string.IsNullOrEmpty(statusFilter) && int.TryParse(statusFilter, out var statusVal) && Enum.IsDefined(typeof(AssetStatus), statusVal))
                myAssets = myAssets.Where(a => (int)a.Status == statusVal);

            var total = myAssets.Count();
            var items = myAssets
                .OrderByDescending(a => a.Specs?.LastUpdatedAt ?? 0)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a =>
                {
                    var p = a.Prices?.FirstOrDefault();
                    var s = a.Specs;
                    return new
                    {
                        id = a.Id,
                        name = a.Name,
                        version = a.Version,
                        description = a.Description,
                        category = a.Category,
                        status = (int)a.Status,
                        statusText = AssetStatusText(a.Status),
                        rejectReason = a.RejectReason ?? string.Empty,
                        lastSubmittedAt = DisplaySubmittedAt(a),
                        coverImage = a.CoverImage,
                        iconImage = a.IconImage,
                        price = p?.Price ?? 0,
                        originalPrice = p?.OriginalPrice ?? 0,
                        downloads = s?.Downloads ?? 0,
                        viewCount = s?.ViewCount ?? 0,
                        lastUpdatedAt = s?.LastUpdatedAt ?? 0
                    };
                }).ToList();

            return new JsonResult(new { code = 0, message = "成功", data = new { total, page, pageSize, items } }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"获取开发者资产列表失败: {ex.Message}");
            return new JsonResult(new { code = 500, message = "服务器错误" }, 500);
        }
    }

    /// <summary>
    /// 开发者创建新资产（任何登录用户均可）
    /// </summary>
    [HttpHandle("/api/developer/asset/create", "POST", RateLimitMaxRequests = 5, RateLimitWindowSeconds = 60)]
    public static async Task<IActionResult> Post_DeveloperCreateAsset(HttpRequest request)
    {
        var (user, authError) = await Api.GetUserAsync(request);
        if (authError != null) return authError;
        var userName = user!.UserName;

        if (!ApiBody.TryParse(request, out var body, out var parseError))
            return parseError!;

        try
        {

            var name = body["name"]?.ToString();
            var version = body["version"]?.ToString();
            var description = body["description"]?.ToString() ?? "";

            if (string.IsNullOrEmpty(name) || name.Length > 100)
                return new JsonResult(new { message = "资源名称无效（1-100字符）" }, 400);
            if (string.IsNullOrEmpty(version) || version.Length > 50)
                return new JsonResult(new { message = "版本字段无效（1-50字符）" }, 400);
            if (description.Length > 500)
                return new JsonResult(new { message = "描述过长（最多500字符）" }, 400);

            var asset = new Model.AssetModel
            {
                Name = name,
                Version = version,
                AuthorId = user.Id,
                Description = description,
                Status = AssetStatus.PendingReview,
                LastSubmittedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                FirstSubmittedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            // 可选字段
            if (body["category"] != null)
                asset.Category = body["category"]?.ToString() ?? string.Empty;
            if (body["tags"] != null)
                asset.Tags = body["tags"]?.ToString() ?? string.Empty;
            if (body["coverImage"] != null)
                asset.CoverImage = body["coverImage"]?.ToString() ?? string.Empty;
            if (body["iconImage"] != null)
                asset.IconImage = body["iconImage"]?.ToString() ?? string.Empty;
            if (body["screenshots"] != null)
                asset.Screenshots = body["screenshots"]?.ToString() ?? string.Empty;

            KaxGlobal.AssetDataBase.Insert(asset);

            // 创建后再写入子表，确保价格/规格子表 ParentId 使用真实资产 ID
            ApplyPriceInfoToAsset(asset, body);
            ApplySpecsInfoToAsset(asset, body);
            ApplyLanguageSupportsToAsset(asset, body);
            await KaxGlobal.AssetDataBase.UpdateAsync(asset);

            Logger.Info($"开发者 {userName} (id={user.Id}) 创建了资源: {name} (v{version})，状态：审核中");
            return new JsonResult(new { code = 0, message = "资源已创建，等待审核", id = asset.Id }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"开发者创建资源失败: {ex.Message}");
            return new JsonResult(new { code = 500, message = "服务器错误" }, 500);
        }
    }

    /// <summary>
    /// 开发者更新自己的资产（仅在审核拒绝或待发布状态下可修改）
    /// </summary>
    [HttpHandle("/api/developer/asset/update", "POST", RateLimitMaxRequests = 10, RateLimitWindowSeconds = 60)]
    public static async Task<IActionResult> Post_DeveloperUpdateAsset(HttpRequest request)
    {
        var (user, authError) = await Api.GetUserAsync(request);
        if (authError != null) return authError;
        var userName = user!.UserName;

        if (!ApiBody.TryParse(request, out var body, out var parseError))
            return parseError!;

        try
        {

            if (!int.TryParse(body["id"]?.ToString(), out var id) || id <= 0)
                return new JsonResult(new { message = "资源ID无效" }, 400);

            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(id);
            if (asset == null) return new JsonResult(new { message = "资源不存在" }, 404);

            // 验证所有权
            if (asset.AuthorId != user.Id)
                return new JsonResult(new { message = "无权修改此资源" }, 403);

            // 仅在被拒绝或待发布状态下允许修改
            if (asset.Status == AssetStatus.PendingReview)
                return new JsonResult(new { message = "资源正在审核中，无法修改" }, 400);

            // 基本字段更新
            if (body["name"] != null)
            {
                var name = body["name"]?.ToString() ?? string.Empty;
                if (name.Length > 100) return new JsonResult(new { message = "名称过长（最多100字符）" }, 400);
                if (!string.IsNullOrEmpty(name)) asset.Name = name;
            }
            if (body["version"] != null)
            {
                var version = body["version"]?.ToString() ?? string.Empty;
                if (version.Length > 50) return new JsonResult(new { message = "版本过长（最多50字符）" }, 400);
                if (!string.IsNullOrEmpty(version)) asset.Version = version;
            }
            if (body["description"] != null)
            {
                var desc = body["description"]?.ToString() ?? string.Empty;
                if (desc.Length > 500) return new JsonResult(new { message = "描述过长（最多500字符）" }, 400);
                asset.Description = desc;
            }
            if (body["category"] != null)
                asset.Category = body["category"]?.ToString() ?? string.Empty;
            if (body["tags"] != null)
                asset.Tags = body["tags"]?.ToString() ?? string.Empty;
            if (body["coverImage"] != null)
                asset.CoverImage = body["coverImage"]?.ToString() ?? string.Empty;
            if (body["iconImage"] != null)
                asset.IconImage = body["iconImage"]?.ToString() ?? string.Empty;
            if (body["screenshots"] != null)
                asset.Screenshots = body["screenshots"]?.ToString() ?? string.Empty;

            ApplyPriceInfoToAsset(asset, body);
            ApplySpecsInfoToAsset(asset, body);
            ApplyLanguageSupportsToAsset(asset, body);

            await KaxGlobal.AssetDataBase.UpdateAsync(asset);

            Logger.Info($"开发者 {userName} 更新了资源: {asset.Name} (id={id})");
            return new JsonResult(new { code = 0, message = "资源已更新" }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"开发者更新资源失败: {ex.Message}");
            return new JsonResult(new { code = 500, message = "服务器错误" }, 500);
        }
    }

    /// <summary>
    /// 开发者获取自己的单个资产详情（完整信息，用于编辑）
    /// </summary>
    [HttpHandle("/api/developer/asset/{id}", "GET", RateLimitMaxRequests = 30, RateLimitWindowSeconds = 60)]
    public static async Task<IActionResult> Get_DeveloperAssetDetail(HttpRequest request)
    {
        var (user, authError) = await Api.GetUserAsync(request);
        if (authError != null) return authError;
        if (user == null) return new JsonResult(new { message = "用户认证失败" }, 401);

        if (!request.PathParameters.TryGetValue("id", out var idStr) || !int.TryParse(idStr, out var assetId) || assetId <= 0)
            return new JsonResult(new { message = "id 参数无效" }, 400);

        try
        {
            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(assetId);
            if (asset == null) return new JsonResult(new { message = "资源不存在" }, 404);
            if (asset.AuthorId != user.Id)
                return new JsonResult(new { message = "无权查看此资源" }, 403);

            var specs = EnsureSpecs(asset);
            var pricesList = asset.Prices?.Select(p => new
            {
                id           = p.Id,
                label        = p.Label ?? string.Empty,
                price        = p.Price / 100.0,
                originalPrice = p.OriginalPrice / 100.0,
                unit         = p.Unit ?? "once",
                duration     = p.Duration,
                durationDays = p.DurationDays,
                stock        = p.Stock
            }).ToList();
            var languageSupports = GetLanguageSupports(asset).Select(x => new
            {
                name = x.Name,
                isSupported = x.IsSupported
            }).ToList();
            var screenshotList = (asset.Screenshots ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var tagList = (asset.Tags ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return new JsonResult(new
            {
                code = 0,
                message = "成功",
                data = new
                {
                    id = asset.Id,
                    name = asset.Name,
                    version = asset.Version,
                    authorId = asset.AuthorId,
                    description = asset.Description,
                    category = asset.Category,
                    status = (int)asset.Status,
                    statusText = AssetStatusText(asset.Status),
                    rejectReason = asset.RejectReason ?? string.Empty,
                    lastSubmittedAt = DisplaySubmittedAt(asset),
                    coverImage = asset.CoverImage,
                    iconImage = asset.IconImage,
                    screenshots = screenshotList,
                    tags = tagList,
                    languageSupportsJson = asset.LanguageSupportsJson,
                    languageSupports = languageSupports,
                    prices = pricesList,
                    specs = new
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
                    }
                }
            }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"获取开发者资产详情失败: {ex.Message}");
            return new JsonResult(new { code = 500, message = "服务器错误" }, 500);
        }
    }

    /// <summary>
    /// 提交资产审核（被拒绝后重新提交，有4小时冷却时间）
    /// </summary>
    [HttpHandle("/api/developer/asset/submit", "POST", RateLimitMaxRequests = 5, RateLimitWindowSeconds = 60)]
    public static async Task<IActionResult> Post_DeveloperSubmitReview(HttpRequest request)
    {
        var (user, authError) = await Api.GetUserAsync(request);
        if (authError != null) return authError;
        var userName = user!.UserName;

        if (!ApiBody.TryParse(request, out var body, out var parseError))
            return parseError!;

        try
        {

            if (!int.TryParse(body["id"]?.ToString(), out var id) || id <= 0)
                return new JsonResult(new { message = "资源ID无效" }, 400);

            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(id);
            if (asset == null) return new JsonResult(new { message = "资源不存在" }, 404);
            if (asset.AuthorId != user.Id)
                return new JsonResult(new { message = "无权操作此资源" }, 403);

            // 检查当前状态：
            // - PendingReview：已在审核中，不可重复提交
            // - Active：允许开发者发起重审（将自动下架并进入审核中）
            // - Rejected / ApprovedPendingPublish / OffShelf：允许提交
            if (asset.Status == AssetStatus.PendingReview)
                return new JsonResult(new { message = "资源已在审核中" }, 400);

            var wasActive = asset.Status == AssetStatus.Active;

            // 检查4小时冷却时间
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var elapsed = now - asset.LastSubmittedAt;
            if (!wasActive && elapsed < REVIEW_COOLDOWN_SECONDS)
            {
                var remaining = REVIEW_COOLDOWN_SECONDS - elapsed;
                var hours = remaining / 3600;
                var minutes = (remaining % 3600) / 60;
                return new JsonResult(new
                {
                    message = $"提交审核冷却中，请在 {hours} 小时 {minutes} 分钟后重试",
                    cooldownRemaining = remaining
                }, 429);
            }

            // 可选：允许在“提交审核”时附带最新资料（包括价格方案）一起保存
            // 用于修复“提交时价格表未写入数据库”的场景
            if (body["name"] != null)
            {
                var name = body["name"]?.ToString() ?? string.Empty;
                if (name.Length > 100)
                    return new JsonResult(new { message = "名称过长（最多100字符）" }, 400);
                if (!string.IsNullOrEmpty(name)) asset.Name = name;
            }
            if (body["version"] != null)
            {
                var version = body["version"]?.ToString() ?? string.Empty;
                if (version.Length > 50)
                    return new JsonResult(new { message = "版本过长（最多50字符）" }, 400);
                if (!string.IsNullOrEmpty(version)) asset.Version = version;
            }
            if (body["description"] != null)
            {
                var desc = body["description"]?.ToString() ?? string.Empty;
                if (desc.Length > 500)
                    return new JsonResult(new { message = "描述过长（最多500字符）" }, 400);
                asset.Description = desc;
            }
            if (body["category"] != null) asset.Category = body["category"]?.ToString() ?? string.Empty;
            if (body["tags"] != null) asset.Tags = body["tags"]?.ToString() ?? string.Empty;
            if (body["coverImage"] != null) asset.CoverImage = body["coverImage"]?.ToString() ?? string.Empty;
            if (body["iconImage"] != null) asset.IconImage = body["iconImage"]?.ToString() ?? string.Empty;
            if (body["screenshots"] != null) asset.Screenshots = body["screenshots"]?.ToString() ?? string.Empty;

            ApplyPriceInfoToAsset(asset, body);
            ApplySpecsInfoToAsset(asset, body);
            ApplyLanguageSupportsToAsset(asset, body);

            asset.Status = AssetStatus.PendingReview;
            asset.LastSubmittedAt = now;
            if (asset.FirstSubmittedAt <= 0)
                asset.FirstSubmittedAt = now;
            asset.RejectReason = string.Empty;
            var specs = EnsureSpecs(asset);
            specs.LastUpdatedAt = now;
            await KaxGlobal.AssetDataBase.UpdateAsync(asset);

            Logger.Info($"开发者 {userName} 提交资源 {asset.Name} (id={id}) 进入审核" + (wasActive ? "（由已上线状态发起，已自动下架）" : string.Empty));
            return new JsonResult(new
            {
                code = 0,
                message = wasActive ? "资源已下架并提交重审" : "已提交审核",
                data = new
                {
                    wasActive,
                    currentStatus = (int)AssetStatus.PendingReview
                }
            }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"提交审核失败: {ex.Message}");
            return new JsonResult(new { code = 500, message = "服务器错误" }, 500);
        }
    }

    /// <summary>
    /// 开发者发布资产（审核通过后，从"待发布"变为"已上线"）
    /// </summary>
    [HttpHandle("/api/developer/asset/publish", "POST", RateLimitMaxRequests = 10, RateLimitWindowSeconds = 60)]
    public static async Task<IActionResult> Post_DeveloperPublishAsset(HttpRequest request)
    {
        var (user, authError) = await Api.GetUserAsync(request);
        if (authError != null) return authError;
        var userName = user!.UserName;

        if (!ApiBody.TryParse(request, out var body, out var parseError))
            return parseError!;

        try
        {
            if (!int.TryParse(body?["id"]?.ToString(), out var id) || id <= 0)
                return new JsonResult(new { message = "资源ID无效" }, 400);

            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(id);
            if (asset == null) return new JsonResult(new { message = "资源不存在" }, 404);
            if (asset.AuthorId != user.Id)
                return new JsonResult(new { message = "无权操作此资源" }, 403);
            if (asset.Status != AssetStatus.ApprovedPendingPublish)
                return new JsonResult(new { message = "仅审核通过待发布的资源可发布" }, 400);

            asset.Status = AssetStatus.Active;
            var specs = EnsureSpecs(asset);
            specs.LastUpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await KaxGlobal.AssetDataBase.UpdateAsync(asset);

            Logger.Info($"开发者 {userName} 发布了资源: {asset.Name} (id={id})");
            return new JsonResult(new { code = 0, message = "资源已发布到商店" }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"发布资源失败: {ex.Message}");
            return new JsonResult(new { code = 500, message = "服务器错误" }, 500);
        }
    }

    #endregion

    #region 审核管理 — 管理员端

    /// <summary>
    /// 获取待审核资产列表（管理员专用）
    /// </summary>
    [HttpHandle("/api/review/pending", "GET", RateLimitMaxRequests = 30, RateLimitWindowSeconds = 60)]
    public static async Task<IActionResult> Get_ReviewPendingList(HttpRequest request)
    {
        var (_, authError) = await Api.RequireAdminNameAsync(request);
        if (authError != null) return authError;

        try
        {
            var (page, pageSize) = ApiPagination.Parse(request, defaultPageSize: 20);

            var statusFilter = request.Query["status"] ?? "0"; // 默认只看审核中
            int statusVal = 0;
            int.TryParse(statusFilter, out statusVal);

            var all = await KaxGlobal.AssetDataBase.SelectAllAsync();
            var filtered = all.Where(a => !a.IsDeleted && (int)a.Status == statusVal);

            var total = filtered.Count();
            var items = filtered
                .OrderByDescending(a => a.LastSubmittedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a =>
                {
                    var s = a.Specs;
                    // 尝试获取作者用户名
                    string authorName = "未知";
                    try
                    {
                        var authorUser = KaxGlobal.UserDatabase.SelectByIdAsync(a.AuthorId).GetAwaiter().GetResult();
                        if (authorUser != null) authorName = authorUser.UserName ?? "未知";
                    }
                    catch { }

                    return new
                    {
                        id = a.Id,
                        name = a.Name,
                        version = a.Version,
                        authorId = a.AuthorId,
                        authorName,
                        description = a.Description,
                        category = a.Category,
                        status = (int)a.Status,
                        lastSubmittedAt = DisplaySubmittedAt(a),
                        coverImage = a.CoverImage,
                        iconImage = a.IconImage,
                        downloads = s?.Downloads ?? 0,
                        viewCount = s?.ViewCount ?? 0,
                        lastUpdatedAt = s?.LastUpdatedAt ?? 0
                    };
                }).ToList();

            return new JsonResult(new { code = 0, message = "成功", data = new { total, page, pageSize, items } }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"获取待审核列表失败: {ex.Message}");
            return new JsonResult(new { code = 500, message = "服务器错误" }, 500);
        }
    }

    /// <summary>
    /// 获取审核资产完整详情（管理员专用）
    /// </summary>
    [HttpHandle("/api/review/asset/{id}", "GET", RateLimitMaxRequests = 30, RateLimitWindowSeconds = 60)]
    public static async Task<IActionResult> Get_ReviewAssetDetail(HttpRequest request)
    {
        var (_, authError) = await Api.RequireAdminNameAsync(request);
        if (authError != null) return authError;

        if (!request.PathParameters.TryGetValue("id", out var idStr) || !int.TryParse(idStr, out var assetId) || assetId <= 0)
            return new JsonResult(new { message = "id 参数无效" }, 400);

        try
        {
            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(assetId);
            if (asset == null) return new JsonResult(new { message = "资源不存在" }, 404);

            var specs = EnsureSpecs(asset);
            var pricesList = asset.Prices?.Select(p => new
            {
                id           = p.Id,
                label        = p.Label ?? string.Empty,
                price        = p.Price / 100.0,
                originalPrice = p.OriginalPrice / 100.0,
                unit         = p.Unit ?? "once",
                duration     = p.Duration,
                durationDays = p.DurationDays,
                stock        = p.Stock
            }).ToList();
            var screenshotList = (asset.Screenshots ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var tagList = (asset.Tags ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // 查作者信息
            string authorName = "未知";
            try
            {
                var authorUser = await KaxGlobal.UserDatabase.SelectByIdAsync(asset.AuthorId);
                if (authorUser != null) authorName = authorUser.UserName ?? "未知";
            }
            catch { }

            return new JsonResult(new
            {
                code = 0,
                message = "成功",
                data = new
                {
                    id = asset.Id,
                    name = asset.Name,
                    version = asset.Version,
                    authorId = asset.AuthorId,
                    authorName,
                    description = asset.Description,
                    category = asset.Category,
                    status = (int)asset.Status,
                    rejectReason = asset.RejectReason ?? string.Empty,
                    lastSubmittedAt = DisplaySubmittedAt(asset),
                    coverImage = asset.CoverImage,
                    iconImage = asset.IconImage,
                    screenshots = screenshotList,
                    tags = tagList,
                    prices = pricesList,
                    specs = new
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
                    }
                }
            }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"获取审核资产详情失败: {ex.Message}");
            return new JsonResult(new { code = 500, message = "服务器错误" }, 500);
        }
    }

    /// <summary>
    /// 审核通过（管理员将资产状态设为"审核通过待发布"，等待开发者发布）
    /// 状态流转：PendingReview(0) -> ApprovedPendingPublish(2)
    /// </summary>
    [HttpHandle("/api/review/approve", "POST", RateLimitMaxRequests = 20, RateLimitWindowSeconds = 60)]
    public static async Task<IActionResult> Post_ReviewApprove(HttpRequest request)
    {
        var (operatorName, authError) = await Api.RequireAdminNameAsync(request);
        if (authError != null) return authError;

        if (!ApiBody.TryParse(request, out var body, out var parseError))
            return parseError!;

        try
        {
            if (!int.TryParse(body?["id"]?.ToString(), out var id) || id <= 0)
                return new JsonResult(new { message = "资源ID无效" }, 400);

            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(id);
            if (asset == null) return new JsonResult(new { message = "资源不存在" }, 404);

            if (asset.Status != AssetStatus.PendingReview)
                return new JsonResult(new { message = "仅审核中的资源可执行通过操作" }, 400);

            // 审核通过后设为待发布状态，需开发者手动发布才上线
            asset.Status = AssetStatus.ApprovedPendingPublish;
            asset.RejectReason = string.Empty;
            var specs = EnsureSpecs(asset);
            specs.LastUpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await KaxGlobal.AssetDataBase.UpdateAsync(asset);
            await NotifyAssetActionEmailAsync(asset, "审核通过", null, operatorName ?? "系统");

            Logger.Info($"管理员 {operatorName} 通过了资源审核: {asset.Name} (id={id})，状态变更为待发布");
            return new JsonResult(new { code = 0, message = "审核通过，等待开发者发布" }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"审核通过操作失败: {ex.Message}");
            return new JsonResult(new { code = 500, message = "服务器错误" }, 500);
        }
    }

    /// <summary>
    /// 审核拒绝
    /// </summary>
    [HttpHandle("/api/review/reject", "POST", RateLimitMaxRequests = 20, RateLimitWindowSeconds = 60)]
    public static async Task<IActionResult> Post_ReviewReject(HttpRequest request)
    {
        var (operatorName, authError) = await Api.RequireAdminNameAsync(request);
        if (authError != null) return authError;

        if (!ApiBody.TryParse(request, out var body, out var parseError))
            return parseError!;

        try
        {
            if (!int.TryParse(body?["id"]?.ToString(), out var id) || id <= 0)
                return new JsonResult(new { message = "资源ID无效" }, 400);

            var reason = body?["reason"]?.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(reason))
                return new JsonResult(new { message = "拒绝原因不能为空" }, 400);
            if (reason.Length > 500)
                return new JsonResult(new { message = "拒绝原因过长（最多500字符）" }, 400);

            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(id);
            if (asset == null) return new JsonResult(new { message = "资源不存在" }, 404);

            if (asset.Status != AssetStatus.PendingReview)
                return new JsonResult(new { message = "仅审核中的资源可执行拒绝操作" }, 400);

            asset.Status = AssetStatus.Rejected;
            asset.RejectReason = reason;
            var specs = EnsureSpecs(asset);
            specs.LastUpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await KaxGlobal.AssetDataBase.UpdateAsync(asset);
            await NotifyAssetActionEmailAsync(asset, "拒绝", reason, operatorName ?? "系统");

            Logger.Info($"管理员 {operatorName} 拒绝了资源审核: {asset.Name} (id={id})，原因: {reason}");
            return new JsonResult(new { code = 0, message = "已拒绝" }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"审核拒绝操作失败: {ex.Message}");
            return new JsonResult(new { code = 500, message = "服务器错误" }, 500);
        }
    }

    #endregion
}

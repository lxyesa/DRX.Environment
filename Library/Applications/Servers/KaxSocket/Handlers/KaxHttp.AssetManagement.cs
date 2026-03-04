using System;
using System.Collections.Generic;
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
    /// 支持前端格式：label, price（元）, originalPrice（元）, durationDays
    /// 也支持向后兼容的参数：discountRate, unit, duration, stock
    /// </summary>
    private static void ApplyPriceInfoToAsset(Model.AssetModel asset, JsonNode body)
    {
        if (body == null) return;

        static int ConvertToDurationDays(string unit, int duration)
        {
            if (duration <= 0) return 0;
            return unit switch
            {
                "year"  => duration * 365,
                "month" => duration * 30,
                "day"   => duration,
                "hour"  => (int)Math.Ceiling(duration / 24.0),
                _        => 0
            };
        }

        // 优先处理 prices 数组（新的价格计划管理方式）
        var pricesNode = body["prices"] as JsonArray;
        if (pricesNode != null && pricesNode.Count > 0)
        {
            // 初始化Prices列表
            if (asset.Prices == null)
                asset.Prices = new Drx.Sdk.Network.DataBase.TableList<AssetPrice>();

            // 记录“当前资产已有”的价格方案 ID，仅允许复用这些 ID，避免外部传入 ID 与其他资产冲突
            var existingPriceIds = asset.Prices
                .Where(p => !string.IsNullOrWhiteSpace(p.Id))
                .Select(p => p.Id)
                .ToHashSet(StringComparer.Ordinal);

            // 本次请求内去重，防止同一 payload 出现重复 id 触发主键冲突
            var reservedIds = new HashSet<string>(StringComparer.Ordinal);

            // 清空现有价格方案，用新的替换
            asset.Prices.Clear();

            foreach (var priceNode in pricesNode)
            {
                if (priceNode == null) continue;

                var priceObj = priceNode as JsonObject;
                if (priceObj == null) continue;

                string label = string.Empty;
                int finalPrice = 0;
                int originalPrice = 0;
                double discountRate = 0;
                string unit = "day";
                int duration = 1;
                int durationDays = 0;
                string id = string.Empty;
                int stock = -1;

                // 方案名称
                if (priceObj["label"] != null)
                    label = priceObj["label"]?.ToString() ?? string.Empty;

                bool hasDurationDays = false;
                bool hasUnit = false;
                bool hasDuration = false;

                // 时长（天数，前端字段）
                if (priceObj["durationDays"] != null && int.TryParse(priceObj["durationDays"]?.ToString(), out var ddVal) && ddVal >= 0)
                {
                    durationDays = ddVal;
                    hasDurationDays = true;
                }

                // 前端直接传入最终价格（元），乘100转为分
                if (priceObj["price"] != null && double.TryParse(priceObj["price"]?.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var priceVal) && priceVal >= 0)
                    finalPrice = (int)Math.Round(priceVal * 100);

                // 原价（元），乘100转为分
                if (priceObj["originalPrice"] != null && double.TryParse(priceObj["originalPrice"]?.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var origPriceVal) && origPriceVal >= 0)
                    originalPrice = (int)Math.Round(origPriceVal * 100);

                // 向后兼容：discountRate（若有）则重新计算最终价格
                if (priceObj["discountRate"] != null && double.TryParse(priceObj["discountRate"]?.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var discountVal))
                {
                    discountRate = Math.Max(0.0, Math.Min(1.0, discountVal));
                    if (originalPrice > 0 && discountRate > 0)
                        finalPrice = (int)Math.Round(originalPrice * (1.0 - discountRate));
                }

                // 如果没有明确设置最终价格，则等于原价
                if (finalPrice == 0 && originalPrice > 0)
                    finalPrice = originalPrice;
                // 如果没有设置原价，则原价等于最终价格
                if (originalPrice == 0 && finalPrice > 0)
                    originalPrice = finalPrice;

                if (priceObj["unit"] != null)
                {
                    unit = priceObj["unit"]?.ToString() ?? "once";
                    hasUnit = true;
                }

                if (priceObj["duration"] != null && int.TryParse(priceObj["duration"]?.ToString(), out var durVal) && durVal > 0)
                {
                    duration = durVal;
                    hasDuration = true;
                }

                // 兼容逻辑：
                // 1) 仅传 durationDays 时，回填为 day + duration
                // 2) 传了 unit/duration 但没传 durationDays 时，自动换算 durationDays
                if (hasDurationDays && (!hasUnit || !hasDuration))
                {
                    if (durationDays <= 0)
                    {
                        unit = "once";
                        duration = 0;
                    }
                    else
                    {
                        unit = "day";
                        duration = durationDays;
                    }
                }
                else if (!hasDurationDays && hasUnit && hasDuration)
                {
                    durationDays = ConvertToDurationDays(unit, duration);
                }
                else if (!hasDurationDays && !hasUnit && !hasDuration)
                {
                    // 完全缺省时，按永久授权处理
                    unit = "once";
                    duration = 0;
                    durationDays = 0;
                }

                if (priceObj["id"] != null)
                    id = priceObj["id"]?.ToString() ?? string.Empty;

                if (priceObj["stock"] != null && int.TryParse(priceObj["stock"]?.ToString(), out var stockVal))
                    stock = stockVal;

                // 创建价格方案
                var assetPrice = new AssetPrice
                {
                    ParentId    = asset.Id,
                    Label       = label,
                    Price       = finalPrice,
                    OriginalPrice = originalPrice,
                    DiscountRate  = discountRate,
                    Unit          = unit,
                    Duration      = duration,
                    DurationDays  = durationDays,
                    Stock         = stock
                };

                // ID 处理策略：
                // 1) 仅复用“当前资产已有”且本次尚未占用的 ID；
                // 2) 外部新传入 ID（包括跨资产复制来的）统一改为新 GUID，避免主键冲突；
                // 3) 同一请求内强制唯一。
                if (!string.IsNullOrEmpty(id) && !id.StartsWith("new_", StringComparison.Ordinal))
                {
                    if (existingPriceIds.Contains(id) && reservedIds.Add(id))
                    {
                        assetPrice.Id = id;
                    }
                    else
                    {
                        var newId = Guid.NewGuid().ToString();
                        while (!reservedIds.Add(newId))
                            newId = Guid.NewGuid().ToString();

                        assetPrice.Id = newId;
                        Logger.Warn($"检测到不可复用或重复的价格方案ID（{id}），已重置为新ID以避免冲突。");
                    }
                }
                else
                {
                    reservedIds.Add(assetPrice.Id);
                }

                asset.Prices.Add(assetPrice);
            }

            return; // 已处理prices数组，不再处理单个价格字段
        }

        // 回退：处理单个价格字段（向后兼容）
        int? price_single = null;
        int? originalPrice_single = null;
        double? discountRate_single = null;

        if (body["price"] != null && double.TryParse(body["price"]?.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var p) && p >= 0)
            price_single = (int)Math.Round(p * 100);

        if (body["originalPrice"] != null && double.TryParse(body["originalPrice"]?.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var op) && op >= 0)
            originalPrice_single = (int)Math.Round(op * 100);

        if (body["discountRate"] != null && double.TryParse(body["discountRate"]?.ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var dr))
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

            // 如果有原价和折扣率，重新计算最终价格以保持一致
            if (firstPrice.OriginalPrice > 0 && firstPrice.DiscountRate > 0)
                firstPrice.Price = (int)Math.Round(firstPrice.OriginalPrice * (1.0 - firstPrice.DiscountRate));
        }
    }

    /// <summary>
    /// 从 JSON 请求体中提取规格信息（文件大小、评分、兼容性、许可证等）并写入 asset.Specs 子表
    /// </summary>
    private static void ApplySpecsInfoToAsset(Model.AssetModel asset, JsonNode body)
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
        var (operatorName, authError) = await Api.RequireAdminNameAsync(request);
        if (authError != null) return authError;
        if (string.IsNullOrEmpty(operatorName)) return new JsonResult(new { message = "管理员认证失败" }, 401);

        if (!ApiBody.TryParse(request, out var body, out var parseError))
            return parseError!;

        try
        {

            var name = body["name"]?.ToString();
            var version = body["version"]?.ToString();

            string description = body["description"]?.ToString()
                ?? body["fullDesc"]?.ToString()
                ?? body["introduction"]?.ToString()
                ?? body["intro"]?.ToString()
                ?? string.Empty;

            // authorId: 优先从请求体获取，否则通过当前用户名查找
            int authorId = 0;
            if (body["authorId"] != null && int.TryParse(body["authorId"]?.ToString(), out var parsedAuthorId) && parsedAuthorId > 0)
                authorId = parsedAuthorId;
            else
            {
                var currentUser = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", operatorName)).FirstOrDefault();
                if (currentUser != null) authorId = currentUser.Id;
            }

            if (string.IsNullOrEmpty(name) || name.Length > 100)
                return new JsonResult(new { message = "资源名称无效（1-100字符）" }, 400);
            if (string.IsNullOrEmpty(version) || version.Length > 50)
                return new JsonResult(new { message = "版本字段无效（1-50字符）" }, 400);
            if (authorId <= 0)
                return new JsonResult(new { message = "作者ID无效" }, 400);
            if (description.Length > 500)
                return new JsonResult(new { message = "描述过长（最多500字符）" }, 400);

            var asset = new Model.AssetModel
            {
                Name = name!,
                Version = version!,
                AuthorId = authorId,
                Description = description
            };

            // 可选字段：分类
            if (body["category"] != null)
            {
                asset.Category = body["category"]?.ToString() ?? string.Empty;
            }

            KaxGlobal.AssetDataBase.Insert(asset);

            // 创建后再写入子表，确保 ParentId 使用真实资产 ID
            ApplyPriceInfoToAsset(asset, body);
            ApplySpecsInfoToAsset(asset, body);
            await KaxGlobal.AssetDataBase.UpdateAsync(asset);

            Logger.Info($"用户 {operatorName} 创建了资源: {name} (v{version})");
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
        var (operatorName, authError) = await Api.RequireAdminNameAsync(request);
        if (authError != null) return authError;

        if (!ApiBody.TryParse(request, out var body, out var parseError))
            return parseError!;

        try
        {

            if (!int.TryParse(body["id"]?.ToString(), out var id) || id <= 0)
                return new JsonResult(new { message = "资源ID无效" }, 400);

            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(id);
            if (asset == null)
                return new JsonResult(new { message = "资源不存在" }, 404);

            var version = body["version"]?.ToString();
            bool hasDescription = body["description"] != null || body["fullDesc"] != null || body["introduction"] != null || body["intro"] != null;
            string? description = null;
            if (hasDescription)
            {
                description = body["description"]?.ToString()
                    ?? body["fullDesc"]?.ToString()
                    ?? body["introduction"]?.ToString()
                    ?? body["intro"]?.ToString()
                    ?? string.Empty;
            }

            if (!string.IsNullOrEmpty(version) && version.Length > 50)
                return new JsonResult(new { message = "版本字段无效（最多50字符）" }, 400);
            if (description != null && description.Length > 500)
                return new JsonResult(new { message = "描述过长（最多500字符）" }, 400);

            if (!string.IsNullOrEmpty(version)) asset.Version = version;

            // 作者在创建时即确定，后续更新不允许修改
            if (body["authorId"] != null || body["author"] != null)
                Logger.Warn($"检测到资源 {id} 的作者变更请求，已忽略（作者固定为创建时绑定值: {asset.AuthorId}）。");

            // 仅当请求显式携带描述字段时才更新，避免“只改其他字段导致简介被清空”
            if (hasDescription && description != null)
                asset.Description = description;

            // 可选更新：资源名称
            if (body["name"] != null)
            {
                var name = body["name"]?.ToString() ?? string.Empty;
                if (name.Length > 200)
                    return new JsonResult(new { message = "名称过长（最多200字符）" }, 400);
                if (!string.IsNullOrEmpty(name)) asset.Name = name;
            }

            // 可选更新：标签
            if (body["tags"] != null)
            {
                var tags = body["tags"]?.ToString() ?? string.Empty;
                if (tags.Length > 500)
                    return new JsonResult(new { message = "标签过长（最多500字符）" }, 400);
                asset.Tags = tags;
            }

            // 可选更新：封面
            if (body["coverImage"] != null)
            {
                asset.CoverImage = body["coverImage"]?.ToString() ?? string.Empty;
            }

            // 可选更新：图标
            if (body["iconImage"] != null)
            {
                asset.IconImage = body["iconImage"]?.ToString() ?? string.Empty;
            }

            // 可选更新：截图列表（分号分隔）
            if (body["screenshots"] != null)
            {
                var screenshots = body["screenshots"]?.ToString() ?? string.Empty;
                if (screenshots.Length > 5000)
                    return new JsonResult(new { message = "截图列表数据过长" }, 400);
                asset.Screenshots = screenshots;
            }

            // 可选更新：徽章列表（JSON 字符串）
            if (body["badges"] != null)
            {
                var badges = body["badges"]?.ToString() ?? string.Empty;
                if (badges.Length > 2000)
                    return new JsonResult(new { message = "徽章数据过长（最多2000字符）" }, 400);
                asset.Badges = badges;
            }

            // 可选更新：特性列表（JSON 字符串）
            if (body["features"] != null)
            {
                var features = body["features"]?.ToString() ?? string.Empty;
                if (features.Length > 2000)
                    return new JsonResult(new { message = "特性数据过长（最多2000字符）" }, 400);
                asset.Features = features;
            }

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

            Logger.Info($"用户 {operatorName} 修改了资源: {asset.Name} (id={id})");
            return new JsonResult(new { message = "资源已更新" });
        }
        catch (Exception ex)
        {
            Logger.Error($"更新资源失败: {ex}");
            return new JsonResult(new { message = "服务器错误" }, 500);
        }
    }

    private static bool TryApplyAssetSingleField(Model.AssetModel asset, string field, JsonNode? valueNode, out string error)
    {
        error = string.Empty;
        field = field.ToLowerInvariant();
        var value = valueNode?.ToString() ?? string.Empty;

        switch (field)
        {
            case "name":
                value = value.Trim();
                if (string.IsNullOrEmpty(value) || value.Length > 200)
                {
                    error = "名称无效（1-200字符）";
                    return false;
                }
                asset.Name = value;
                return true;

            case "version":
                value = value.Trim();
                if (string.IsNullOrEmpty(value) || value.Length > 50)
                {
                    error = "版本字段无效（1-50字符）";
                    return false;
                }
                asset.Version = value;
                return true;

            case "description":
                if (value.Length > 500)
                {
                    error = "描述过长（最多500字符）";
                    return false;
                }
                asset.Description = value;
                return true;

            case "category":
                asset.Category = value;
                return true;

            case "tags":
                if (value.Length > 500)
                {
                    error = "标签过长（最多500字符）";
                    return false;
                }
                asset.Tags = value;
                return true;

            case "coverimage":
                asset.CoverImage = value;
                return true;

            case "iconimage":
                asset.IconImage = value;
                return true;

            case "screenshots":
                if (value.Length > 5000)
                {
                    error = "截图列表数据过长";
                    return false;
                }
                asset.Screenshots = value;
                return true;

            case "badges":
                if (value.Length > 2000)
                {
                    error = "徽章数据过长（最多2000字符）";
                    return false;
                }
                asset.Badges = value;
                return true;

            case "features":
                if (value.Length > 2000)
                {
                    error = "特性数据过长（最多2000字符）";
                    return false;
                }
                asset.Features = value;
                return true;

            case "filesize":
            case "rating":
            case "reviewcount":
            case "compatibility":
            case "downloads":
            case "uploaddate":
            case "license":
            case "downloadurl":
                {
                    var patchBody = new JsonObject
                    {
                        [field] = valueNode?.DeepClone() ?? JsonValue.Create(value)
                    };
                    ApplySpecsInfoToAsset(asset, patchBody);
                    return true;
                }

            case "prices":
                {
                    if (valueNode is not JsonArray arr)
                    {
                        error = "prices 字段必须是数组";
                        return false;
                    }
                    var patchBody = new JsonObject
                    {
                        ["prices"] = arr.DeepClone()
                    };
                    ApplyPriceInfoToAsset(asset, patchBody);
                    return true;
                }

            default:
                error = "不支持的字段";
                return false;
        }
    }

    [HttpHandle("/api/asset/admin/update-field", "POST", RateLimitMaxRequests = 20, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_UpdateAssetField(HttpRequest request)
    {
        var (operatorName, authError) = await Api.RequireAdminNameAsync(request);
        if (authError != null) return authError;

        if (!ApiBody.TryParse(request, out var body, out var parseError))
            return parseError!;

        try
        {
            if (!int.TryParse(body["id"]?.ToString(), out var id) || id <= 0)
                return new JsonResult(new { message = "资源ID无效" }, 400);

            var field = body["field"]?.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(field))
                return new JsonResult(new { message = "field 参数缺失" }, 400);

            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(id);
            if (asset == null)
                return new JsonResult(new { message = "资源不存在" }, 404);

            if (!TryApplyAssetSingleField(asset, field, body["value"], out var fieldError))
                return new JsonResult(new { message = fieldError }, 400);

            var specs = EnsureSpecs(asset);
            specs.LastUpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await KaxGlobal.AssetDataBase.UpdateAsync(asset);

            Logger.Info($"用户 {operatorName} 单字段修改了资源: {asset.Name} (id={id}, field={field})");
            return new JsonResult(new { message = "字段已更新", id, field });
        }
        catch (Exception ex)
        {
            Logger.Error($"单字段更新资源失败: {ex}");
            return new JsonResult(new { message = "服务器错误" }, 500);
        }
    }

    [HttpHandle("/api/asset/admin/inspect", "POST", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_InspectAsset(HttpRequest request)
    {
        var (_, authError) = await Api.RequireAdminNameAsync(request);
        if (authError != null) return authError;

        if (!ApiBody.TryParse(request, out var body, out var parseError))
            return parseError!;

        try
        {

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
                    authorId = asset.AuthorId,
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
        var (operatorName, authError) = await Api.RequireAdminNameAsync(request);
        if (authError != null) return authError;

        if (!ApiBody.TryParse(request, out var body, out var parseError))
            return parseError!;

        try
        {

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

            Logger.Info($"用户 {operatorName} 软删除了资源: {asset.Name} (id={id})");
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
        var (operatorName, authError) = await Api.RequireAdminNameAsync(request);
        if (authError != null) return authError;

        if (!ApiBody.TryParse(request, out var body, out var parseError))
            return parseError!;

        try
        {

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

            Logger.Info($"用户 {operatorName} 恢复了资源: {asset.Name} (id={id})");
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
        var (_, authError) = await Api.RequireAdminNameAsync(request);
        if (authError != null) return authError;

        try
        {
            // 支持查询参数：q (搜索), author, version, page, pageSize, includeDeleted
            var q = request.Query[("q")] ?? string.Empty;
            var authorFilter = request.Query[("author")] ?? string.Empty;
            var versionFilter = request.Query[("version")] ?? string.Empty;
            var includeDeleted = (request.Query[("includeDeleted")] ?? "false").ToLower() == "true";
            var (page, pageSize) = ApiPagination.Parse(request, defaultPageSize: 20);

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
                    || a.AuthorId.ToString().Contains(qlow)
                    || (a.Version ?? string.Empty).ToLowerInvariant().Contains(qlow)).AsQueryable();
            }
            if (!string.IsNullOrEmpty(authorFilter))
            {
                if (int.TryParse(authorFilter, out var authorIdFilter))
                    filtered = filtered.Where(a => a.AuthorId == authorIdFilter).AsQueryable();
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
                    authorId = a.AuthorId,
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

    #region System-Only 资产管理 (仅权限组0)

    /// <summary>
    /// System-Only 资产列表（分页+筛选）
    /// 仅权限组 0 (System) 可访问
    /// 支持筛选：q(关键字)、status(状态)、authorId(开发者ID)、page、pageSize、includeDeleted
    /// </summary>
    [HttpHandle("/api/asset/system/list", "GET", RateLimitMaxRequests = 0, RateLimitWindowSeconds = 0, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Get_SystemAssetList(HttpRequest request)
    {
        var (_, authError) = await Api.RequireSystemNameAsync(request);
        if (authError != null) return authError;

        try
        {
            // 支持查询参数：q (搜索关键字), status (资产状态), authorId (开发者ID), page, pageSize, includeDeleted
            var q = request.Query["q"] ?? string.Empty;
            var statusFilter = request.Query["status"] ?? string.Empty;
            var authorIdFilter = request.Query["authorId"] ?? string.Empty;
            var includeDeleted = (request.Query["includeDeleted"] ?? "false").ToLower() == "true";
            var (page, pageSize) = ApiPagination.Parse(request, defaultPageSize: 20);

            var all = await KaxGlobal.AssetDataBase.SelectAllAsync();

            var filtered = all.AsQueryable();

            // 软删除筛选
            if (!includeDeleted)
            {
                filtered = filtered.Where(a => !a.IsDeleted);
            }

            // 关键字搜索（名称、版本、作者ID）
            if (!string.IsNullOrEmpty(q))
            {
                var qlow = q.ToLowerInvariant();
                filtered = filtered.Where(a =>
                    (a.Name ?? string.Empty).ToLowerInvariant().Contains(qlow)
                    || a.AuthorId.ToString().Contains(qlow)
                    || (a.Version ?? string.Empty).ToLowerInvariant().Contains(qlow)
                    || (a.Description ?? string.Empty).ToLowerInvariant().Contains(qlow));
            }

            // 状态筛选
            if (!string.IsNullOrEmpty(statusFilter) && int.TryParse(statusFilter, out var statusValue))
            {
                filtered = filtered.Where(a => (int)a.Status == statusValue);
            }

            // 开发者ID筛选
            if (!string.IsNullOrEmpty(authorIdFilter) && int.TryParse(authorIdFilter, out var authorId))
            {
                filtered = filtered.Where(a => a.AuthorId == authorId);
            }

            var total = filtered.Count();
            var items = filtered
                .OrderByDescending(a => a.Specs != null ? a.Specs.LastUpdatedAt : 0)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    id = a.Id,
                    name = a.Name,
                    version = a.Version,
                    authorId = a.AuthorId,
                    description = a.Description,
                    status = (int)a.Status,
                    statusName = a.Status.ToString(),
                    category = a.Category,
                    lastUpdatedAt = a.Specs != null ? a.Specs.LastUpdatedAt : 0,
                    isDeleted = a.IsDeleted
                })
                .ToList<object>();

            return new JsonResult(new { data = items, page, pageSize, total });
        }
        catch (Exception ex)
        {
            Logger.Error($"[System] 读取资源列表失败: {ex.Message}");
            return new JsonResult(new { message = "无法读取资源列表" }, 500);
        }
    }

    /// <summary>
    /// System-Only 资产详情
    /// 仅权限组 0 (System) 可访问
    /// </summary>
    [HttpHandle("/api/asset/system/{id}", "GET", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Get_SystemAssetDetail(HttpRequest request)
    {
        var (_, authError) = await Api.RequireSystemNameAsync(request);
        if (authError != null) return authError;

        try
        {
            // 从路由模板提取 id
            var idStr = request.PathParameters.GetValueOrDefault("id") ?? string.Empty;
            if (!int.TryParse(idStr, out var id) || id <= 0)
                return new JsonResult(new { message = "资源ID无效" }, 400);

            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(id);
            if (asset == null)
                return new JsonResult(new { message = "资源不存在" }, 404);

            // 将价格方案列表转换为可序列化的格式，并从第一个方案派生兼容字段
            var pricesList = asset.Prices?.ToList();
            var primaryPrice = pricesList?.FirstOrDefault();
            var specs = asset.Specs;

            return new JsonResult(new
            {
                data = new
                {
                    id = asset.Id,
                    name = asset.Name,
                    version = asset.Version,
                    authorId = asset.AuthorId,
                    description = asset.Description,
                    category = asset.Category,
                    tags = asset.Tags,
                    badges = asset.Badges,
                    features = asset.Features,
                    coverImage = asset.CoverImage,
                    iconImage = asset.IconImage,
                    screenshots = asset.Screenshots,
                    status = (int)asset.Status,
                    statusName = asset.Status.ToString(),
                    rejectReason = asset.RejectReason,
                    lastSubmittedAt = asset.LastSubmittedAt,
                    // 价格信息（兼容字段）
                    price = primaryPrice?.Price ?? 0,
                    originalPrice = primaryPrice?.OriginalPrice ?? 0,
                    discountRate = primaryPrice?.DiscountRate ?? 0.0,
                    // 规格信息
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
            Logger.Error($"[System] 查询资源详情失败: {ex.Message}");
            return new JsonResult(new { message = "服务器错误" }, 500);
        }
    }

    /// <summary>
    /// System-Only 资产字段黑名单（禁止通过此接口修改）
    /// </summary>
    private static readonly HashSet<string> SystemFieldBlacklist = new(StringComparer.OrdinalIgnoreCase)
    {
        "id",
        "assetid",
        "authorid",
        "developerid"  // 设计文档使用 DeveloperId，但模型字段为 AuthorId，两者都禁止
    };

    /// <summary>
    /// System-Only 单字段更新
    /// 仅权限组 0 (System) 可访问
    /// 禁止修改 DeveloperId (AuthorId) 和 AssetId (Id)
    /// </summary>
    [HttpHandle("/api/asset/system/update-field", "POST", RateLimitMaxRequests = 20, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_SystemUpdateAssetField(HttpRequest request)
    {
        var (operatorUser, authError) = await Api.RequireSystemAsync(request);
        if (authError != null) return authError;

        var operatorName = operatorUser!.UserName;
        var operatorId = operatorUser.Id;

        if (!ApiBody.TryParse(request, out var body, out var parseError))
            return parseError!;

        var requestId = Guid.NewGuid().ToString();

        try
        {
            if (!int.TryParse(body["id"]?.ToString(), out var id) || id <= 0)
                return new JsonResult(new { message = "资源ID无效" }, 400);

            var field = body["field"]?.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(field))
                return new JsonResult(new { message = "field 参数缺失" }, 400);

            // 字段黑名单检查：禁止修改 AuthorId/DeveloperId 和 Id/AssetId
            if (SystemFieldBlacklist.Contains(field))
                return new JsonResult(new { message = $"字段 '{field}' 禁止修改", code = "FIELD_FORBIDDEN" }, 403);

            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(id);
            if (asset == null)
                return new JsonResult(new { message = "资源不存在" }, 404);

            // 记录旧值（用于审计）
            var oldValue = GetAssetFieldValue(asset, field);

            if (!TryApplyAssetSingleField(asset, field, body["value"], out var fieldError))
            {
                await WriteAuditAsync(id, operatorId, operatorName, "update-field", false,
                    fieldChangesJson: $"{{\"field\":\"{field}\",\"oldValue\":{System.Text.Json.JsonSerializer.Serialize(oldValue)},\"newValue\":{System.Text.Json.JsonSerializer.Serialize(body["value"]?.ToString())}}}",
                    errorCode: "FIELD_APPLY_ERROR",
                    errorMessage: fieldError ?? string.Empty,
                    requestId: requestId);
                return new JsonResult(new { message = fieldError }, 400);
            }

            // 记录新值（用于审计）
            var newValue = GetAssetFieldValue(asset, field);

            var specs = EnsureSpecs(asset);
            specs.LastUpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await KaxGlobal.AssetDataBase.UpdateAsync(asset);

            // 写入字段变更审计
            await WriteAuditAsync(id, operatorId, operatorName, "update-field", true,
                fieldChangesJson: $"{{\"field\":\"{field}\",\"oldValue\":{System.Text.Json.JsonSerializer.Serialize(oldValue)},\"newValue\":{System.Text.Json.JsonSerializer.Serialize(newValue)}}}",
                requestId: requestId);

            Logger.Info($"[System] 用户 {operatorName} 单字段修改了资源: {asset.Name} (id={id}, field={field})");
            return new JsonResult(new { message = "字段已更新", id, field });
        }
        catch (Exception ex)
        {
            Logger.Error($"[System] 单字段更新资源失败: {ex}");
            _ = WriteAuditAsync(0, operatorId, operatorName, "update-field", false,
                errorCode: "INTERNAL_ERROR",
                errorMessage: ex.Message,
                requestId: requestId);
            return new JsonResult(new { message = "服务器错误" }, 500);
        }
    }

    /// <summary>
    /// 获取资产指定字段的当前值（用于审计记录旧值/新值）
    /// </summary>
    private static string GetAssetFieldValue(Model.AssetModel asset, string field)
    {
        if (asset == null || string.IsNullOrEmpty(field)) return string.Empty;
        return field.ToLowerInvariant() switch
        {
            "name"          => asset.Name ?? string.Empty,
            "version"       => asset.Version ?? string.Empty,
            "description"   => asset.Description ?? string.Empty,
            "category"      => asset.Category ?? string.Empty,
            "tags"          => asset.Tags ?? string.Empty,
            "badges"        => asset.Badges ?? string.Empty,
            "features"      => asset.Features ?? string.Empty,
            "coverimage"    => asset.CoverImage ?? string.Empty,
            "iconimage"     => asset.IconImage ?? string.Empty,
            "screenshots"   => asset.Screenshots ?? string.Empty,
            "status"        => ((int)asset.Status).ToString(),
            "rejectreason"  => asset.RejectReason ?? string.Empty,
            "filesize"      => asset.Specs?.FileSize.ToString() ?? "0",
            "downloadurl"   => asset.Specs?.DownloadUrl ?? string.Empty,
            "license"       => asset.Specs?.License ?? string.Empty,
            "compatibility" => asset.Specs?.Compatibility ?? string.Empty,
            "prices"        => "[complex]",
            _               => string.Empty
        };
    }


    /// <summary>
    /// 写入资产审计日志（fire-and-forget，内部异常不影响主流程）
    /// </summary>
    private static async Task WriteAuditAsync(
        int assetId,
        int operatorUserId,
        string operatorUserName,
        string actionType,
        bool success,
        int beforeStatus = -1,
        int afterStatus = -1,
        string reason = "",
        string fieldChangesJson = "",
        string errorCode = "",
        string errorMessage = "",
        string requestId = "")
    {
        try
        {
            var log = new AssetAuditLog
            {
                AssetId = assetId,
                OperatorUserId = operatorUserId,
                OperatorUserName = operatorUserName ?? string.Empty,
                ActionType = actionType ?? string.Empty,
                Reason = reason ?? string.Empty,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                BeforeStatus = beforeStatus,
                AfterStatus = afterStatus,
                FieldChangesJson = fieldChangesJson ?? string.Empty,
                Success = success,
                ErrorCode = errorCode ?? string.Empty,
                ErrorMessage = errorMessage ?? string.Empty,
                RequestId = string.IsNullOrEmpty(requestId) ? Guid.NewGuid().ToString() : requestId
            };
            await KaxGlobal.AssetAuditDatabase.InsertAsync(log);
        }
        catch (Exception auditEx)
        {
            // 审计写入失败不影响主流程，仅记录日志
            Logger.Warn($"[Audit] 审计日志写入失败(assetId={assetId}, action={actionType}): {auditEx.Message}");
        }
    }

    private readonly struct StateTransitionResult
    {
        public bool IsValid { get; init; }
        public string ErrorMessage { get; init; }
        public string ErrorCode { get; init; }

        public static StateTransitionResult Valid() => new() { IsValid = true };
        public static StateTransitionResult Invalid(string code, string message) => new() { IsValid = false, ErrorCode = code, ErrorMessage = message };
    }

    /// <summary>
    /// 校验状态流转是否合法
    /// 
    /// 状态流转规则（设计文档定义）：
    /// - 退回（Return）：0/2/3/4 -> 1（受规则约束，需原因）
    /// - 重审（ForceReview）：1/3/4 -> 0（强制更新=重审）
    /// - 下架（OffShelf）：3 -> 4
    /// - 恢复上架（Relist）：4 -> 3（可选能力，预留）
    /// </summary>
    private static StateTransitionResult ValidateStateTransition(AssetStatus currentStatus, string action)
    {
        // 根据动作类型定义合法的前置状态
        var validTransitions = action.ToLowerInvariant() switch
        {
            "return" => new[] { AssetStatus.PendingReview, AssetStatus.ApprovedPendingPublish, AssetStatus.Active, AssetStatus.OffShelf },
            "off-shelf" => new[] { AssetStatus.Active },
            "force-review" => new[] { AssetStatus.Rejected, AssetStatus.Active, AssetStatus.OffShelf },
            "relist" => new[] { AssetStatus.OffShelf },  // 预留：恢复上架
            _ => Array.Empty<AssetStatus>()
        };

        if (validTransitions.Length == 0)
            return StateTransitionResult.Invalid("INVALID_ACTION", $"未知的动作类型: {action}");

        if (!validTransitions.Contains(currentStatus))
        {
            var allowedStates = string.Join(", ", validTransitions.Select(s => $"{(int)s}({s})"));
            return StateTransitionResult.Invalid(
                "INVALID_STATE_TRANSITION",
                $"当前状态 {(int)currentStatus}({currentStatus}) 不允许执行 {action} 操作。允许的前置状态: {allowedStates}"
            );
        }

        return StateTransitionResult.Valid();
    }

    /// <summary>
    /// System-Only 退回资产
    /// 仅权限组 0 (System) 可访问
    /// 
    /// 状态流转：PendingReview(0)/ApprovedPendingPublish(2)/Active(3)/OffShelf(4) -> Rejected(1)
    /// 必填：assetId, reason
    /// </summary>
    [HttpHandle("/api/asset/system/return", "POST", RateLimitMaxRequests = 20, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_SystemReturnAsset(HttpRequest request)
    {
        var (operatorUser, authError) = await Api.RequireSystemAsync(request);
        if (authError != null) return authError;

        var operatorName = operatorUser!.UserName;
        var operatorId = operatorUser.Id;

        if (!ApiBody.TryParse(request, out var body, out var parseError))
            return parseError!;

        var requestId = Guid.NewGuid().ToString();

        try
        {
            // 参数校验
            if (!int.TryParse(body["assetId"]?.ToString() ?? body["id"]?.ToString(), out var assetId) || assetId <= 0)
                return new JsonResult(new { message = "资源ID无效", code = "INVALID_ASSET_ID" }, 400);

            var reason = body["reason"]?.ToString()?.Trim();
            if (string.IsNullOrEmpty(reason))
                return new JsonResult(new { message = "退回原因不能为空", code = "REASON_REQUIRED" }, 400);

            if (reason.Length > 500)
                return new JsonResult(new { message = "退回原因过长（最多500字符）", code = "REASON_TOO_LONG" }, 400);

            // 获取资产
            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(assetId);
            if (asset == null)
                return new JsonResult(new { message = "资源不存在", code = "ASSET_NOT_FOUND" }, 404);

            // 状态流转校验
            var transition = ValidateStateTransition(asset.Status, "return");
            if (!transition.IsValid)
            {
                await WriteAuditAsync(assetId, operatorId, operatorName, "return", false,
                    beforeStatus: (int)asset.Status,
                    reason: reason,
                    errorCode: transition.ErrorCode,
                    errorMessage: transition.ErrorMessage,
                    requestId: requestId);
                return new JsonResult(new { message = transition.ErrorMessage, code = transition.ErrorCode }, 400);
            }

            // 记录原状态用于日志
            var previousStatus = asset.Status;

            // 执行状态变更
            asset.Status = AssetStatus.Rejected;
            asset.RejectReason = reason;
            var specs = EnsureSpecs(asset);
            specs.LastUpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await KaxGlobal.AssetDataBase.UpdateAsync(asset);

            // 写入成功审计
            await WriteAuditAsync(assetId, operatorId, operatorName, "return", true,
                beforeStatus: (int)previousStatus,
                afterStatus: (int)AssetStatus.Rejected,
                reason: reason,
                requestId: requestId);

            Logger.Info($"[System] 用户 {operatorName} 退回了资源: {asset.Name} (id={assetId})，状态: {previousStatus} -> Rejected，原因: {reason}");
            return new JsonResult(new
            {
                code = 0,
                message = "资源已退回",
                data = new
                {
                    assetId,
                    previousStatus = (int)previousStatus,
                    currentStatus = (int)AssetStatus.Rejected,
                    reason
                }
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"[System] 退回资源失败: {ex}");
            // 尽力写入失败审计（assetId 可能为 0 如果解析失败，仍记录以便排查）
            _ = WriteAuditAsync(0, operatorId, operatorName, "return", false,
                errorCode: "INTERNAL_ERROR",
                errorMessage: ex.Message,
                requestId: requestId);
            return new JsonResult(new { message = "服务器错误", code = "INTERNAL_ERROR" }, 500);
        }
    }

    /// <summary>
    /// System-Only 下架资产
    /// 仅权限组 0 (System) 可访问
    /// 
    /// 状态流转：Active(3) -> OffShelf(4)
    /// 注意：下架是独立状态，不等同于软删除（IsDeleted）
    /// </summary>
    [HttpHandle("/api/asset/system/off-shelf", "POST", RateLimitMaxRequests = 20, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_SystemOffShelfAsset(HttpRequest request)
    {
        var (operatorUser, authError) = await Api.RequireSystemAsync(request);
        if (authError != null) return authError;

        var operatorName = operatorUser!.UserName;
        var operatorId = operatorUser.Id;

        if (!ApiBody.TryParse(request, out var body, out var parseError))
            return parseError!;

        var requestId = Guid.NewGuid().ToString();

        try
        {
            // 参数校验
            if (!int.TryParse(body["assetId"]?.ToString() ?? body["id"]?.ToString(), out var assetId) || assetId <= 0)
                return new JsonResult(new { message = "资源ID无效", code = "INVALID_ASSET_ID" }, 400);

            var reason = body["reason"]?.ToString()?.Trim() ?? string.Empty;
            // 下架原因可选，但建议填写
            if (reason.Length > 500)
                return new JsonResult(new { message = "下架原因过长（最多500字符）", code = "REASON_TOO_LONG" }, 400);

            // 获取资产
            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(assetId);
            if (asset == null)
                return new JsonResult(new { message = "资源不存在", code = "ASSET_NOT_FOUND" }, 404);

            // 状态流转校验
            var transition = ValidateStateTransition(asset.Status, "off-shelf");
            if (!transition.IsValid)
            {
                await WriteAuditAsync(assetId, operatorId, operatorName, "off-shelf", false,
                    beforeStatus: (int)asset.Status,
                    reason: reason,
                    errorCode: transition.ErrorCode,
                    errorMessage: transition.ErrorMessage,
                    requestId: requestId);
                return new JsonResult(new { message = transition.ErrorMessage, code = transition.ErrorCode }, 400);
            }

            // 记录原状态
            var previousStatus = asset.Status;

            // 执行下架：注意不修改 IsDeleted（下架独立于软删除）
            asset.Status = AssetStatus.OffShelf;
            // 将下架原因记录到 RejectReason 字段（复用）
            if (!string.IsNullOrEmpty(reason))
                asset.RejectReason = $"[下架] {reason}";
            var specs = EnsureSpecs(asset);
            specs.LastUpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await KaxGlobal.AssetDataBase.UpdateAsync(asset);

            // 写入成功审计
            await WriteAuditAsync(assetId, operatorId, operatorName, "off-shelf", true,
                beforeStatus: (int)previousStatus,
                afterStatus: (int)AssetStatus.OffShelf,
                reason: reason,
                requestId: requestId);

            Logger.Info($"[System] 用户 {operatorName} 下架了资源: {asset.Name} (id={assetId})，状态: {previousStatus} -> OffShelf" + (string.IsNullOrEmpty(reason) ? "" : $"，原因: {reason}"));
            return new JsonResult(new
            {
                code = 0,
                message = "资源已下架",
                data = new
                {
                    assetId,
                    previousStatus = (int)previousStatus,
                    currentStatus = (int)AssetStatus.OffShelf,
                    reason = string.IsNullOrEmpty(reason) ? null : reason
                }
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"[System] 下架资源失败: {ex}");
            _ = WriteAuditAsync(0, operatorId, operatorName, "off-shelf", false,
                errorCode: "INTERNAL_ERROR",
                errorMessage: ex.Message,
                requestId: requestId);
            return new JsonResult(new { message = "服务器错误", code = "INTERNAL_ERROR" }, 500);
        }
    }

    /// <summary>
    /// System-Only 强制重审资产（强制更新）
    /// 仅权限组 0 (System) 可访问
    /// 
    /// 状态流转：Rejected(1)/Active(3)/OffShelf(4) -> PendingReview(0)
    /// 本期"强制更新"定义为"强制资产重审"
    /// </summary>
    [HttpHandle("/api/asset/system/review/force", "POST", RateLimitMaxRequests = 20, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_SystemForceReviewAsset(HttpRequest request)
    {
        var (operatorUser, authError) = await Api.RequireSystemAsync(request);
        if (authError != null) return authError;

        var operatorName = operatorUser!.UserName;
        var operatorId = operatorUser.Id;

        if (!ApiBody.TryParse(request, out var body, out var parseError))
            return parseError!;

        var requestId = Guid.NewGuid().ToString();

        try
        {
            // 参数校验
            if (!int.TryParse(body["assetId"]?.ToString() ?? body["id"]?.ToString(), out var assetId) || assetId <= 0)
                return new JsonResult(new { message = "资源ID无效", code = "INVALID_ASSET_ID" }, 400);

            var reason = body["reason"]?.ToString()?.Trim() ?? string.Empty;
            if (reason.Length > 500)
                return new JsonResult(new { message = "原因过长（最多500字符）", code = "REASON_TOO_LONG" }, 400);

            // force 参数（可选，默认 true）
            var forceStr = body["force"]?.ToString()?.ToLowerInvariant();
            var force = string.IsNullOrEmpty(forceStr) || forceStr == "true" || forceStr == "1";

            if (!force)
                return new JsonResult(new { message = "force 参数必须为 true", code = "FORCE_REQUIRED" }, 400);

            // 获取资产
            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(assetId);
            if (asset == null)
                return new JsonResult(new { message = "资源不存在", code = "ASSET_NOT_FOUND" }, 404);

            // 状态流转校验
            var transition = ValidateStateTransition(asset.Status, "force-review");
            if (!transition.IsValid)
            {
                await WriteAuditAsync(assetId, operatorId, operatorName, "force-review", false,
                    beforeStatus: (int)asset.Status,
                    reason: reason,
                    errorCode: transition.ErrorCode,
                    errorMessage: transition.ErrorMessage,
                    requestId: requestId);
                return new JsonResult(new { message = transition.ErrorMessage, code = transition.ErrorCode }, 400);
            }

            // 记录原状态
            var previousStatus = asset.Status;

            // 执行强制重审：将资产置入审核流程
            asset.Status = AssetStatus.PendingReview;
            asset.LastSubmittedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            asset.RejectReason = string.Empty;  // 清除之前的拒绝原因
            var specs = EnsureSpecs(asset);
            specs.LastUpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await KaxGlobal.AssetDataBase.UpdateAsync(asset);

            // 写入成功审计
            await WriteAuditAsync(assetId, operatorId, operatorName, "force-review", true,
                beforeStatus: (int)previousStatus,
                afterStatus: (int)AssetStatus.PendingReview,
                reason: reason,
                requestId: requestId);

            Logger.Info($"[System] 用户 {operatorName} 强制重审了资源: {asset.Name} (id={assetId})，状态: {previousStatus} -> PendingReview" + (string.IsNullOrEmpty(reason) ? "" : $"，原因: {reason}"));
            return new JsonResult(new
            {
                code = 0,
                message = "资源已进入重审流程",
                data = new
                {
                    assetId,
                    previousStatus = (int)previousStatus,
                    currentStatus = (int)AssetStatus.PendingReview,
                    reason = string.IsNullOrEmpty(reason) ? null : reason
                }
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"[System] 强制重审资源失败: {ex}");
            _ = WriteAuditAsync(0, operatorId, operatorName, "force-review", false,
                errorCode: "INTERNAL_ERROR",
                errorMessage: ex.Message,
                requestId: requestId);
            return new JsonResult(new { message = "服务器错误", code = "INTERNAL_ERROR" }, 500);
        }
    }

    /// <summary>
    /// System-Only 恢复上架资产（预留能力）
    /// 仅权限组 0 (System) 可访问
    /// 
    /// 状态流转：OffShelf(4) -> Active(3)
    /// </summary>
    [HttpHandle("/api/asset/system/relist", "POST", RateLimitMaxRequests = 20, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_SystemRelistAsset(HttpRequest request)
    {
        var (operatorUser, authError) = await Api.RequireSystemAsync(request);
        if (authError != null) return authError;

        var operatorName = operatorUser!.UserName;
        var operatorId = operatorUser.Id;

        if (!ApiBody.TryParse(request, out var body, out var parseError))
            return parseError!;

        var requestId = Guid.NewGuid().ToString();

        try
        {
            // 参数校验
            if (!int.TryParse(body["assetId"]?.ToString() ?? body["id"]?.ToString(), out var assetId) || assetId <= 0)
                return new JsonResult(new { message = "资源ID无效", code = "INVALID_ASSET_ID" }, 400);

            var reason = body["reason"]?.ToString()?.Trim() ?? string.Empty;
            if (reason.Length > 500)
                return new JsonResult(new { message = "原因过长（最多500字符）", code = "REASON_TOO_LONG" }, 400);

            // 获取资产
            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(assetId);
            if (asset == null)
                return new JsonResult(new { message = "资源不存在", code = "ASSET_NOT_FOUND" }, 404);

            // 状态流转校验
            var transition = ValidateStateTransition(asset.Status, "relist");
            if (!transition.IsValid)
            {
                await WriteAuditAsync(assetId, operatorId, operatorName, "relist", false,
                    beforeStatus: (int)asset.Status,
                    reason: reason,
                    errorCode: transition.ErrorCode,
                    errorMessage: transition.ErrorMessage,
                    requestId: requestId);
                return new JsonResult(new { message = transition.ErrorMessage, code = transition.ErrorCode }, 400);
            }

            // 记录原状态
            var previousStatus = asset.Status;

            // 执行恢复上架
            asset.Status = AssetStatus.Active;
            asset.RejectReason = string.Empty;
            var specs = EnsureSpecs(asset);
            specs.LastUpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await KaxGlobal.AssetDataBase.UpdateAsync(asset);

            // 写入成功审计
            await WriteAuditAsync(assetId, operatorId, operatorName, "relist", true,
                beforeStatus: (int)previousStatus,
                afterStatus: (int)AssetStatus.Active,
                reason: reason,
                requestId: requestId);

            Logger.Info($"[System] 用户 {operatorName} 恢复上架了资源: {asset.Name} (id={assetId})，状态: {previousStatus} -> Active" + (string.IsNullOrEmpty(reason) ? "" : $"，原因: {reason}"));
            return new JsonResult(new
            {
                code = 0,
                message = "资源已恢复上架",
                data = new
                {
                    assetId,
                    previousStatus = (int)previousStatus,
                    currentStatus = (int)AssetStatus.Active,
                    reason = string.IsNullOrEmpty(reason) ? null : reason
                }
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"[System] 恢复上架资源失败: {ex}");
            _ = WriteAuditAsync(0, operatorId, operatorName, "relist", false,
                errorCode: "INTERNAL_ERROR",
                errorMessage: ex.Message,
                requestId: requestId);
            return new JsonResult(new { message = "服务器错误", code = "INTERNAL_ERROR" }, 500);
        }
    }

    #endregion

    #region System-Only 审计查询接口

    /// <summary>
    /// System-Only 查询资产审计记录
    /// 仅权限组 0 (System) 可访问
    /// 
    /// 返回指定资产的所有审计日志，按时间倒序排列，支持分页。
    /// </summary>
    [HttpHandle("/api/asset/system/audit/{assetId}", "GET", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Get_SystemAssetAuditLog(HttpRequest request)
    {
        var (_, authError) = await Api.RequireSystemNameAsync(request);
        if (authError != null) return authError;

        try
        {
            var assetIdStr = request.PathParameters.GetValueOrDefault("assetId") ?? string.Empty;
            if (!int.TryParse(assetIdStr, out var assetId) || assetId <= 0)
                return new JsonResult(new { message = "资源ID无效", code = "INVALID_ASSET_ID" }, 400);

            // 分页参数（可选）
            var (page, pageSize) = ApiPagination.Parse(request, defaultPageSize: 50);

            // action 类型过滤（可选）
            var actionFilter = request.Query["action"] ?? string.Empty;

            // 查询该资产的所有审计记录
            var all = await KaxGlobal.AssetAuditDatabase.SelectWhereAsync("AssetId", assetId);

            var filtered = all.AsQueryable();
            if (!string.IsNullOrEmpty(actionFilter))
            {
                var af = actionFilter.ToLowerInvariant();
                filtered = filtered.Where(l => l.ActionType.ToLowerInvariant() == af);
            }

            var total = filtered.Count();
            var items = filtered
                .OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new
                {
                    id = l.Id,
                    assetId = l.AssetId,
                    operatorUserId = l.OperatorUserId,
                    operatorUserName = l.OperatorUserName,
                    actionType = l.ActionType,
                    reason = l.Reason,
                    createdAt = l.CreatedAt,
                    beforeStatus = l.BeforeStatus,
                    afterStatus = l.AfterStatus,
                    fieldChangesJson = l.FieldChangesJson,
                    success = l.Success,
                    errorCode = l.ErrorCode,
                    errorMessage = l.ErrorMessage,
                    requestId = l.RequestId
                })
                .ToList<object>();

            return new JsonResult(new { data = items, page, pageSize, total, assetId });
        }
        catch (Exception ex)
        {
            Logger.Error($"[System] 查询资产审计记录失败: {ex.Message}");
            return new JsonResult(new { message = "服务器错误", code = "INTERNAL_ERROR" }, 500);
        }
    }

    #endregion

}

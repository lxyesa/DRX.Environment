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

namespace KaxSocket.Handlers;

/// <summary>
/// 购物与套餐管理模块 - 处理购买、套餐更改、取消订阅等功能
/// </summary>
public partial class KaxHttp
{
    #region 购物与套餐管理 (Shopping & Plan Management)

    /// <summary>
    /// 购买资产API - 验证token和金币，将资产添加到用户的激活资产中
    /// 支持重复购买：已拥有的资产再次购买时，新时长将与现有时长累加；已过期则重新激活
    /// 请求体格式：{ assetId: int, priceId: string, durationOverride?: long }
    /// durationOverride（可选）：购买期限（毫秒），0表示永久激活
    /// </summary>
    [HttpHandle("/api/shop/purchase", "POST", RateLimitMaxRequests = 20, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_PurchaseAsset(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token ?? string.Empty);
        if (principal == null) return new JsonResult(new { code = 401, message = "未授权" }, 401);

        var userName = principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName)) return new JsonResult(new { code = 400, message = "用户名无效" }, 400);

        if (await KaxGlobal.IsUserBanned(userName)) 
            return new JsonResult(new { code = 403, message = "账号被封禁，无法购买" }, 403);

        if (string.IsNullOrEmpty(request.Body)) 
            return new JsonResult(new { code = 400, message = "请求体不能为空" }, 400);

        var body = JsonNode.Parse(request.Body);
        if (body == null) 
            return new JsonResult(new { code = 400, message = "无法解析请求体" }, 400);

        if (!int.TryParse(body["assetId"]?.ToString(), out var assetId) || assetId <= 0)
            return new JsonResult(new { code = 400, message = "assetId 无效" }, 400);

        var priceId = body["priceId"]?.ToString();
        if (string.IsNullOrEmpty(priceId))
            return new JsonResult(new { code = 400, message = "priceId 参数缺失" }, 400);

        long durationOverride = 0;
        if (body["durationOverride"] != null)
        {
            if (!long.TryParse(body["durationOverride"]?.ToString(), out durationOverride) || durationOverride < 0)
                return new JsonResult(new { code = 400, message = "durationOverride 参数无效" }, 400);
        }

        try
        {
            var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
            if (user == null) 
                return new JsonResult(new { code = 404, message = "用户不存在" }, 404);

            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(assetId);
            if (asset == null) 
                return new JsonResult(new { code = 404, message = "资产不存在" }, 404);

            if (asset.IsDeleted)
                return new JsonResult(new { code = 404, message = "资产已被删除" }, 404);

            var assetPrice = asset.Prices?.FirstOrDefault(p => p.Id == priceId);
            if (assetPrice == null)
                return new JsonResult(new { code = 404, message = "价格方案不存在" }, 404);

            int discountedPrice = (int)Math.Round(assetPrice.Price * (1.0 - assetPrice.DiscountRate));
            int minimumGold = Math.Max(0, discountedPrice);
            if (user.Gold < minimumGold)
                return new JsonResult(new { code = 403, message = $"金币不足，需要至少 {minimumGold} 点金币才能购买此资产" }, 403);

            long purchaseDurationMs = 0;
            bool isPermanentPurchase = false;
            if (durationOverride > 0)
            {
                purchaseDurationMs = durationOverride;
            }
            else if (assetPrice.Unit == "once" || assetPrice.Duration <= 0)
            {
                isPermanentPurchase = true;
            }
            else
            {
                purchaseDurationMs = CalculateDurationInMilliseconds(assetPrice.Unit, assetPrice.Duration);
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var existingAsset = user.ActiveAssets?.FirstOrDefault(a => a.AssetId == assetId);
            bool isRenewal = existingAsset != null;
            string purchaseType;

            if (user.ActiveAssets == null)
                user.ActiveAssets = new Drx.Sdk.Network.DataBase.TableList<ActiveAssets>();

            if (existingAsset != null)
            {
                if (existingAsset.ExpiresAt == 0)
                    return new JsonResult(new { code = 409, message = "您已永久拥有此资产，无需再次购买" }, 409);

                if (isPermanentPurchase)
                {
                    existingAsset.ExpiresAt = 0;
                    user.ActiveAssets.Update(existingAsset);
                    purchaseType = "升级为永久";
                }
                else if (existingAsset.ExpiresAt < now)
                {
                    existingAsset.ActivatedAt = now;
                    existingAsset.ExpiresAt = now + purchaseDurationMs;
                    user.ActiveAssets.Update(existingAsset);
                    purchaseType = "重新激活";
                }
                else
                {
                    existingAsset.ExpiresAt += purchaseDurationMs;
                    user.ActiveAssets.Update(existingAsset);
                    purchaseType = "续期累加";
                }
            }
            else
            {
                long expiresAt = isPermanentPurchase ? 0 : (now + purchaseDurationMs);
                var activeAsset = new ActiveAssets
                {
                    ParentId = user.Id,
                    AssetId = assetId,
                    ActivatedAt = now,
                    ExpiresAt = expiresAt
                };
                user.ActiveAssets.Add(activeAsset);
                purchaseType = "首次购买";
            }

            user.Gold = Math.Max(0, user.Gold - minimumGold);
            Logger.Info($"用户 {userName} 扣减金币 {minimumGold}，剩余金币 {user.Gold}");

            asset.PurchaseCount++;
            await KaxGlobal.AssetDataBase.UpdateAsync(asset);

            await KaxGlobal.UserDatabase.UpdateAsync(user);

            var finalAsset = user.ActiveAssets!.First(a => a.AssetId == assetId);
            var finalExpires = finalAsset.ExpiresAt;

            Logger.Info($"用户 {userName} {purchaseType}资产 {asset.Name} (ID:{assetId})，过期时间：{(finalExpires == 0 ? "永久" : DateTimeOffset.FromUnixTimeMilliseconds(finalExpires).ToString("yyyy-MM-dd HH:mm:ss"))}");

            return new JsonResult(new 
            { 
                code = 0, 
                message = isRenewal ? $"{purchaseType}成功" : "购买成功",
                data = new 
                {
                    assetId = assetId,
                    activatedAt = finalAsset.ActivatedAt,
                    expiresAt = finalExpires,
                    permanent = finalExpires == 0,
                    purchaseType = purchaseType
                }
            }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"购买资产时出错: {ex.Message}");
            return new JsonResult(new { code = 500, message = "服务器错误" }, 500);
        }
    }

    /// <summary>
    /// 获取指定资产的可用套餐列表
    /// 用户可根据当前激活的资产ID获取其他可用套餐选项
    /// </summary>
    [HttpHandle("/api/asset/{assetId}/plans", "GET", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Get_AssetPlans(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token ?? string.Empty);
        if (principal == null) return new JsonResult(new { code = 401, message = "未授权" }, 401);

        var userName = principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName)) return new JsonResult(new { code = 400, message = "用户名无效" }, 400);

        if (await KaxGlobal.IsUserBanned(userName)) return new JsonResult(new { code = 403, message = "用户已被封禁" }, 403);

        // 提取路径参数中的 assetId
        if (!request.PathParameters.TryGetValue("assetId", out var assetIdStr) || !int.TryParse(assetIdStr, out var assetId) || assetId <= 0)
            return new JsonResult(new { code = 400, message = "assetId 参数无效" }, 400);

        try
        {
            var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
            if (user == null) return new JsonResult(new { code = 404, message = "用户不存在" }, 404);

            // 验证用户是否已激活该资产
            var activeAsset = user.ActiveAssets?.FirstOrDefault(a => a.AssetId == assetId);
            if (activeAsset == null)
                return new JsonResult(new { code = 404, message = "您未激活此资产" }, 404);

            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(assetId);
            if (asset == null)
                return new JsonResult(new { code = 404, message = "资产不存在" }, 404);

            var pricesList = asset.Prices?.ToList();
            if (pricesList == null || pricesList.Count == 0)
                return new JsonResult(new { code = 400, message = "该资产暂无可用套餐" }, 400);

            string LocalizeDuration(string unit, int dur)
            {
                if (dur <= 0) return string.Empty;
                return unit switch
                {
                    "day" => dur + "天",
                    "month" => dur + "月",
                    "year" => dur + "年",
                    "hour" => dur + "小时",
                    "minute" => dur + "分钟",
                    _ => dur + unit
                };
            }
            var plans = pricesList.Select((p, idx) => new
            {
                id = idx + 1,
                name = $"套餐 {idx + 1}",
                duration = LocalizeDuration(p.Unit, p.Duration),
                price = p.Price,
                originalPrice = p.OriginalPrice,
                discountRate = p.DiscountRate
            }).ToList();

            return new JsonResult(new { code = 0, message = "成功", plans = plans }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"获取资产套餐列表失败: {ex.Message}");
            return new JsonResult(new { code = 500, message = "服务器错误" }, 500);
        }
    }

    /// <summary>
    /// 更变到新的套餐（费用需另行处理，此处仅更新本地状态）
    /// 请求体格式: { "planId": <套餐ID> }
    /// 注意：更变套餐不会继承上个套餐的剩余时常，将重新计算有效期
    /// </summary>
    [HttpHandle("/api/asset/{assetId}/changePlan", "POST", RateLimitMaxRequests = 10, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_ChangeAssetPlan(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token ?? string.Empty);
        if (principal == null) return new JsonResult(new { code = 401, message = "未授权" }, 401);

        var userName = principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName)) return new JsonResult(new { code = 400, message = "用户名无效" }, 400);

        if (await KaxGlobal.IsUserBanned(userName)) return new JsonResult(new { code = 403, message = "用户已被封禁" }, 403);

        if (string.IsNullOrEmpty(request.Body)) return new JsonResult(new { code = 400, message = "请求体不能为空" }, 400);

        if (!request.PathParameters.TryGetValue("assetId", out var assetIdStr) || !int.TryParse(assetIdStr, out var assetId) || assetId <= 0)
            return new JsonResult(new { code = 400, message = "assetId 参数无效" }, 400);

        try
        {
            var body = JsonNode.Parse(request.Body);
            if (body == null) return new JsonResult(new { code = 400, message = "无效的 JSON" }, 400);

            if (!int.TryParse(body["planId"]?.ToString(), out var planId) || planId <= 0)
                return new JsonResult(new { code = 400, message = "planId 参数无效" }, 400);

            var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
            if (user == null) return new JsonResult(new { code = 404, message = "用户不存在" }, 404);

            var activeAsset = user.ActiveAssets?.FirstOrDefault(a => a.AssetId == assetId);
            if (activeAsset == null)
                return new JsonResult(new { code = 404, message = "您未激活此资产" }, 404);

            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(assetId);
            if (asset == null)
                return new JsonResult(new { code = 404, message = "资产不存在" }, 404);

            var pricesList = asset.Prices?.ToList();
            if (pricesList == null || pricesList.Count == 0)
                return new JsonResult(new { code = 400, message = "该资产暂无可用套餐" }, 400);

            if (planId > pricesList.Count)
                return new JsonResult(new { code = 400, message = "套餐ID无效" }, 400);

            var selectedPlan = pricesList[planId - 1];
            var planPrice = selectedPlan.Price;

            if (user.Gold < planPrice)
                return new JsonResult(new { code = 403, message = $"金币不足，需 {planPrice} 金币" }, 403);

            user.Gold -= planPrice;
            Logger.Info($"用户 {userName} 为更变套餐支付金币 {planPrice}，剩余 {user.Gold}");

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long newExpiresAt;
            if (selectedPlan.Unit != null && selectedPlan.Duration > 0)
            {
                long delta = CalculateDurationInMilliseconds(selectedPlan.Unit, selectedPlan.Duration);
                newExpiresAt = (delta > 0) ? (now + delta) : now;
            }
            else
            {
                newExpiresAt = 0;
            }

            if (user.ActiveAssets != null)
            {
                var assetToRemove = user.ActiveAssets.FirstOrDefault(a => a.AssetId == assetId);
                if (assetToRemove != null)
                    user.ActiveAssets.Remove(assetToRemove);

                var newActiveAsset = new ActiveAssets
                {
                    ParentId = user.Id,
                    AssetId = assetId,
                    ActivatedAt = now,
                    ExpiresAt = newExpiresAt
                };
                user.ActiveAssets.Add(newActiveAsset);
            }

            if (user.ActiveAssets != null)
            {
                await KaxGlobal.UserDatabase.UpdateAsync(user);

                Logger.Info($"用户 {userName} 成功更变了资产 {assetId} 的套餐，需支付 ¥{planPrice}");

                return new JsonResult(new
                {
                    code = 0,
                    message = "套餐已更变",
                    cost = planPrice
                });
            }

            return new JsonResult(new { code = 500, message = "更新失败，请重试" }, 500);
        }
        catch (Exception ex)
        {
            Logger.Error($"更变资产套餐失败: {ex.Message}");
            return new JsonResult(new { code = 500, message = "服务器错误" }, 500);
        }
    }

    /// <summary>
    /// 取消订阅并从已激活资产中移除该资产
    /// 取消订阅后，用户将失去对该资源的访问权限
    /// </summary>
    [HttpHandle("/api/asset/{assetId}/unsubscribe", "POST", RateLimitMaxRequests = 10, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_UnsubscribeAsset(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token ?? string.Empty);
        if (principal == null) return new JsonResult(new { code = 401, message = "未授权" }, 401);

        var userName = principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName)) return new JsonResult(new { code = 400, message = "用户名无效" }, 400);

        if (await KaxGlobal.IsUserBanned(userName)) return new JsonResult(new { code = 403, message = "用户已被封禁" }, 403);

        if (!request.PathParameters.TryGetValue("assetId", out var assetIdStr) || !int.TryParse(assetIdStr, out var assetId) || assetId <= 0)
            return new JsonResult(new { code = 400, message = "assetId 参数无效" }, 400);

        try
        {
            var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
            if (user == null) return new JsonResult(new { code = 404, message = "用户不存在" }, 404);

            var activeAsset = user.ActiveAssets?.FirstOrDefault(a => a.AssetId == assetId);
            if (activeAsset == null)
                return new JsonResult(new { code = 404, message = "您未激活此资产，无法取消订阅" }, 404);

            if (user.ActiveAssets != null)
            {
                var assetToRemove = user.ActiveAssets.FirstOrDefault(a => a.AssetId == assetId);
                if (assetToRemove != null)
                    user.ActiveAssets.Remove(assetToRemove);
            }

            await KaxGlobal.UserDatabase.UpdateAsync(user);

            Logger.Info($"用户 {userName} 成功取消订阅了资产 {assetId}");

            return new JsonResult(new
            {
                code = 0,
                message = "订阅已取消，资产已从您的库中移除"
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"取消订阅资产失败: {ex.Message}");
            return new JsonResult(new { code = 500, message = "服务器错误" }, 500);
        }
    }

    #endregion
}

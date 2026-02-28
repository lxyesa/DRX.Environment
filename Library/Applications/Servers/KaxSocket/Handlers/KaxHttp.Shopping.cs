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
/// 购物模块 - 仅包含金币驱动的购买流程
///   POST /api/shop/purchase       — 购买 / 续期 / 升级资产
///   POST /api/asset/{id}/changePlan   — 更变套餐（重新计算有效期并扣金币）
///   POST /api/asset/{id}/unsubscribe  — 取消订阅（移除激活资产并写订单记录）
///
/// 套餐查询 (GET /api/asset/{id}/plans) 已迁移到 AssetQueries.cs
/// </summary>
public partial class KaxHttp
{
    #region 购物 (Shopping)

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

            // 写入订单记录
            if (user.OrderRecords == null)
                user.OrderRecords = new Drx.Sdk.Network.DataBase.TableList<UserOrderRecord>();
            user.OrderRecords.Add(new UserOrderRecord
            {
                ParentId = user.Id,
                OrderType = "purchase",
                AssetId = assetId,
                AssetName = asset.Name ?? string.Empty,
                CdkCode = string.Empty,
                GoldChange = -minimumGold,
                GoldChangeReason = "purchase",
                Description = $"{purchaseType}: {asset.Name} (priceId={priceId})"
            });

            EnsureSpecs(asset).PurchaseCount++;
            await KaxGlobal.AssetDataBase.UpdateAsync(asset);

            await KaxGlobal.UserDatabase.UpdateAsync(user);

            var finalAsset = user.ActiveAssets!.First(a => a.AssetId == assetId);
            var finalExpires = finalAsset.ExpiresAt;
            var updatedSpecs = asset.Specs ?? EnsureSpecs(asset);

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
                    purchaseType = purchaseType,
                    purchaseCount = updatedSpecs.PurchaseCount,
                    favoriteCount = updatedSpecs.FavoriteCount,
                    viewCount = updatedSpecs.ViewCount,
                    rating = updatedSpecs.Rating,
                    downloads = updatedSpecs.Downloads
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
    /// 更变到新的套餐（重新计算有效期，扣取所选套餐金币）
    /// POST /api/asset/{assetId}/changePlan
    /// 请求体: { "planId": "price-id-string" }
    /// 注意：更变套餐不会继承上个套餐剩余时长，将从当前时间重新计算有效期
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

            // planId 为 price.Id（字符串 GUID）
            var planId = body["planId"]?.ToString();
            if (string.IsNullOrWhiteSpace(planId))
                return new JsonResult(new { code = 400, message = "planId 参数缺失" }, 400);

            var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
            if (user == null) return new JsonResult(new { code = 404, message = "用户不存在" }, 404);

            var activeAsset = user.ActiveAssets?.FirstOrDefault(a => a.AssetId == assetId);
            if (activeAsset == null)
                return new JsonResult(new { code = 404, message = "您未激活此资产" }, 404);

            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(assetId);
            if (asset == null || asset.IsDeleted)
                return new JsonResult(new { code = 404, message = "资产不存在" }, 404);

            var selectedPlan = asset.Prices?.FirstOrDefault(p => p.Id == planId);
            if (selectedPlan == null)
                return new JsonResult(new { code = 404, message = "套餐不存在" }, 404);

            int planPrice = (int)Math.Round(selectedPlan.Price * (1.0 - selectedPlan.DiscountRate));
            planPrice = Math.Max(0, planPrice);

            if (user.Gold < planPrice)
                return new JsonResult(new { code = 402, message = $"金币不足，需 {planPrice} 金币" }, 402);

            user.Gold -= planPrice;

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long newExpiresAt = (selectedPlan.Unit == "once" || selectedPlan.Duration <= 0)
                ? 0
                : now + CalculateDurationInMilliseconds(selectedPlan.Unit, selectedPlan.Duration);

            // 移除旧条目，写入新条目（重新激活以替换有效期）
            user.ActiveAssets!.Remove(activeAsset);
            user.ActiveAssets.Add(new ActiveAssets
            {
                ParentId  = user.Id,
                AssetId   = assetId,
                ActivatedAt = now,
                ExpiresAt = newExpiresAt
            });

            // 写入订单记录
            if (user.OrderRecords == null)
                user.OrderRecords = new Drx.Sdk.Network.DataBase.TableList<UserOrderRecord>();
            user.OrderRecords.Add(new UserOrderRecord
            {
                ParentId         = user.Id,
                OrderType        = "change_plan",
                AssetId          = assetId,
                AssetName        = asset.Name ?? string.Empty,
                GoldChange       = -planPrice,
                GoldChangeReason = "plan_change",
                PlanTransition   = planId,
                Description      = $"更变套餐: {asset.Name} → priceId={planId}"
            });

            await KaxGlobal.UserDatabase.UpdateAsync(user);
            Logger.Info($"用户 {userName} 更变资产 {asset.Name}(ID:{assetId}) 套餐，扣金币 {planPrice}，新过期时间: {(newExpiresAt == 0 ? "永久" : DateTimeOffset.FromUnixTimeMilliseconds(newExpiresAt).ToString("yyyy-MM-dd HH:mm:ss"))}");

            return new JsonResult(new
            {
                code       = 0,
                message    = "套餐已更变",
                assetId    = assetId,
                assetName  = asset.Name,
                planId     = selectedPlan.Id,
                goldDeducted = planPrice,
                userGold   = user.Gold,
                expiresAt  = newExpiresAt
            }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"更变资产套餐失败: {ex.Message}");
            return new JsonResult(new { code = 500, message = "服务器错误" }, 500);
        }
    }

    /// <summary>
    /// 取消订阅并从已激活资产中移除该资产，写入订单记录
    /// POST /api/asset/{assetId}/unsubscribe
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

            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(assetId);
            string assetName = asset?.Name ?? $"Asset#{assetId}";

            user.ActiveAssets!.Remove(activeAsset);

            // 写入订单记录
            if (user.OrderRecords == null)
                user.OrderRecords = new Drx.Sdk.Network.DataBase.TableList<UserOrderRecord>();
            user.OrderRecords.Add(new UserOrderRecord
            {
                ParentId    = user.Id,
                OrderType   = "cancel_subscription",
                AssetId     = assetId,
                AssetName   = assetName,
                GoldChange  = 0,
                Description = $"取消订阅: {assetName} (到期={activeAsset.ExpiresAt})"
            });

            await KaxGlobal.UserDatabase.UpdateAsync(user);
            Logger.Info($"用户 {userName} 取消订阅资产 {assetName}(ID:{assetId})");

            return new JsonResult(new
            {
                code      = 0,
                message   = "订阅已取消，资产已从您的库中移除",
                assetId   = assetId,
                assetName = assetName,
                cancelledAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"取消订阅资产失败: {ex.Message}");
            return new JsonResult(new { code = 500, message = "服务器错误" }, 500);
        }
    }

    #endregion
}

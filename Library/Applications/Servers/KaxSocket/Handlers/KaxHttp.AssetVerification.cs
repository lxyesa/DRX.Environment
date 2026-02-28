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
/// 资源验证模块 - 处理用户资产验证、CDK激活等功能
/// </summary>
public partial class KaxHttp
{
    #region 资源验证 (Asset Verification)

    [HttpHandle("/api/user/verify/asset/{assetId}", "GET", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Get_VerifyAsset(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        var userName = principal?.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName))
        {
            return new JsonResult(new { message = "未授权" }, 401);
        }

        if (await KaxGlobal.IsUserBanned(userName))
        {
            return new JsonResult(new { message = "账号被封禁" }, 403);
        }

        if (!request.PathParameters.TryGetValue("assetId", out var assetIdString) ||
            !int.TryParse(assetIdString, out var assetId) || assetId <= 0)
        {
            return new JsonResult(new { message = "assetId 参数必须是大于 0 的整数" }, 400);
        }

        var hasAsset = await KaxGlobal.VerifyUserHasActiveAsset(userName, assetId);

        // 返回统一结构：HTTP 200 + 内部业务码（0 = 拥有，2004 = 未拥有）
        if (hasAsset)
        {
            return new JsonResult(new { assetId = assetId, has = true, code = 0 });
        }
        else
        {
            return new JsonResult(new { assetId = assetId, has = false, code = 2004 });
        }
    }

    // 新增：返回资产原始激活记录（activatedAt / expiresAt）——raw
    [HttpHandle("/api/user/verify/asset/{assetId}/raw", "GET", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Get_VerifyAssetRaw(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        var userName = principal?.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName))
            return new JsonResult(new { message = "未授权" }, 401);

        if (await KaxGlobal.IsUserBanned(userName))
            return new JsonResult(new { message = "账号被封禁" }, 403);

        if (!request.PathParameters.TryGetValue("assetId", out var assetIdString) ||
            !int.TryParse(assetIdString, out var assetId) || assetId <= 0)
        {
            return new JsonResult(new { message = "assetId 参数必须是大于 0 的整数" }, 400);
        }

        var entry = await KaxGlobal.GetUserActiveAssetRawAsync(userName, assetId);
        if (entry == null)
        {
            return new JsonResult(new { assetId = assetId, activatedAt = 0L, expiresAt = 0L, has = false, code = 2004 });
        }

        return new JsonResult(new { assetId = assetId, activatedAt = entry.ActivatedAt, expiresAt = entry.ExpiresAt, has = true, code = 0 });
    }

    // 新增：返回资产剩余时间（秒）——如果永久返回 -1
    [HttpHandle("/api/user/verify/asset/{assetId}/remaining", "GET", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Get_VerifyAssetRemaining(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        var userName = principal?.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName))
            return new JsonResult(new { message = "未授权" }, 401);

        if (await KaxGlobal.IsUserBanned(userName))
            return new JsonResult(new { message = "账号被封禁" }, 403);

        if (!request.PathParameters.TryGetValue("assetId", out var assetIdString) ||
            !int.TryParse(assetIdString, out var assetId) || assetId <= 0)
        {
            return new JsonResult(new { message = "assetId 参数必须是大于 0 的整数" }, 400);
        }

        var remaining = await KaxGlobal.GetUserAssetRemainingSecondsAsync(userName, assetId);

        // null 表示无记录 / 未拥有
        if (remaining == null)
        {
            return new JsonResult(new { assetId = assetId, has = false, remainingSeconds = 0L, code = 2004 });
        }

        var isActive = (remaining == -1L) || (remaining > 0L);
        return new JsonResult(new { assetId = assetId, has = isActive, remainingSeconds = remaining, code = isActive ? 0 : 2004 });
    }

    /// <summary>
    /// 用户激活 CDK API
    /// 请求体格式: { "code": "CDK_CODE" }
    /// 响应: { "code": <错误码>, "message": <消息>, "assetId": <资源ID（可选）>, "goldValue": <金币（可选）> }
    /// </summary>
    [HttpHandle("/api/cdk/activate", "POST", RateLimitMaxRequests = 20, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_ActivateCdk(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        if (principal == null)
        {
            return new JsonResult(new { code = 401, message = "未授权" }, 401);
        }

        var userName = principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName))
        {
            return new JsonResult(new { code = 401, message = "未授权" }, 401);
        }

        if (await KaxGlobal.IsUserBanned(userName))
        {
            return new JsonResult(new { code = 403, message = "账号被封禁" }, 403);
        }

        if (string.IsNullOrEmpty(request.Body))
        {
            return new JsonResult(new { code = 400, message = "请求体不能为空" }, 400);
        }

        var body = JsonNode.Parse(request.Body);
        if (body == null)
        {
            return new JsonResult(new { code = 400, message = "无效的 JSON" }, 400);
        }

        var cdkCode = body["code"]?.ToString()?.Trim();
        if (string.IsNullOrEmpty(cdkCode))
        {
            return new JsonResult(new { code = 1, message = "CDK为空" }, 400);
        }

        // 调用激活方法
        var (resultCode, resultMessage) = await KaxGlobal.ActivateCdkAsync(cdkCode, userName);

        // 根据激活结果返回对应的 HTTP 状态码和响应
        int httpStatus = resultCode switch
        {
            0 => 200,          // 成功
            1 => 400,          // CDK为空
            2 => 400,          // CDK错误
            3 => 409,          // CDK已使用（冲突）
            _ => 500           // 服务器错误
        };

        // 如果激活成功，同时返回 CDK 对应的资源信息
        if (resultCode == 0)
        {
            try
            {
                var normalizedCode = cdkCode.Trim().ToUpperInvariant();
                var all = await KaxGlobal.CdkDatabase.SelectAllAsync();
                var cdk = all.FirstOrDefault(c => string.Equals(c.Code, normalizedCode, StringComparison.OrdinalIgnoreCase));

                if (cdk != null)
                {
                    // 写入订单记录
                    var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
                    if (user != null)
                    {
                        if (user.OrderRecords == null)
                            user.OrderRecords = new Drx.Sdk.Network.DataBase.TableList<UserOrderRecord>();
                        user.OrderRecords.Add(new UserOrderRecord
                        {
                            ParentId = user.Id,
                            OrderType = "cdk",
                            AssetId = 0,
                            AssetName = string.Empty,
                            CdkCode = cdk.Code,
                            GoldChange = cdk.GoldValue,
                            Description = string.IsNullOrEmpty(cdk.Description)
                                ? $"兑换 CDK: {cdk.Code}"
                                : $"兑换 CDK: {cdk.Code} ({cdk.Description})"
                        });
                        await KaxGlobal.UserDatabase.UpdateAsync(user);
                    }

                    return new JsonResult(new
                    {
                        code = resultCode,
                        message = resultMessage,
                        goldValue = cdk.GoldValue,
                        description = cdk.Description
                    }, httpStatus);
                }
            }
            catch { /* 忽略异常，仅返回基本信息 */ }
        }

        return new JsonResult(new { code = resultCode, message = resultMessage }, httpStatus);
    }

    [HttpHandle("/api/user/unban?{userName}?{dev_code}", "POST")]
    public static HttpResponse Post_UnBanUser(HttpRequest request)
    {
        try
        {
            var userName = request.PathParameters["userName"];
            var devCode = request.PathParameters["dev_code"];
            if (devCode != "yuerzuikeai001")
            {
                return new HttpResponse()
                {
                    StatusCode = 403,
                    Body = "无效的开发者代码，无法解除封禁。",
                };
            }
            _ = KaxGlobal.UnBanUser(userName);
            return new HttpResponse()
            {
                StatusCode = 200,
                Body = $"已解除用户 {userName} 的封禁状态。",
            };
        }
        catch (Exception ex)
        {
            Logger.Error("解除封禁时发生异常: " + ex.Message);
            return new HttpResponse()
            {
                StatusCode = 500,
                Body = "服务器错误，无法处理请求。",
            };
        }
    }

    #endregion
}

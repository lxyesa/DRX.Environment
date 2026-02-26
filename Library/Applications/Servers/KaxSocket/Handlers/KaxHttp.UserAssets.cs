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

namespace KaxSocket.Handlers;

/// <summary>
/// 用户资产模块 - 处理用户激活资产、收藏、购物车等功能
/// </summary>
public partial class KaxHttp
{
    #region 用户资产管理 (User Asset Management)

    /// <summary>
    /// 获取用户的激活资源列表（当前有效的资源）
    /// 响应包含资源ID、激活时间、过期时间及剩余秒数
    /// </summary>
    [HttpHandle("/api/user/assets/active", "GET", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Get_UserActiveAssets(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token ?? string.Empty);
        if (principal == null) return new JsonResult(new { code = 401, message = "未授权" }, 401);

        var userName = principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName)) return new JsonResult(new { code = 400, message = "用户名无效" }, 400);

        if (await KaxGlobal.IsUserBanned(userName)) return new JsonResult(new { code = 403, message = "用户已被封禁" }, 403);

        var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (user == null) return new JsonResult(new { code = 404, message = "用户不存在" }, 404);

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var activeAssets = user.ActiveAssets
            .Select(a => new
            {
                id = a.Id,
                assetId = a.AssetId,
                activatedAt = a.ActivatedAt,
                expiresAt = a.ExpiresAt,
                remainingSeconds = a.ExpiresAt == 0 ? -1L : (a.ExpiresAt - now) / 1000,
                isExpired = a.ExpiresAt != 0 && a.ExpiresAt <= now
            })
            .ToList();

        return new JsonResult(new { code = 0, message = "成功", data = activeAssets }, 200);
    }

    // 用户收藏相关 API
    [HttpHandle("/api/user/favorites", "GET", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Get_UserFavorites(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token ?? string.Empty);
        if (principal == null) return new JsonResult(new { code = 401, message = "未授权" }, 401);

        var userName = principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName)) return new JsonResult(new { code = 400, message = "用户名无效" }, 400);

        var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (user == null) return new JsonResult(new { code = 404, message = "用户不存在" }, 404);

        var favs = user.FavoriteAssets?.Select(f => f.AssetId).ToList() ?? new List<int>();
        return new JsonResult(new { code = 0, message = "成功", data = favs }, 200);
    }

    [HttpHandle("/api/user/favorites", "POST", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_AddFavorite(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token ?? string.Empty);
        if (principal == null) return new JsonResult(new { code = 401, message = "未授权" }, 401);

        var userName = principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName)) return new JsonResult(new { code = 400, message = "用户名无效" }, 400);

        if (string.IsNullOrEmpty(request.Body)) return new JsonResult(new { code = 400, message = "请求体不能为空" }, 400);

        var body = JsonNode.Parse(request.Body);
        if (body == null) return new JsonResult(new { code = 400, message = "无法解析请求体" }, 400);
        if (!int.TryParse(body["assetId"]?.ToString(), out var assetId) || assetId <= 0)
            return new JsonResult(new { code = 400, message = "assetId 无效" }, 400);

        var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (user == null) return new JsonResult(new { code = 404, message = "用户不存在" }, 404);

        var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(assetId);
        if (asset == null) return new JsonResult(new { code = 404, message = "资源不存在" }, 404);

        // 已收藏则直接返回成功
        if (user.FavoriteAssets != null && user.FavoriteAssets.Any(f => f.AssetId == assetId))
            return new JsonResult(new { code = 0, message = "已收藏", favoriteCount = asset.FavoriteCount }, 200);

        // 添加收藏子项
        var fav = new UserFavoriteAsset { ParentId = user.Id, AssetId = assetId };
        if (user.FavoriteAssets == null) user.FavoriteAssets = new Drx.Sdk.Network.DataBase.TableList<UserFavoriteAsset>();
        user.FavoriteAssets.Add(fav);

        // 更新资产收藏计数
        asset.FavoriteCount = Math.Max(0, asset.FavoriteCount + 1);

        await KaxGlobal.AssetDataBase.UpdateAsync(asset);
        await KaxGlobal.UserDatabase.UpdateAsync(user);

        return new JsonResult(new { code = 0, message = "收藏成功", favoriteCount = asset.FavoriteCount }, 200);
    }

    [HttpHandle("/api/user/favorites/{assetId}", "DELETE", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Delete_Favorite(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token ?? string.Empty);
        if (principal == null) return new JsonResult(new { code = 401, message = "未授权" }, 401);

        var userName = principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName)) return new JsonResult(new { code = 400, message = "用户名无效" }, 400);

        if (!request.PathParameters.TryGetValue("assetId", out var idStr) || !int.TryParse(idStr, out var assetId) || assetId <= 0)
            return new JsonResult(new { code = 400, message = "assetId 参数无效" }, 400);

        var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (user == null) return new JsonResult(new { code = 404, message = "用户不存在" }, 404);

        var existing = user.FavoriteAssets?.FirstOrDefault(f => f.AssetId == assetId);
        if (existing == null) return new JsonResult(new { code = 0, message = "未收藏" }, 200);

        // 移除
        if (user.FavoriteAssets != null) user.FavoriteAssets.Remove(existing);

        var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(assetId);
        if (asset != null)
        {
            asset.FavoriteCount = Math.Max(0, asset.FavoriteCount - 1);
            await KaxGlobal.AssetDataBase.UpdateAsync(asset);
        }

        await KaxGlobal.UserDatabase.UpdateAsync(user);

        return new JsonResult(new { code = 0, message = "已取消收藏", favoriteCount = asset?.FavoriteCount ?? 0 }, 200);
    }

    // 用户购物车（仅记录 assetId）
    [HttpHandle("/api/user/cart", "GET", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Get_UserCart(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token ?? string.Empty);
        if (principal == null) return new JsonResult(new { code = 401, message = "未授权" }, 401);

        var userName = principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName)) return new JsonResult(new { code = 400, message = "用户名无效" }, 400);

        var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (user == null) return new JsonResult(new { code = 404, message = "用户不存在" }, 404);

        var items = user.CartItems?.Select(c => c.AssetId).ToList() ?? new List<int>();
        return new JsonResult(new { code = 0, message = "成功", data = items }, 200);
    }

    [HttpHandle("/api/user/cart", "POST", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_AddCart(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token ?? string.Empty);
        if (principal == null) return new JsonResult(new { code = 401, message = "未授权" }, 401);

        var userName = principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName)) return new JsonResult(new { code = 400, message = "用户名无效" }, 400);

        if (string.IsNullOrEmpty(request.Body)) return new JsonResult(new { code = 400, message = "请求体不能为空" }, 400);
        var body = JsonNode.Parse(request.Body);
        if (body == null) return new JsonResult(new { code = 400, message = "无法解析请求体" }, 400);
        if (!int.TryParse(body["assetId"]?.ToString(), out var assetId) || assetId <= 0)
            return new JsonResult(new { code = 400, message = "assetId 无效" }, 400);

        var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (user == null) return new JsonResult(new { code = 404, message = "用户不存在" }, 404);

        if (user.CartItems == null) user.CartItems = new Drx.Sdk.Network.DataBase.TableList<UserCartItem>();
        if (!user.CartItems.Any(c => c.AssetId == assetId))
        {
            user.CartItems.Add(new UserCartItem { ParentId = user.Id, AssetId = assetId });
            await KaxGlobal.UserDatabase.UpdateAsync(user);
        }

        return new JsonResult(new { code = 0, message = "已加入购物车" }, 200);
    }

    [HttpHandle("/api/user/cart/{assetId}", "DELETE", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Delete_CartItem(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token ?? string.Empty);
        if (principal == null) return new JsonResult(new { code = 401, message = "未授权" }, 401);

        var userName = principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName)) return new JsonResult(new { code = 400, message = "用户名无效" }, 400);

        if (!request.PathParameters.TryGetValue("assetId", out var idStr) || !int.TryParse(idStr, out var assetId) || assetId <= 0)
            return new JsonResult(new { code = 400, message = "assetId 参数无效" }, 400);

        var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (user == null) return new JsonResult(new { code = 404, message = "用户不存在" }, 404);

        var existing = user.CartItems?.FirstOrDefault(c => c.AssetId == assetId);
        if (existing != null && user.CartItems != null)
        {
            user.CartItems.Remove(existing);
            await KaxGlobal.UserDatabase.UpdateAsync(user);
        }

        return new JsonResult(new { code = 0, message = "已从购物车移除" }, 200);
    }

    #endregion
}

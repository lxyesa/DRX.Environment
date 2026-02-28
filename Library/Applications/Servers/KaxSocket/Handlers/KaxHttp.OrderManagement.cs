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
/// 订单管理模块 - 处理用户订单详细的查询与删除
/// </summary>
public partial class KaxHttp
{
    #region 订单管理 (Order Management)

    /// <summary>
    /// 获取当前用户的订单列表（支持分页）
    /// GET /api/user/orders?page=1&amp;pageSize=50
    /// </summary>
    [HttpHandle("/api/user/orders", "GET", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Get_UserOrders(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token ?? string.Empty);
        if (principal == null) return new JsonResult(new { code = 401, message = "未授权" }, 401);

        var userName = principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName)) return new JsonResult(new { code = 400, message = "用户名无效" }, 400);

        if (await KaxGlobal.IsUserBanned(userName)) return new JsonResult(new { code = 403, message = "账号被封禁" }, 403);

        try
        {
            var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
            if (user == null) return new JsonResult(new { code = 404, message = "用户不存在" }, 404);

            int page = 1;
            int pageSize = 50;
            if (!int.TryParse(request.Query["page"], out page) || page <= 0) page = 1;
            if (!int.TryParse(request.Query["pageSize"], out pageSize) || pageSize <= 0) pageSize = 50;
            pageSize = Math.Clamp(pageSize, 1, 200);

            var orders = (user.OrderRecords ?? new Drx.Sdk.Network.DataBase.TableList<UserOrderRecord>())
                .OrderByDescending(o => o.CreatedAt)
                .ToList();
            var total = orders.Count;
            var items = orders.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(o => new
                {
                    id = o.Id,
                    orderType = o.OrderType,
                    assetId = o.AssetId,
                    assetName = o.AssetName,
                    cdkCode = o.CdkCode,
                    goldChange = o.GoldChange,
                    description = o.Description,
                    createdAt = o.CreatedAt
                })
                .ToList<object>();

            return new JsonResult(new { code = 0, message = "成功", data = items, page, pageSize, total }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"获取订单列表失败: {ex.Message}");
            return new JsonResult(new { code = 500, message = "服务器错误" }, 500);
        }
    }

    /// <summary>
    /// 管理员查询指定用户的订单列表（权限 ≤ 3：System/Console/Admin）
    /// GET /api/admin/orders/{userId}?page=1&amp;pageSize=50
    /// </summary>
    [HttpHandle("/api/admin/orders/{userId}", "GET", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Get_AdminUserOrders(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token ?? string.Empty);
        if (principal == null) return new JsonResult(new { code = 401, message = "未授权" }, 401);

        var operatorName = principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(operatorName)) return new JsonResult(new { code = 400, message = "用户名无效" }, 400);

        if (!await IsOrderAdminUser(operatorName)) return new JsonResult(new { code = 403, message = "权限不足" }, 403);

        if (!request.PathParameters.TryGetValue("userId", out var userIdStr) || !int.TryParse(userIdStr, out var userId) || userId <= 0)
            return new JsonResult(new { code = 400, message = "userId 参数无效" }, 400);

        try
        {
            var targetUser = await KaxGlobal.UserDatabase.SelectByIdAsync(userId);
            if (targetUser == null) return new JsonResult(new { code = 404, message = "用户不存在" }, 404);

            int page = 1;
            int pageSize = 50;
            if (!int.TryParse(request.Query["page"], out page) || page <= 0) page = 1;
            if (!int.TryParse(request.Query["pageSize"], out pageSize) || pageSize <= 0) pageSize = 50;
            pageSize = Math.Clamp(pageSize, 1, 200);

            var orders = (targetUser.OrderRecords ?? new Drx.Sdk.Network.DataBase.TableList<UserOrderRecord>())
                .OrderByDescending(o => o.CreatedAt)
                .ToList();
            var total = orders.Count;
            var items = orders.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(o => new
                {
                    id = o.Id,
                    orderType = o.OrderType,
                    assetId = o.AssetId,
                    assetName = o.AssetName,
                    cdkCode = o.CdkCode,
                    goldChange = o.GoldChange,
                    description = o.Description,
                    createdAt = o.CreatedAt
                })
                .ToList<object>();

            return new JsonResult(new { code = 0, message = "成功", data = items, page, pageSize, total, userId = targetUser.Id, userName = targetUser.UserName }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"管理员获取用户订单失败: {ex.Message}");
            return new JsonResult(new { code = 500, message = "服务器错误" }, 500);
        }
    }

    /// <summary>
    /// 删除指定用户的某条订单（权限 ≤ 3：System/Console/Admin）
    /// DELETE /api/admin/orders/{userId}/{orderId}
    /// </summary>
    [HttpHandle("/api/admin/orders/{userId}/{orderId}", "DELETE", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Delete_AdminOrder(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token ?? string.Empty);
        if (principal == null) return new JsonResult(new { code = 401, message = "未授权" }, 401);

        var operatorName = principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(operatorName)) return new JsonResult(new { code = 400, message = "用户名无效" }, 400);

        if (!await IsOrderAdminUser(operatorName)) return new JsonResult(new { code = 403, message = "权限不足" }, 403);

        if (!request.PathParameters.TryGetValue("userId", out var userIdStr) || !int.TryParse(userIdStr, out var userId) || userId <= 0)
            return new JsonResult(new { code = 400, message = "userId 参数无效" }, 400);

        if (!request.PathParameters.TryGetValue("orderId", out var orderId) || string.IsNullOrWhiteSpace(orderId))
            return new JsonResult(new { code = 400, message = "orderId 参数无效" }, 400);

        try
        {
            var targetUser = await KaxGlobal.UserDatabase.SelectByIdAsync(userId);
            if (targetUser == null) return new JsonResult(new { code = 404, message = "用户不存在" }, 404);

            var order = targetUser.OrderRecords?.FirstOrDefault(o => o.Id == orderId);
            if (order == null) return new JsonResult(new { code = 404, message = "订单不存在" }, 404);

            targetUser.OrderRecords!.Remove(order);
            await KaxGlobal.UserDatabase.UpdateAsync(targetUser);

            Logger.Info($"管理员 {operatorName} 删除了用户 {targetUser.UserName}（ID:{userId}）的订单 {orderId}");
            return new JsonResult(new { code = 0, message = "订单已删除" }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"删除订单失败: {ex.Message}");
            return new JsonResult(new { code = 500, message = "服务器错误" }, 500);
        }
    }

    /// <summary>
    /// 批量删除指定用户的多条订单（权限 ≤ 3：System/Console/Admin）
    /// POST /api/admin/orders/{userId}/delete
    /// 请求体: { "orderIds": ["id1","id2",...] }
    /// </summary>
    [HttpHandle("/api/admin/orders/{userId}/delete", "POST", RateLimitMaxRequests = 30, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_AdminBatchDeleteOrders(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token ?? string.Empty);
        if (principal == null) return new JsonResult(new { code = 401, message = "未授权" }, 401);

        var operatorName = principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(operatorName)) return new JsonResult(new { code = 400, message = "用户名无效" }, 400);

        if (!await IsOrderAdminUser(operatorName)) return new JsonResult(new { code = 403, message = "权限不足" }, 403);

        if (!request.PathParameters.TryGetValue("userId", out var userIdStr) || !int.TryParse(userIdStr, out var userId) || userId <= 0)
            return new JsonResult(new { code = 400, message = "userId 参数无效" }, 400);

        if (string.IsNullOrEmpty(request.Body)) return new JsonResult(new { code = 400, message = "请求体不能为空" }, 400);
        var body = JsonNode.Parse(request.Body);
        if (body == null) return new JsonResult(new { code = 400, message = "无效的 JSON" }, 400);

        var orderIdsNode = body["orderIds"] as JsonArray;
        if (orderIdsNode == null || orderIdsNode.Count == 0)
            return new JsonResult(new { code = 400, message = "orderIds 不能为空" }, 400);

        try
        {
            var targetUser = await KaxGlobal.UserDatabase.SelectByIdAsync(userId);
            if (targetUser == null) return new JsonResult(new { code = 404, message = "用户不存在" }, 404);

            if (targetUser.OrderRecords == null)
                return new JsonResult(new { code = 0, message = "无订单可删除", removed = 0 }, 200);

            var idsToDelete = orderIdsNode
                .Where(n => n != null)
                .Select(n => n!.ToString())
                .ToHashSet();

            var toRemove = targetUser.OrderRecords.Where(o => idsToDelete.Contains(o.Id)).ToList();
            foreach (var o in toRemove)
                targetUser.OrderRecords.Remove(o);

            if (toRemove.Count > 0)
                await KaxGlobal.UserDatabase.UpdateAsync(targetUser);

            Logger.Info($"管理员 {operatorName} 批量删除了用户 {targetUser.UserName}（ID:{userId}）的 {toRemove.Count} 条订单");
            return new JsonResult(new { code = 0, message = "批量删除完成", removed = toRemove.Count }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"批量删除订单失败: {ex.Message}");
            return new JsonResult(new { code = 500, message = "服务器错误" }, 500);
        }
    }

    // 检查操作者是否具有订单管理权限（权限组数值 ≤ 3，即 System=0、Console=2、Admin=3）
    private static async Task<bool> IsOrderAdminUser(string? userName)
    {
        if (string.IsNullOrWhiteSpace(userName)) return false;
        var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (user == null) return false;
        return (int)user.PermissionGroup <= 3;
    }

    #endregion
}

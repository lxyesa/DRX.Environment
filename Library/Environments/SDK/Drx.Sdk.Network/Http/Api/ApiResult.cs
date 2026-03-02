using System;
using System.Collections.Generic;
using System.Linq;
using Drx.Sdk.Network.Http.Protocol;

namespace Drx.Sdk.Network.Http.Api
{
    /// <summary>
    /// 统一 API 响应构建器。
    /// 所有响应遵循格式：{ code: int, message: string?, data: object? }
    /// <para>
    /// 使用示例：
    /// <code>
    /// return ApiResult.Ok(new { id = 1, name = "Alice" });
    /// return ApiResult.Error(400, "参数无效");
    /// return ApiResult.Paginated(items, total, page, pageSize);
    /// </code>
    /// </para>
    /// </summary>
    public static class ApiResult
    {
        #region 成功响应 (2xx)

        /// <summary>
        /// 返回成功响应（无数据）
        /// </summary>
        /// <param name="message">可选的成功消息</param>
        public static IActionResult Ok(string? message = null)
        {
            return new JsonResult(new { code = 0, message = message ?? "success" });
        }

        /// <summary>
        /// 返回成功响应（带数据）
        /// </summary>
        /// <param name="data">响应数据对象</param>
        /// <param name="message">可选的成功消息</param>
        public static IActionResult Ok(object data, string? message = null)
        {
            return new JsonResult(new { code = 0, message = message ?? "success", data });
        }

        /// <summary>
        /// 返回 201 Created 响应
        /// </summary>
        /// <param name="data">创建的资源数据</param>
        /// <param name="message">可选消息</param>
        public static IActionResult Created(object? data = null, string? message = null)
        {
            if (data != null)
                return new JsonResult(new { code = 0, message = message ?? "created", data }, 201);
            return new JsonResult(new { code = 0, message = message ?? "created" }, 201);
        }

        #endregion

        #region 分页响应

        /// <summary>
        /// 返回分页数据响应
        /// </summary>
        /// <typeparam name="T">列表元素类型</typeparam>
        /// <param name="items">当前页的数据列表</param>
        /// <param name="total">数据总条数</param>
        /// <param name="page">当前页码（从 1 开始）</param>
        /// <param name="pageSize">每页大小</param>
        /// <param name="message">可选消息</param>
        public static IActionResult Paginated<T>(IEnumerable<T> items, int total, int page, int pageSize, string? message = null)
        {
            var totalPages = pageSize > 0 ? (int)Math.Ceiling((double)total / pageSize) : 0;
            return new JsonResult(new
            {
                code = 0,
                message = message ?? "success",
                data = items,
                pagination = new
                {
                    page,
                    pageSize,
                    total,
                    totalPages,
                    hasMore = page < totalPages
                }
            });
        }

        /// <summary>
        /// 对 IEnumerable 就地分页并返回分页结果。
        /// 会自动计算 total，然后 Skip/Take。
        /// </summary>
        public static IActionResult AutoPaginated<T>(IEnumerable<T> allItems, int page, int pageSize, string? message = null)
        {
            var list = allItems as IList<T> ?? allItems.ToList();
            var total = list.Count;
            var paged = list.Skip((page - 1) * pageSize).Take(pageSize);
            return Paginated(paged, total, page, pageSize, message);
        }

        #endregion

        #region 错误响应 (4xx / 5xx)

        /// <summary>
        /// 返回通用错误响应
        /// </summary>
        /// <param name="httpStatus">HTTP 状态码</param>
        /// <param name="message">错误消息</param>
        /// <param name="errorCode">可选的业务错误码（默认等于 httpStatus）</param>
        public static IActionResult Error(int httpStatus, string message, int? errorCode = null)
        {
            return new JsonResult(new { code = errorCode ?? httpStatus, message }, httpStatus);
        }

        /// <summary>400 Bad Request</summary>
        public static IActionResult BadRequest(string message = "请求参数无效") => Error(400, message);

        /// <summary>401 Unauthorized</summary>
        public static IActionResult Unauthorized(string message = "未授权") => Error(401, message);

        /// <summary>403 Forbidden</summary>
        public static IActionResult Forbidden(string message = "禁止访问") => Error(403, message);

        /// <summary>403 Forbidden - 封禁专用</summary>
        public static IActionResult Banned(string message = "账号被封禁") => Error(403, message);

        /// <summary>404 Not Found</summary>
        public static IActionResult NotFound(string message = "资源不存在") => Error(404, message);

        /// <summary>409 Conflict</summary>
        public static IActionResult Conflict(string message = "资源冲突") => Error(409, message);

        /// <summary>429 Too Many Requests</summary>
        public static IActionResult TooManyRequests(string message = "请求过于频繁，请稍后再试") => Error(429, message);

        /// <summary>500 Internal Server Error</summary>
        public static IActionResult ServerError(string message = "服务器内部错误") => Error(500, message);

        #endregion
    }
}

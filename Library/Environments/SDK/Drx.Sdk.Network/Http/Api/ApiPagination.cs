using System;
using Drx.Sdk.Network.Http.Protocol;

namespace Drx.Sdk.Network.Http.Api
{
    /// <summary>
    /// 分页参数解析工具。
    /// 从查询字符串中提取并验证 page / pageSize 参数。
    /// <para>
    /// 使用示例：
    /// <code>
    /// var (page, pageSize) = ApiPagination.Parse(request);
    /// var pagedItems = allItems.Skip((page - 1) * pageSize).Take(pageSize);
    /// return ApiResult.Paginated(pagedItems, total, page, pageSize);
    /// </code>
    /// </para>
    /// </summary>
    public static class ApiPagination
    {
        /// <summary>
        /// 从请求查询参数中解析分页信息
        /// </summary>
        /// <param name="request">HTTP 请求</param>
        /// <param name="defaultPage">默认页码（默认 1）</param>
        /// <param name="defaultPageSize">默认每页大小（默认 50）</param>
        /// <param name="maxPageSize">最大允许每页大小（默认 200）</param>
        /// <returns>验证后的 (page, pageSize) 元组</returns>
        public static (int page, int pageSize) Parse(HttpRequest request, int defaultPage = 1, int defaultPageSize = 50, int maxPageSize = 200)
        {
            int page = defaultPage;
            int pageSize = defaultPageSize;

            if (int.TryParse(request.Query["page"], out var p) && p > 0) page = p;
            if (int.TryParse(request.Query["pageSize"], out var ps) && ps > 0) pageSize = ps;

            pageSize = Math.Clamp(pageSize, 1, maxPageSize);
            return (page, pageSize);
        }

        /// <summary>
        /// 解析排序参数
        /// </summary>
        /// <param name="request">HTTP 请求</param>
        /// <param name="defaultSortBy">默认排序字段</param>
        /// <param name="defaultDescending">默认是否降序</param>
        /// <returns>(sortBy, descending) 元组</returns>
        public static (string sortBy, bool descending) ParseSort(HttpRequest request, string defaultSortBy = "id", bool defaultDescending = true)
        {
            var sortBy = request.Query["sortBy"] ?? request.Query["sort"];
            if (string.IsNullOrWhiteSpace(sortBy)) sortBy = defaultSortBy;

            var orderStr = request.Query["order"] ?? request.Query["sortOrder"];
            var descending = defaultDescending;
            if (!string.IsNullOrEmpty(orderStr))
            {
                descending = orderStr.Equals("desc", StringComparison.OrdinalIgnoreCase);
            }

            return (sortBy, descending);
        }

        /// <summary>
        /// 解析搜索关键字
        /// </summary>
        /// <param name="request">HTTP 请求</param>
        /// <param name="paramNames">参数名列表（默认检查 q, search, keyword）</param>
        /// <returns>搜索关键字（可能为 null）</returns>
        public static string? ParseSearch(HttpRequest request, params string[] paramNames)
        {
            if (paramNames == null || paramNames.Length == 0)
                paramNames = new[] { "q", "search", "keyword" };

            foreach (var name in paramNames)
            {
                var value = request.Query[name]?.Trim();
                if (!string.IsNullOrEmpty(value)) return value;
            }
            return null;
        }
    }
}

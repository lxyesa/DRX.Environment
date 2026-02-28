using System;
using System.Collections.Generic;
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
/// CDK 管理模块 - 处理 CDK 的生成、保存、查询、删除等功能
/// CDK 仅支持金币类型，不再绑定资产
/// </summary>
public partial class KaxHttp
{
    #region CDK 管理 (CDK Administration)

    /// <summary>
    /// 查询单个 CDK 的详细信息（包含创建者、使用者等溯源信息）
    /// </summary>
    [HttpHandle("/api/cdk/admin/inspect", "POST", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_InspectCdk(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        if (principal == null) return new JsonResult(new { message = "未授权" }, 401);
        var userName = principal.Identity?.Name;
        if (await KaxGlobal.IsUserBanned(userName!)) return new JsonResult(new { message = "账号被封禁" }, 403);
        if (!await IsCdkAdminUser(userName)) return new JsonResult(new { message = "权限不足" }, 403);

        if (string.IsNullOrEmpty(request.Body)) return new JsonResult(new { message = "请求体不能为空" }, 400);
        var body = JsonNode.Parse(request.Body);
        if (body == null) return new JsonResult(new { message = "无效的 JSON" }, 400);

        var code = body["code"]?.ToString()?.Trim();
        if (string.IsNullOrEmpty(code)) return new JsonResult(new { message = "缺少 code 字段" }, 400);

        try
        {
            // 以 case-insensitive 在数据库中查找 CDK（兼容历史数据）
            var all = await KaxGlobal.CdkDatabase.SelectAllAsync();
            var model = all.FirstOrDefault(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));
            if (model == null) return new JsonResult(new { contains = false });

            return new JsonResult(new
            {
                contains = true,
                data = new
                {
                    id = model.Id,
                    code = model.Code,
                    description = model.Description,
                    isUsed = model.IsUsed,
                    goldValue = model.GoldValue,
                    expiresInSeconds = model.ExpiresInSeconds,
                    createdAt = model.CreatedAt,
                    createdBy = model.CreatedBy,
                    usedAt = model.UsedAt,
                    usedBy = model.UsedBy
                }
            });
        }
        catch (Exception ex)
        {
            Logger.Error("Inspect CDK 错误: " + ex.Message);
            return new JsonResult(new { message = "查询失败" }, 500);
        }
    }

    /// <summary>
    /// 仅生成 CDK 代码（不保存），用于预览
    /// </summary>
    [HttpHandle("/api/cdk/admin/generate", "POST", RateLimitMaxRequests = 10, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_GenerateCdk(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        if (principal == null) return new JsonResult(new { message = "未授权" }, 401);
        var userName = principal.Identity?.Name;
        if (await KaxGlobal.IsUserBanned(userName!)) return new JsonResult(new { message = "账号被封禁" }, 403);
        if (!await IsCdkAdminUser(userName)) return new JsonResult(new { message = "权限不足" }, 403);

        if (string.IsNullOrEmpty(request.Body)) return new JsonResult(new { message = "请求体不能为空" }, 400);
        var body = JsonNode.Parse(request.Body);
        if (body == null) return new JsonResult(new { message = "无效的 JSON" }, 400);

        var prefix = body["prefix"]?.ToString() ?? string.Empty;
        int count = 1; int.TryParse(body["count"]?.ToString() ?? "1", out count);
        int length = 8; int.TryParse(body["length"]?.ToString() ?? "8", out length);
        count = Math.Clamp(count, 1, 1000);
        length = Math.Clamp(length, 4, 256);

        var codes = new List<string>(count);
        for (int i = 0; i < count; i++)
        {
            var codePart = ((prefix ?? string.Empty) + RandomString(length)).ToUpperInvariant();
            codes.Add(codePart);
        }

        return new JsonResult(new { codes = codes });
    }

    /// <summary>
    /// 生成并保存 CDK（金币类型），记录创建者
    /// </summary>
    [HttpHandle("/api/cdk/admin/save", "POST", RateLimitMaxRequests = 5, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_SaveCdk(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        if (principal == null) return new JsonResult(new { message = "未授权" }, 401);
        var userName = principal.Identity?.Name ?? "anonymous";
        if (await KaxGlobal.IsUserBanned(userName!)) return new JsonResult(new { message = "账号被封禁" }, 403);
        if (!await IsCdkAdminUser(userName)) return new JsonResult(new { message = "权限不足" }, 403);

        if (string.IsNullOrEmpty(request.Body)) return new JsonResult(new { message = "请求体不能为空" }, 400);
        var body = JsonNode.Parse(request.Body);
        if (body == null) return new JsonResult(new { message = "无效的 JSON" }, 400);

        var codes = new List<string>();
        var codesNode = body["codes"] as JsonArray;
        if (codesNode != null)
        {
            foreach (var it in codesNode) if (it != null) codes.Add(it.ToString());
        }
        else
        {
            var prefix = body["prefix"]?.ToString() ?? string.Empty;
            int count = 1; int.TryParse(body["count"]?.ToString() ?? "1", out count);
            int length = 8; int.TryParse(body["length"]?.ToString() ?? "8", out length);
            count = Math.Clamp(count, 1, 1000);
            length = Math.Clamp(length, 4, 256);

            // 支持通过保存接口直接以参数生成（payload 功能已移除）
            for (int i = 0; i < count; i++)
            {
                var baseCode = ((prefix ?? string.Empty) + RandomString(length)).ToUpperInvariant();
                codes.Add(baseCode);
            }
        }

        if (codes.Count == 0) return new JsonResult(new { message = "没有要保存的 CDK" }, 400);

        // 解析金币值、时效和描述
        int goldValue = 0;
        if (body["goldValue"] != null) int.TryParse(body["goldValue"]?.ToString() ?? "0", out goldValue);

        long expiresInSeconds = 0;
        if (body["expiresInSeconds"] != null) long.TryParse(body["expiresInSeconds"]?.ToString() ?? "0", out expiresInSeconds);

        var description = body["description"]?.ToString() ?? string.Empty;

        try
        {
            var saved = 0;

            // 使用直接表操作（避免将整表加载到内存）
            foreach (var raw in codes)
            {
                var normalized = (raw ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(normalized)) continue;

                // 以规范化（大写）形式判断是否已存在——这是最常见的存储格式，能用索引快速判断
                var normalizedUpper = normalized.ToUpperInvariant();
                var exists = (await KaxGlobal.CdkDatabase.SelectWhereAsync("Code", normalizedUpper)).Any();
                if (exists)
                    continue;

                // 回退检查：兼容历史可能存在的非规范大小写记录（仅在上面快速检查未命中时才做）
                var ciExisting = (await KaxGlobal.CdkDatabase.SelectAllAsync()).FirstOrDefault(c => string.Equals(c.Code, normalized, StringComparison.OrdinalIgnoreCase));
                if (ciExisting != null)
                    continue;

                var model = new Model.CdkModel
                {
                    Code = normalizedUpper,
                    Description = description,
                    IsUsed = false,
                    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    UsedAt = 0,
                    UsedBy = string.Empty,
                    CreatedBy = userName,
                    GoldValue = goldValue,
                    ExpiresInSeconds = expiresInSeconds
                };

                KaxGlobal.CdkDatabase.Insert(model);
                saved++;
            }

            Logger.Info($"用户 {userName} 保存了 {saved} 个 CDK");
            return new JsonResult(new { message = "已保存到数据库", count = saved }, saved > 0 ? 201 : 200);
        }
        catch (Exception ex)
        {
            Logger.Error("保存 CDK 时出错: " + ex.Message);
            return new JsonResult(new { message = "保存失败" }, 500);
        }
    }

    /// <summary>
    /// 删除 CDK（支持单个或批量）
    /// </summary>
    [HttpHandle("/api/cdk/admin/delete", "POST", RateLimitMaxRequests = 180, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_DeleteCdk(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        if (principal == null) return new JsonResult(new { message = "未授权" }, 401);
        var userName = principal.Identity?.Name;
        if (await KaxGlobal.IsUserBanned(userName!)) return new JsonResult(new { message = "账号被封禁" }, 403);
        if (!await IsCdkAdminUser(userName)) return new JsonResult(new { message = "权限不足" }, 403);

        if (string.IsNullOrEmpty(request.Body)) return new JsonResult(new { message = "请求体不能为空" }, 400);
        var body = JsonNode.Parse(request.Body);
        if (body == null) return new JsonResult(new { message = "无效的 JSON" }, 400);

        // 支持批量删除：请求体可包含单个字段 "code" 或数组字段 "codes"
        var codesNode = body["codes"] as JsonArray;
        var singleCode = body["code"]?.ToString()?.Trim();

        if ((codesNode == null || codesNode.Count == 0) && string.IsNullOrEmpty(singleCode))
            return new JsonResult(new { message = "缺少 code 或 codes 字段" }, 400);

        try
        {
            var all = await KaxGlobal.CdkDatabase.SelectAllAsync();

            // 结果汇总
            var totalRemoved = 0;
            var perCode = new List<object>();

            // 处理 codes 数组（优先）
            if (codesNode != null && codesNode.Count > 0)
            {
                foreach (var it in codesNode)
                {
                    if (it == null) continue;
                    var code = it.ToString()?.Trim();
                    if (string.IsNullOrEmpty(code))
                    {
                        perCode.Add(new { code = code, removed = 0, status = "invalid" });
                        continue;
                    }

                    var matches = all.Where(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (matches.Count == 0)
                    {
                        perCode.Add(new { code = code, removed = 0, status = "not_found" });
                        continue;
                    }

                    var removed = 0;
                    foreach (var m in matches)
                    {
                        KaxGlobal.CdkDatabase.DeleteById(m.Id);
                        removed++;
                    }
                    totalRemoved += removed;
                    perCode.Add(new { code = code, removed = removed, status = "ok" });
                }

                return new JsonResult(new { message = "批量删除完成", removed = totalRemoved, results = perCode });
            }

            // 处理单个 code
            var codeToDelete = singleCode!;
            var singleMatches = all.Where(c => string.Equals(c.Code, codeToDelete, StringComparison.OrdinalIgnoreCase)).ToList();
            if (singleMatches.Count == 0) return new JsonResult(new { message = "未找到指定 CDK" }, 404);

            var singleRemoved = 0;
            foreach (var m in singleMatches)
            {
                KaxGlobal.CdkDatabase.DeleteById(m.Id);
                singleRemoved++;
            }

            return new JsonResult(new { message = "删除成功", removed = singleRemoved });
        }
        catch (Exception ex)
        {
            Logger.Error("删除 CDK 时出错: " + ex.Message);
            return new JsonResult(new { message = "删除失败" }, 500);
        }
    }

    /// <summary>
    /// 获取 CDK 列表（支持分页）
    /// </summary>
    [HttpHandle("/api/cdk/admin/list", "GET", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Get_CdkList(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        if (principal == null) return new JsonResult(new { message = "未授权" }, 401);
        var userName = principal.Identity?.Name;
        if (!await IsCdkAdminUser(userName)) return new JsonResult(new { message = "权限不足" }, 403);

        try
        {
            int page = 1;
            int pageSize = 50;
            if (!int.TryParse(request.Query["page"], out page) || page <= 0) page = 1;
            if (!int.TryParse(request.Query["pageSize"], out pageSize) || pageSize <= 0) pageSize = 50;
            pageSize = Math.Clamp(pageSize, 1, 200);

            var all = await KaxGlobal.CdkDatabase.SelectAllAsync();
            var ordered = all.OrderByDescending(c => c.CreatedAt).ToList();
            var total = ordered.Count;

            var items = ordered.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(c => new
                {
                    id = c.Id,
                    code = c.Code,
                    description = c.Description,
                    isUsed = c.IsUsed,
                    goldValue = c.GoldValue,
                    expiresInSeconds = c.ExpiresInSeconds,
                    createdAt = c.CreatedAt,
                    createdBy = c.CreatedBy,
                    usedAt = c.UsedAt,
                    usedBy = c.UsedBy
                })
                .ToList<object>();

            return new JsonResult(new { data = items, page, pageSize, total });
        }
        catch (Exception ex)
        {
            Logger.Error("读取 CDK 列表时出错: " + ex.Message);
            return new JsonResult(new { message = "无法读取 CDK 列表" }, 500);
        }
    }

    /// <summary>
    /// 搜索 CDK（支持按代码、创建者、使用者、描述多字段搜索）
    /// </summary>
    [HttpHandle("/api/cdk/admin/search", "GET", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Get_SearchCdk(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        if (principal == null) return new JsonResult(new { message = "未授权" }, 401);
        var userName = principal.Identity?.Name;
        if (!await IsCdkAdminUser(userName)) return new JsonResult(new { message = "权限不足" }, 403);

        try
        {
            var keyword = (request.Query["keyword"] ?? string.Empty).Trim().ToLowerInvariant();
            // searchIn: code | creator | user | description | all (默认 all)
            var searchIn = (request.Query["searchIn"] ?? "all").Trim().ToLowerInvariant();

            int page = 1;
            int pageSize = 50;
            if (!int.TryParse(request.Query["page"], out page) || page <= 0) page = 1;
            if (!int.TryParse(request.Query["pageSize"], out pageSize) || pageSize <= 0) pageSize = 50;
            pageSize = Math.Clamp(pageSize, 1, 200);

            var all = await KaxGlobal.CdkDatabase.SelectAllAsync();
            IEnumerable<Model.CdkModel> filtered = all;

            if (!string.IsNullOrEmpty(keyword))
            {
                filtered = searchIn switch
                {
                    "code" => filtered.Where(c => (c.Code ?? string.Empty).ToLowerInvariant().Contains(keyword)),
                    "creator" => filtered.Where(c => (c.CreatedBy ?? string.Empty).ToLowerInvariant().Contains(keyword)),
                    "user" => filtered.Where(c => (c.UsedBy ?? string.Empty).ToLowerInvariant().Contains(keyword)),
                    "description" => filtered.Where(c => (c.Description ?? string.Empty).ToLowerInvariant().Contains(keyword)),
                    _ => filtered.Where(c =>
                        (c.Code ?? string.Empty).ToLowerInvariant().Contains(keyword) ||
                        (c.CreatedBy ?? string.Empty).ToLowerInvariant().Contains(keyword) ||
                        (c.UsedBy ?? string.Empty).ToLowerInvariant().Contains(keyword) ||
                        (c.Description ?? string.Empty).ToLowerInvariant().Contains(keyword))
                };
            }

            var ordered = filtered.OrderByDescending(c => c.CreatedAt).ToList();
            var total = ordered.Count;

            var items = ordered.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(c => new
                {
                    id = c.Id,
                    code = c.Code,
                    description = c.Description,
                    isUsed = c.IsUsed,
                    goldValue = c.GoldValue,
                    expiresInSeconds = c.ExpiresInSeconds,
                    createdAt = c.CreatedAt,
                    createdBy = c.CreatedBy,
                    usedAt = c.UsedAt,
                    usedBy = c.UsedBy
                })
                .ToList<object>();

            return new JsonResult(new { data = items, page, pageSize, total, keyword, searchIn });
        }
        catch (Exception ex)
        {
            Logger.Error("搜索 CDK 时出错: " + ex.Message);
            return new JsonResult(new { message = "搜索失败" }, 500);
        }
    }

    #endregion
}

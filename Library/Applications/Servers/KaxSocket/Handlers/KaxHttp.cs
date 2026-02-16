using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Drx.Sdk.Network.V2.Web;
using Drx.Sdk.Shared;
using Drx.Sdk.Shared.Utility;

namespace KaxSocket.Handlers;

public class KaxHttp
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(KaxHttp))]
    public KaxHttp()
    {
    }

    static KaxHttp()
    {
        // 配置 JWT
        JwtHelper.Configure(new JwtHelper.JwtConfig
        {
            SecretKey = "A1b2C3d4E5f6G7h8I9j0K1l2M3n4O5p6", // 建议使用环境变量
            Issuer = "KaxSocket",
            Audience = "KaxUsers",
            Expiration = TimeSpan.FromHours(1)
        });
    }

    #region Rate Limit Callback


    public static HttpResponse RateLimitCallback(int count, HttpRequest request, OverrideContext overrideContext)
    {
        if (count > 20)
        {
            var userToken = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
            var userName = JwtHelper.ValidateToken(userToken!)?.Identity?.Name ?? "未知用户";
            _ = KaxGlobal.BanUser(userName, "短时间内请求过于频繁，自动封禁。", 60); // 封禁 1 分钟
            return new HttpResponse(429, "请求过于频繁，您的账号暂时被封禁。");
        }
        else
        {
            Logger.Warn($"请求过于频繁: {request.Method} {request.Path} from {request.ClientAddress.Ip}:{request.ClientAddress.Port}");
            return new HttpResponse(429, "请求过于频繁，请稍后再试。");
        }
    }

    #endregion








    #region HTTP Handlers

    // 检查当前用户是否属于允许使用 CDK 管理 API 的权限组（Console/Root/Admin）
    private static async Task<bool> IsCdkAdminUser(string? userName)
    {
        if (string.IsNullOrWhiteSpace(userName)) return false;
        var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (user == null) return false;
        var g = user.PermissionGroup;
        return g == UserPermissionGroup.Console || g == UserPermissionGroup.Root || g == UserPermissionGroup.Admin;
    }

    [HttpMiddleware]
    public static HttpResponse Echo(HttpRequest request, Func<HttpRequest, HttpResponse> next)
    {
        Logger.Info($"收到 HTTP 请求: {request.Method} {request.Path} from {request.ClientAddress.Ip}:{request.ClientAddress.Port}");
        // 继续处理请求
        return next(request);
    }

    [HttpHandle("/api/user/register", "POST", RateLimitMaxRequests = 3, RateLimitWindowSeconds = 60)]
    public static async Task<HttpResponse> PostRegister(HttpRequest request)
    {
        if (request.Body == null)
        {
            Logger.Error("注册请求体为空。");
            return new HttpResponse()
            {
                StatusCode = 400,
                Body = "请求体不能为空。",
            };
        }

        Logger.Info("处理用户注册请求...");

        try
        {
            var bodyJson = JsonNode.Parse(request.Body);
            if (bodyJson == null)
            {
                Logger.Error("注册请求体格式错误，无法解析为 JSON。");
                return new HttpResponse()
                {
                    StatusCode = 400,
                    Body = "服务器错误，无法解析请求体。",
                };
            }

            // 处理注册逻辑
            var userName = bodyJson["username"]?.ToString();
            var password = bodyJson["password"]?.ToString();
            var email = bodyJson["email"]?.ToString();

            // 验证用户名、密码是否合规（用户名 = 5~12 字符，密码 >= 8 字符）
            if (string.IsNullOrEmpty(userName) || userName.Length < 5 || userName.Length > 12)
            {
                return new HttpResponse()
                {
                    StatusCode = 400,
                    Body = "用户名长度应为 5~12 字符。",
                };
            }
            if (string.IsNullOrEmpty(password) || password.Length < 8)
            {
                return new HttpResponse()
                {
                    StatusCode = 400,
                    Body = "密码长度应至少为 8 字符。",
                };
            }
            if (string.IsNullOrEmpty(email) || !CommonUtility.IsValidEmail(email))
            {
                return new HttpResponse()
                {
                    StatusCode = 400,
                    Body = "无效的电子邮箱地址。",
                };
            }

            // 处理注册逻辑
            // 检查用户名或邮箱是否已被注册
            var byName = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
            var byEmail = (await KaxGlobal.UserDatabase.SelectWhereAsync("Email", email)).FirstOrDefault();
            var userExists = byName ?? byEmail;
            if (userExists != null)
            {
                return new HttpResponse()
                {
                    StatusCode = 409,
                    Body = "用户名或电子邮箱已被注册。",
                };
            }

            // 保存用户数据到数据库
            KaxGlobal.UserDatabase.Insert(new UserData()
            {
                UserName = userName,
                PasswordHash = CommonUtility.ComputeSHA256Hash(password),
                Email = email,
            });

            return new HttpResponse()
            {
                StatusCode = 201,
                Body = "注册成功。",
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"处理注册请求时发生错误: {ex.Message}");
            return new HttpResponse()
            {
                StatusCode = 500,
                Body = "服务器错误，无法处理请求。",
            };
        }
    }

    [HttpHandle("/api/user/login", "POST", RateLimitMaxRequests = 5, RateLimitWindowSeconds = 60)]
    public static async Task<HttpResponse> PostLogin(HttpRequest request, DrxHttpServer server)
    {
        if (request.Body == null)
        {
            Logger.Error("登录请求体为空。");
            return new HttpResponse()
            {
                StatusCode = 400,
                Body = "请求体不能为空。",
            };
        }

        var bodyJson = JsonNode.Parse(request.Body);
        if (bodyJson == null)
        {
            Logger.Error("登录请求体格式错误，无法解析为 JSON。");
            return new HttpResponse()
            {
                StatusCode = 400,
                Body = "服务器错误，无法解析请求体。",
            };
        }

        // 处理登录逻辑
        var userName = bodyJson["username"]?.ToString();
        var password = bodyJson["password"]?.ToString();

        if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
        {
            return new HttpResponse()
            {
                StatusCode = 400,
                Body = "用户名和密码不能为空。",
            };
        }
        var userExists = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (userExists != null && userExists.PasswordHash == CommonUtility.ComputeSHA256Hash(password))
        {
            var token = KaxGlobal.GenerateLoginToken(userExists);
            userExists.LastLoginAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await KaxGlobal.UserDatabase.UpdateAsync(userExists);

            var resp = new HttpResponse()
            {
                StatusCode = 200,
                Body = new JsonObject
                {
                    ["message"] = "登录成功。",
                    ["login_token"] = token
                }.ToJsonString(),
            };

            return resp;
        }
        else
        {
            Logger.Warn($"用户登录失败，用户名或密码错误：{userName}");
            return new HttpResponse()
            {
                StatusCode = 401,
                Body = "用户名或密码错误。",
            };
        }
    }

    [HttpHandle("/api/user/verify/account", "POST", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_Verify(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        if (principal == null)
        {
            return new JsonResult(new
            {
                message = "无效的登录令牌。",
            }, 401);
        }

        var userName = principal.Identity?.Name;

        var userModel = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (userModel == null)
        {
            return new JsonResult(new
            {
                message = "令牌对应的用户不存在。",
            }, 404);
        }

        if (await KaxGlobal.IsUserBanned(userName!))
        {
            return new JsonResult(new
            {
                message = "您的账号已被封禁，无法访问此资源。",
            }, 403);
        }

        // 返回额外的权限信息，前端可据此决定是否显示管理员入口
        var permissionGroup = userModel.PermissionGroup;
        var isAdmin = permissionGroup == UserPermissionGroup.Console || permissionGroup == UserPermissionGroup.Root || permissionGroup == UserPermissionGroup.Admin;

        return new JsonResult(new
        {
            message = "令牌有效，欢迎您！",
            user = userName,
            permissionGroup = (int)permissionGroup,
            isAdmin = isAdmin
        });
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

    // --------------------------------------------------
    // CDK：生成 / 保存 / 列表
    // - POST /api/cdk/generate  -> 返回 codes
    // - POST /api/cdk/save      -> 保存到服务器（接受 codes[] 或生成参数）
    // - GET  /api/cdk/list      -> 返回最近的 CDK 列表（最多 200 条）
    // --------------------------------------------------

    private static string RandomString(int length, string? charset = null)
    {
        if (string.IsNullOrEmpty(charset)) charset = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var chars = charset.ToCharArray();
        var sb = new StringBuilder(length);
        using var rng = RandomNumberGenerator.Create();
        var buf = new byte[4];
        for (int i = 0; i < length; i++)
        {
            rng.GetBytes(buf);
            uint v = BitConverter.ToUInt32(buf, 0);
            sb.Append(chars[(int)(v % (uint)chars.Length)]);
        }
        return sb.ToString();
    }

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
                mapped = new
                {
                    assetId = model.AssetId,
                    description = model.Description,
                    iat = model.CreatedAt,
                    isUsed = model.IsUsed,
                    usedBy = model.UsedBy
                },
                claims = new { }
            });
        }
        catch (Exception ex)
        {
            Logger.Error("Inspect CDK 错误: " + ex.Message);
            return new JsonResult(new { message = "查询失败" }, 500);
        }
    }

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

        // 解析 assetId 与 description（assetId 建议为大于 0 的整数）
        int assetId = 0;
        if (body["assetId"] != null) int.TryParse(body["assetId"]?.ToString() ?? "0", out assetId);
        var description = body["description"]?.ToString() ?? string.Empty;

        // 强制校验 assetId（客户端已验证，但后端再校验以确保数据完整）
        if (assetId <= 0) return new JsonResult(new { message = "assetId 必须是大于 0 的整数" }, 400);

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
                    Description = description ?? string.Empty,
                    IsUsed = false,
                    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    UsedAt = 0,
                    UsedBy = string.Empty,
                    AssetId = assetId
                };

                KaxGlobal.CdkDatabase.Insert(model);
                saved++;
            }

            return new JsonResult(new { message = "已保存到数据库", count = saved }, saved > 0 ? 201 : 200);
        }
        catch (Exception ex)
        {
            Logger.Error("保存 CDK 时出错: " + ex.Message);
            return new JsonResult(new { message = "保存失败" }, 500);
        }
    }

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

        var code = body["code"]?.ToString()?.Trim();
        if (string.IsNullOrEmpty(code)) return new JsonResult(new { message = "缺少 code 字段" }, 400);

        try
        {
            // 在数据库中以大小写不敏感方式查找并删除匹配项
            var all = await KaxGlobal.CdkDatabase.SelectAllAsync();
            var matches = all.Where(c => string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count == 0) return new JsonResult(new { message = "未找到指定 CDK" }, 404);

            var removed = 0;
            foreach (var m in matches)
            {
                KaxGlobal.CdkDatabase.DeleteById(m.Id);
                removed++;
            }

            return new JsonResult(new { message = "删除成功", removed = removed });
        }
        catch (Exception ex)
        {
            Logger.Error("删除 CDK 时出错: " + ex.Message);
            return new JsonResult(new { message = "删除失败" }, 500);
        }
    }

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
            var all = await KaxGlobal.CdkDatabase.SelectAllAsync();
            var items = all.OrderByDescending(c => c.CreatedAt).Take(200)
                .Select(c => new { code = c.Code, isUsed = c.IsUsed, createdAt = c.CreatedAt, assetId = c.AssetId, description = c.Description })
                .ToList<object>();
            return new JsonResult(items);
        }
        catch (Exception ex)
        {
            Logger.Error("读取 CDK 列表时出错: " + ex.Message);
            return new JsonResult(new { message = "无法读取 CDK 列表" }, 500);
        }
    }

    // --------------------------------------------------
    // Asset：创建 / 修改 / 查询 / 删除 / 列表
    // - POST /api/asset/admin/create   -> 创建资源
    // - POST /api/asset/admin/update   -> 修改资源
    // - POST /api/asset/admin/inspect  -> 查询单个资源
    // - POST /api/asset/admin/delete   -> 删除资源
    // - GET  /api/asset/admin/list     -> 返回资源列表
    // --------------------------------------------------

    /// <summary>
    /// 检查当前用户是否属于允许使用 Asset 管理 API 的权限组（Console/Root/Admin）
    /// </summary>
    private static async Task<bool> IsAssetAdminUser(string? userName)
    {
        if (string.IsNullOrWhiteSpace(userName)) return false;
        var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (user == null) return false;
        var g = user.PermissionGroup;
        return g == UserPermissionGroup.Console || g == UserPermissionGroup.Root || g == UserPermissionGroup.Admin;
    }

    [HttpHandle("/api/asset/admin/create", "POST", RateLimitMaxRequests = 10, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_CreateAsset(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        if (principal == null) return new JsonResult(new { message = "无效的登录令牌" }, 401);

        var userName = principal.Identity?.Name;
        if (await KaxGlobal.IsUserBanned(userName!))
            return new JsonResult(new { message = "您的账号已被封禁" }, 403);
        if (!await IsAssetAdminUser(userName))
            return new JsonResult(new { message = "权限不足" }, 403);

        if (string.IsNullOrEmpty(request.Body))
            return new JsonResult(new { message = "请求体不能为空" }, 400);

        try
        {
            var body = JsonNode.Parse(request.Body);
            if (body == null) return new JsonResult(new { message = "无法解析请求体" }, 400);

            var name = body["name"]?.ToString();
            var version = body["version"]?.ToString();
            var author = body["author"]?.ToString();
            var description = body["description"]?.ToString() ?? "";

            if (string.IsNullOrEmpty(name) || name.Length > 100)
                return new JsonResult(new { message = "资源名称无效（1-100字符）" }, 400);
            if (string.IsNullOrEmpty(version) || version.Length > 50)
                return new JsonResult(new { message = "版本字段无效（1-50字符）" }, 400);
            if (string.IsNullOrEmpty(author) || author.Length > 100)
                return new JsonResult(new { message = "作者字段无效（1-100字符）" }, 400);
            if (description.Length > 500)
                return new JsonResult(new { message = "描述过长（最多500字符）" }, 400);

            var asset = new Model.AssetModel
            {
                Name = name,
                Version = version,
                Author = author,
                Description = description,
                LastUpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            KaxGlobal.AssetDataBase.Insert(asset);

            Logger.Info($"用户 {userName} 创建了资源: {name} (v{version})");
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
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        if (principal == null) return new JsonResult(new { message = "无效的登录令牌" }, 401);

        var userName = principal.Identity?.Name;
        if (await KaxGlobal.IsUserBanned(userName!))
            return new JsonResult(new { message = "您的账号已被封禁" }, 403);
        if (!await IsAssetAdminUser(userName))
            return new JsonResult(new { message = "权限不足" }, 403);

        if (string.IsNullOrEmpty(request.Body))
            return new JsonResult(new { message = "请求体不能为空" }, 400);

        try
        {
            var body = JsonNode.Parse(request.Body);
            if (body == null) return new JsonResult(new { message = "无法解析请求体" }, 400);

            if (!int.TryParse(body["id"]?.ToString(), out var id) || id <= 0)
                return new JsonResult(new { message = "资源ID无效" }, 400);

            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(id);
            if (asset == null)
                return new JsonResult(new { message = "资源不存在" }, 404);

            var version = body["version"]?.ToString();
            var author = body["author"]?.ToString();
            var description = body["description"]?.ToString() ?? "";

            if (!string.IsNullOrEmpty(version) && version.Length > 50)
                return new JsonResult(new { message = "版本字段无效（最多50字符）" }, 400);
            if (!string.IsNullOrEmpty(author) && author.Length > 100)
                return new JsonResult(new { message = "作者字段无效（最多100字符）" }, 400);
            if (description.Length > 500)
                return new JsonResult(new { message = "描述过长（最多500字符）" }, 400);

            if (!string.IsNullOrEmpty(version)) asset.Version = version;
            if (!string.IsNullOrEmpty(author)) asset.Author = author;
            asset.Description = description;
            asset.LastUpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            await KaxGlobal.AssetDataBase.UpdateAsync(asset);

            Logger.Info($"用户 {userName} 修改了资源: {asset.Name} (id={id})");
            return new JsonResult(new { message = "资源已更新" });
        }
        catch (Exception ex)
        {
            Logger.Error($"更新资源失败: {ex.Message}");
            return new JsonResult(new { message = "服务器错误" }, 500);
        }
    }

    [HttpHandle("/api/asset/admin/inspect", "POST", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_InspectAsset(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        if (principal == null) return new JsonResult(new { message = "无效的登录令牌" }, 401);

        var userName = principal.Identity?.Name;
        if (await KaxGlobal.IsUserBanned(userName!))
            return new JsonResult(new { message = "您的账号已被封禁" }, 403);
        if (!await IsAssetAdminUser(userName))
            return new JsonResult(new { message = "权限不足" }, 403);

        if (string.IsNullOrEmpty(request.Body))
            return new JsonResult(new { message = "请求体不能为空" }, 400);

        try
        {
            var body = JsonNode.Parse(request.Body);
            if (body == null) return new JsonResult(new { message = "无法解析请求体" }, 400);

            if (!int.TryParse(body["id"]?.ToString(), out var id) || id <= 0)
                return new JsonResult(new { message = "资源ID无效" }, 400);

            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(id);
            if (asset == null)
                return new JsonResult(new { message = "资源不存在" }, 404);

            return new JsonResult(new
            {
                data = new
                {
                    id = asset.Id,
                    name = asset.Name,
                    version = asset.Version,
                    author = asset.Author,
                    description = asset.Description,
                    lastUpdatedAt = asset.LastUpdatedAt,
                    isDeleted = asset.IsDeleted,
                    deletedAt = asset.DeletedAt
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
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        if (principal == null) return new JsonResult(new { message = "无效的登录令牌" }, 401);

        var userName = principal.Identity?.Name;
        if (await KaxGlobal.IsUserBanned(userName!))
            return new JsonResult(new { message = "您的账号已被封禁" }, 403);
        if (!await IsAssetAdminUser(userName))
            return new JsonResult(new { message = "权限不足" }, 403);

        if (string.IsNullOrEmpty(request.Body))
            return new JsonResult(new { message = "请求体不能为空" }, 400);

        try
        {
            var body = JsonNode.Parse(request.Body);
            if (body == null) return new JsonResult(new { message = "无法解析请求体" }, 400);

            if (!int.TryParse(body["id"]?.ToString(), out var id) || id <= 0)
                return new JsonResult(new { message = "资源ID无效" }, 400);

            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(id);
            if (asset == null)
                return new JsonResult(new { message = "资源不存在" }, 404);

            // 软删除：标记为已删除并记录时间
            asset.IsDeleted = true;
            asset.DeletedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            asset.LastUpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await KaxGlobal.AssetDataBase.UpdateAsync(asset);

            Logger.Info($"用户 {userName} 软删除了资源: {asset.Name} (id={id})");
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
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        if (principal == null) return new JsonResult(new { message = "无效的登录令牌" }, 401);

        var userName = principal.Identity?.Name;
        if (await KaxGlobal.IsUserBanned(userName!))
            return new JsonResult(new { message = "您的账号已被封禁" }, 403);
        if (!await IsAssetAdminUser(userName))
            return new JsonResult(new { message = "权限不足" }, 403);

        if (string.IsNullOrEmpty(request.Body))
            return new JsonResult(new { message = "请求体不能为空" }, 400);

        try
        {
            var body = JsonNode.Parse(request.Body);
            if (body == null) return new JsonResult(new { message = "无法解析请求体" }, 400);

            if (!int.TryParse(body["id"]?.ToString(), out var id) || id <= 0)
                return new JsonResult(new { message = "资源ID无效" }, 400);

            var asset = await KaxGlobal.AssetDataBase.SelectByIdAsync(id);
            if (asset == null)
                return new JsonResult(new { message = "资源不存在" }, 404);

            asset.IsDeleted = false;
            asset.DeletedAt = 0;
            asset.LastUpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await KaxGlobal.AssetDataBase.UpdateAsync(asset);

            Logger.Info($"用户 {userName} 恢复了资源: {asset.Name} (id={id})");
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
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        if (principal == null) return new JsonResult(new { message = "无效的登录令牌" }, 401);

        var userName = principal.Identity?.Name;
        if (!await IsAssetAdminUser(userName))
            return new JsonResult(new { message = "权限不足" }, 403);

        try
        {
            // 支持查询参数：q (搜索), author, version, page, pageSize, includeDeleted
            var q = request.Query[("q")] ?? string.Empty;
            var authorFilter = request.Query[("author")] ?? string.Empty;
            var versionFilter = request.Query[("version")] ?? string.Empty;
            var includeDeleted = (request.Query[("includeDeleted")] ?? "false").ToLower() == "true";
            int page = 1;
            int pageSize = 20;
            if (!int.TryParse(request.Query[("page")], out page) || page <= 0) page = 1;
            if (!int.TryParse(request.Query[("pageSize")], out pageSize) || pageSize <= 0) pageSize = 20;

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
                    || (a.Author ?? string.Empty).ToLowerInvariant().Contains(qlow)
                    || (a.Version ?? string.Empty).ToLowerInvariant().Contains(qlow)).AsQueryable();
            }
            if (!string.IsNullOrEmpty(authorFilter))
            {
                var alow = authorFilter.ToLowerInvariant();
                filtered = filtered.Where(a => (a.Author ?? string.Empty).ToLowerInvariant().Contains(alow)).AsQueryable();
            }
            if (!string.IsNullOrEmpty(versionFilter))
            {
                var vlow = versionFilter.ToLowerInvariant();
                filtered = filtered.Where(a => (a.Version ?? string.Empty).ToLowerInvariant().Contains(vlow)).AsQueryable();
            }

            var total = filtered.Count();
            var items = filtered.OrderByDescending(a => a.LastUpdatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(a => new
                {
                    id = a.Id,
                    name = a.Name,
                    version = a.Version,
                    author = a.Author,
                    description = a.Description,
                    lastUpdatedAt = a.LastUpdatedAt,
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
}
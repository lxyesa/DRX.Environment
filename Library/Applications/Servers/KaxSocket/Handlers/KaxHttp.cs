using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Json.Nodes;
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



    #region 辅助方法

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

    private static string GenerateLoginToken(UserData user)
    {
        return JwtHelper.GenerateToken(user.Id.ToString(), user.UserName, user.Email);
    }

    private static ClaimsPrincipal ValidateToken(string token)
    {
        return JwtHelper.ValidateToken(token);
    }


    private static bool IsUserBanned(string userName)
    {
        var user = KaxGlobal.UserDatabase.QueryFirstAsync(u => u.UserName == userName).Result;
        if (user != null)
        {
            Logger.Info($"检查用户 {userName} 的封禁状态: IsBanned={user.Status.IsBanned}");
            Logger.Info($"封禁过期时间: {user.Status.BanExpiresAt}, 当前时间: {DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");
            Logger.Info($"封禁原因: {user.Status.BanReason}");

            if (user.Status.IsBanned)
            {
                // 检查封禁是否已过期
                var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                Logger.Info($"当前时间戳: {currentTime}, 用户封禁到期时间戳: {user.Status.BanExpiresAt}");
                if (user.Status.BanExpiresAt > 0 && currentTime >= user.Status.BanExpiresAt)
                {
                    UnBanUser(userName).Wait();
                    Logger.Info($"用户 {user.UserName} 的封禁已过期，已自动解除封禁。");
                    return false;
                }
                return true;
            }
        }
        else
        {
            Logger.Warn($"检查封禁状态时未找到用户：{userName}");
        }
        return false;
    }

    private static async Task BanUser(string userName, string reason, long durationSeconds)
    {
        var user = KaxGlobal.UserDatabase.QueryFirstAsync(u => u.UserName == userName).Result;
        if (user != null && !user.Status.IsBanned)
        {
            user.Status.IsBanned = true;
            user.Status.BannedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            user.Status.BanExpiresAt = user.Status.BannedAt + durationSeconds;
            user.Status.BanReason = reason;
            _ = await KaxGlobal.UserDatabase.EditWhereAsync(u => u.Id == user.Id, user);
            Logger.Info($"已封禁用户 {user.UserName}，原因：{reason}，持续时间：{durationSeconds} 秒。");
        }
    }

    private static async Task UnBanUser(string userName)
    {
        var user = await KaxGlobal.UserDatabase.QueryFirstAsync(u => u.UserName == userName);
        if (user != null && user.Status.IsBanned)
        {
            user.Status.IsBanned = false;
            user.Status.BanExpiresAt = 0;
            user.Status.BanReason = string.Empty;
            _ = KaxGlobal.UserDatabase.EditWhereAsync(u => u.Id == user.Id, user);
            Logger.Info($"已解除用户 {user.UserName} 的封禁状态。");
        }
    }

    #endregion







    #region Rate Limit Callback


    public static HttpResponse RateLimitCallback(int count, HttpRequest request, OverrideContext overrideContext)
    {
        if (count > 20)
        {
            var userToken = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
            var userName = JwtHelper.ValidateToken(userToken!)?.Identity?.Name ?? "未知用户";
            _ = BanUser(userName, "短时间内请求过于频繁，自动封禁。", 60); // 封禁 1 分钟
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

    [HttpMiddleware]
    public static HttpResponse Echo(HttpRequest request, Func<HttpRequest, HttpResponse> next)
    {
        Logger.Info($"收到 HTTP 请求: {request.Method} {request.Path} from {request.ClientAddress.Ip}:{request.ClientAddress.Port}");
        // 继续处理请求
        return next(request);
    }

    [HttpHandle("/api/user/register", "POST", RateLimitMaxRequests = 3, RateLimitWindowSeconds = 60)]
    public static HttpResponse PostRegister(HttpRequest request)
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
            var userExists = KaxGlobal.UserDatabase.QueryFirstAsync(u => u.UserName == userName || u.Email == email).Result;
            if (userExists != null)
            {
                return new HttpResponse()
                {
                    StatusCode = 409,
                    Body = "用户名或电子邮箱已被注册。",
                };
            }

            // 保存用户数据到数据库
            _ = KaxGlobal.UserDatabase.PushAsync(new UserData()
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

        var userExists = KaxGlobal.UserDatabase.QueryFirstAsync(u => u.UserName == userName && u.PasswordHash == CommonUtility.ComputeSHA256Hash(password)).Result;
        if (userExists != null)
        {
            var token = GenerateLoginToken(userExists);
            userExists.LastLoginAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await KaxGlobal.UserDatabase.EditWhereAsync(u => u.Id == userExists.Id, userExists);

            // 使用会话机制：获取或创建会话并在会话中保存用户信息
            try
            {
                var session = server?.SessionManager.GetOrCreateSession(request.Session?.Id);
                if (session != null)
                {
                    session.Data["userId"] = userExists.Id;
                    session.Data["userName"] = userExists.UserName;
                    session.UpdateAccess();
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"尝试写入会话时发生错误: {ex.Message}");
            }

            return new HttpResponse()
            {
                StatusCode = 200,
                Body = new JsonObject
                {
                    ["message"] = "登录成功。",
                    ["login_token"] = token,
                    ["session_id"] = request.Session?.Id ?? server?.SessionManager.GetOrCreateSession(null)?.Id
                }.ToJsonString(),
            };
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

    [HttpHandle("/api/token/test", "POST", RateLimitMaxRequests = 5, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static IActionResult Post_TestToken(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = ValidateToken(token!);
        if (principal == null)
        {
            return new JsonResult(new
            {
                message = "无效的登录令牌。",
            }, 401);
        }

        var userName = principal.Identity?.Name;

        if (KaxGlobal.UserDatabase.QueryFirstAsync(u => u.UserName == userName).Result == null)
        {
            return new JsonResult(new
            {
                message = "令牌对应的用户不存在。",
            }, 404);
        }

        if (IsUserBanned(userName!))
        {
            return new JsonResult(new
            {
                message = "您的账号已被封禁，无法访问此资源。",
            }, 403);
        }

        return new JsonResult(new
        {
            message = "令牌有效，欢迎您！",
            user = userName,
        });
    }

    /*
    * 测试用的受保护路由，需提供有效的登录令牌才能访问
    * 用于测试登录令牌的有效性
    */
    [HttpHandle("/api/hello", "GET", RateLimitMaxRequests = 1, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static IActionResult Get_SayHello(HttpRequest request)
    {
        try
        {
            var principal = JwtHelper.ValidateTokenFromRequest(request);
            if (principal == null)
            {
                return new JsonResult(new
                {
                    message = "无效的登录令牌。",
                }, 401);
            }

            var userName = principal.Identity?.Name;
            if (IsUserBanned(userName!))
            {
                return new JsonResult(new
                {
                    message = "您的账号已被封禁，无法访问此资源。",
                }, 403);
            }
            return new JsonResult(new
            {
                message = $"Hello, {userName}! Your token is valid.",
            }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error("验证令牌时发生异常: " + ex.Message);
            return new JsonResult(new
            {
                message = "服务器错误，无法处理请求。",
            }, 500);
        }
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
            _ = UnBanUser(userName);
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
    
    [HttpHandle("/api/session/test", "GET")]
    public static IActionResult Get_SessionTest(HttpRequest request)
    {
        try
        {
            var session = request.Session;
            if (session == null)
            {
                return new JsonResult(new
                {
                    message = "未检测到会话，请确保已在服务器端启用会话中间件并在登录时获得会话。"
                }, 401);
            }

            // 将会话内的简单键值复制到可序列化对象
            var dict = new System.Collections.Generic.Dictionary<string, object?>();
            foreach (var kv in session.Data)
            {
                dict[kv.Key] = kv.Value;
            }

            return new JsonResult(new
            {
                message = "会话有效",
                session_id = session.Id,
                created = session.Created,
                last_access = session.LastAccess,
                data = dict
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"读取会话时发生错误: {ex.Message}");
            return new JsonResult(new { message = "服务器错误，无法读取会话" }, 500);
        }
    }

    #endregion
}
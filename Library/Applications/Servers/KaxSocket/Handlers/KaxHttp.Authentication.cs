using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Drx.Sdk.Network.Http;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.Results;
using Drx.Sdk.Network.Http.Configs;
using Drx.Sdk.Shared;
using Drx.Sdk.Shared.Utility;
using KaxSocket;

namespace KaxSocket.Handlers;

/// <summary>
/// 用户认证模块 - 处理用户注册、登录和令牌验证
/// </summary>
public partial class KaxHttp
{
    #region 用户认证 (User Authentication)

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

        var userName = principal.Identity?.Name ?? "unknown user";

        var userModel = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName ?? "null")).FirstOrDefault();
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
        var isAdmin = permissionGroup == UserPermissionGroup.System || permissionGroup == UserPermissionGroup.Console || permissionGroup == UserPermissionGroup.Admin;

        // 如果服务器上存在已上传的头像文件，返回可访问的 avatarUrl（供前端直接使用）
        string avatarUrl = string.Empty;
        try
        {
            var avatarPath = KaxGlobal.GetUserAvatarPathById(userModel.Id);
            if (!string.IsNullOrEmpty(avatarPath))
            {
                var stamp = userModel.LastLoginAt > 0 ? userModel.LastLoginAt : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                avatarUrl = $"/api/user/avatar/{userModel.Id}?v={stamp}";
            }
        }
        catch { /* 忽略异常，保持兼容 */ }

        var _uname = userName ?? string.Empty;
        return new JsonResult(new
        {
            message = "令牌有效，欢迎您！",
            user = userName,
            permissionGroup = (int)permissionGroup,
            isAdmin = isAdmin,
            avatarUrl = avatarUrl,
            // 兼容：返回用户统计摘要，方便 topbar/前端统一使用
            resourceCount = await KaxGlobal.GetUserResourceCountAsync(_uname),
            gold = await KaxGlobal.GetUserGoldAsync(_uname),
            recentActivity = await KaxGlobal.GetUserRecentActivityAsync(_uname),
            cdkCount = await KaxGlobal.GetUserCdkCountAsync(_uname)
        });
    }

    #endregion
}

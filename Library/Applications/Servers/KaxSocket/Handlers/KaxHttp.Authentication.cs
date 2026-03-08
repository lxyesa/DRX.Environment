using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Drx.Sdk.Network.Http;
using Drx.Sdk.Network.Http.Api;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.Results;
using Drx.Sdk.Network.Http.Configs;
using Drx.Sdk.Network.Http.Auth;
using Drx.Sdk.Shared;
using Drx.Sdk.Shared.Utility;
using KaxSocket;
using KaxSocket.Handlers.Helpers;

namespace KaxSocket.Handlers;

/// <summary>
/// 用户认证模块 - 处理用户注册、登录和令牌验证
/// </summary>
public partial class KaxHttp
{
    /// <summary>
    /// 从当前请求提取 Bearer Token 并撤销，供敏感操作（如邮箱变更）成功后收口会话使用。
    /// </summary>
    private static void RevokeTokenFromRequest(HttpRequest request)
    {
        try
        {
            var token = ApiGuard.ExtractBearerToken(request);
            if (!string.IsNullOrWhiteSpace(token))
            {
                JwtHelper.RevokeToken(token);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"撤销当前请求令牌失败: {ex.Message}");
        }
    }

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
        var loginValue = bodyJson["username"]?.ToString();
        var password = bodyJson["password"]?.ToString();
        var loginType = (request.Headers["type"] ?? "web").Trim().ToLowerInvariant();
        var hid = (request.Headers["hid"] ?? string.Empty).Trim();
        var deviceName = (request.Headers["device-name"] ?? string.Empty).Trim();
        var deviceOs = (request.Headers["device-os"] ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(loginValue) || string.IsNullOrEmpty(password))
        {
            return new HttpResponse()
            {
                StatusCode = 400,
                Body = "用户名/邮箱和密码不能为空。",
            };
        }

        if (loginType == "client" && string.IsNullOrWhiteSpace(hid))
        {
            return new HttpResponse()
            {
                StatusCode = 400,
                Body = "客户端登录缺少 hid 请求头。",
            };
        }

        // 支持用户名或邮箱登录
        UserData userExists = null;
        if (CommonUtility.IsValidEmail(loginValue))
        {
            // 按邮箱查询
            userExists = (await KaxGlobal.UserDatabase.SelectWhereAsync("Email", loginValue)).FirstOrDefault();
        }
        else
        {
            // 按用户名查询
            userExists = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", loginValue)).FirstOrDefault();
        }

        if (userExists != null && userExists.PasswordHash == CommonUtility.ComputeSHA256Hash(password))
        {
            // 检查封禁状态
            if (await KaxGlobal.IsUserBanned(userExists))
            {
                var banReason = string.IsNullOrWhiteSpace(userExists.Status.BanReason) ? "违反服务条款" : userExists.Status.BanReason;
                var banExpireDesc = userExists.Status.BanExpiresAt > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(userExists.Status.BanExpiresAt).ToString("yyyy-MM-dd HH:mm:ss") + " (UTC)"
                    : "永久";
                Logger.Warn($"已封禁用户 {userExists.UserName} 尝试登录，已拒绝。");
                return new HttpResponse()
                {
                    StatusCode = 403,
                    Body = new JsonObject
                    {
                        ["message"] = "您的账号已被封禁，无法登录。",
                        ["ban_reason"] = banReason,
                        ["ban_expires"] = banExpireDesc,
                    }.ToJsonString(),
                };
            }

            var nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            // TokenInvalidBefore 仅由密码重置等全局失效场景写入，登录时不再推进，
            // 以允许客户端与 Web 端同时在线、Web 多端并存。
            userExists.LastLoginAt = nowSeconds;
            var isClientLogin = string.Equals(loginType, "client", StringComparison.OrdinalIgnoreCase);
            var (clientToken, webToken, webTokenRotated) = KaxGlobal.ResolveLoginTokens(userExists, isClientLogin, hid, deviceName, deviceOs);
            userExists.ClientToken = clientToken;
            userExists.WebToken = webToken;

            // 写入登录设备记录：客户端登录使用 hid，Web 登录使用 User-Agent 哈希作为唯一键
            var recordDevice = isClientLogin ? !string.IsNullOrWhiteSpace(hid) : true;
            if (recordDevice)
            {
                if (userExists.LoginDevices == null)
                    userExists.LoginDevices = new Drx.Sdk.Network.DataBase.TableList<LoginDevice>();

                string deviceKey;
                string resolvedDeviceName;
                string resolvedOs;

                if (isClientLogin)
                {
                    deviceKey = hid;
                    resolvedDeviceName = deviceName;
                    resolvedOs = deviceOs;
                }
                else
                {
                    // Web 登录：用 User-Agent 的 SHA-256 前 16 字节作为稳定唯一键
                    var ua = request.Headers["User-Agent"] ?? string.Empty;
                    var uaBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(ua));
                    deviceKey = "web:" + Convert.ToHexString(uaBytes[..8]).ToLowerInvariant();
                    resolvedDeviceName = ParseBrowserName(ua);
                    resolvedOs = ParseOsFromUserAgent(ua);
                }

                var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                // 相同 key 的记录仅更新时间，避免每次登录都新增一行
                var existing = userExists.LoginDevices.FirstOrDefault(d => d.Hid == deviceKey);
                // 当前设备对应的 token（客户端用 clientToken，Web 用 webToken）
                var deviceToken = isClientLogin ? clientToken : webToken;
                if (existing != null)
                {
                    existing.DeviceName = string.IsNullOrWhiteSpace(resolvedDeviceName) ? existing.DeviceName : resolvedDeviceName;
                    existing.Os = string.IsNullOrWhiteSpace(resolvedOs) ? existing.Os : resolvedOs;
                    existing.LastLoginAt = nowMs;
                    existing.UpdatedAt = nowMs;
                    existing.Token = deviceToken ?? string.Empty;
                    userExists.LoginDevices.Update(existing);
                }
                else
                {
                    userExists.LoginDevices.Add(new LoginDevice
                    {
                        ParentId = userExists.Id,
                        Hid = deviceKey,
                        DeviceName = resolvedDeviceName,
                        Os = resolvedOs,
                        LastLoginAt = nowMs,
                        Token = deviceToken ?? string.Empty
                    });
                }
            }

            await KaxGlobal.UserDatabase.UpdateAsync(userExists);

            var resp = new HttpResponse()
            {
                StatusCode = 200,
                Body = new JsonObject
                {
                    ["message"] = "登录成功。",
                    ["client_token"] = clientToken,
                    ["web_token"] = webToken,
                    ["web_token_rotated"] = webTokenRotated
                }.ToJsonString(),
            };

            return resp;
        }
        else
        {
            Logger.Warn($"用户登录失败，用户名/邮箱或密码错误：{loginValue}");
            return new HttpResponse()
            {
                StatusCode = 401,
                Body = "用户名/邮箱或密码错误。",
            };
        }
    }

    [HttpHandle("/api/user/verify/account", "POST", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_Verify(HttpRequest request)
    {
        var (userModel, authError) = await Api.GetUserAsync(request);
        if (authError != null) return authError;
        var userName = userModel!.UserName;

        // 返回额外的权限信息，前端可据此决定是否显示管理员入口
        var permissionGroup = userModel.PermissionGroup;
        var isAdmin = permissionGroup == UserPermissionGroup.System || permissionGroup == UserPermissionGroup.Console || permissionGroup == UserPermissionGroup.Admin;
        // 新增：精确标识 System 权限组，用于前端判断是否显示"资产管理"等仅系统管理员可见的功能
        var isSystem = permissionGroup == UserPermissionGroup.System;

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

        return new JsonResult(new
        {
            message = "令牌有效，欢迎您！",
            user = userName,
            permissionGroup = (int)permissionGroup,
            isAdmin = isAdmin,
            isSystem = isSystem, // 新增：用于前端判断是否显示"资产管理"等仅系统管理员可见的功能
            avatarUrl = avatarUrl,
            // 兼容：返回用户统计摘要，方便 topbar/前端统一使用
            resourceCount = userModel.ResourceCount,
            gold = userModel.Gold,
            recentActivity = userModel.RecentActivity,
            cdkCount = await KaxGlobal.GetUserCdkCountAsync(userName ?? string.Empty)
        });
    }

    #endregion

    /// <summary>从 User-Agent 字符串解析浏览器名称，用于 Web 登录设备记录。</summary>
    private static string ParseBrowserName(string ua)
    {
        if (string.IsNullOrWhiteSpace(ua)) return "Web Browser";
        if (ua.Contains("Edg/") || ua.Contains("EdgA/")) return "Microsoft Edge";
        if (ua.Contains("OPR/") || ua.Contains("Opera/")) return "Opera";
        if (ua.Contains("Chrome/") && !ua.Contains("Chromium/")) return "Chrome";
        if (ua.Contains("Chromium/")) return "Chromium";
        if (ua.Contains("Firefox/")) return "Firefox";
        if (ua.Contains("Safari/") && !ua.Contains("Chrome/")) return "Safari";
        return "Web Browser";
    }

    /// <summary>从 User-Agent 字符串解析操作系统名称，用于 Web 登录设备记录。</summary>
    private static string ParseOsFromUserAgent(string ua)
    {
        if (string.IsNullOrWhiteSpace(ua)) return "Unknown";
        if (ua.Contains("Windows NT 10.0")) return "Windows 10/11";
        if (ua.Contains("Windows NT 6.3")) return "Windows 8.1";
        if (ua.Contains("Windows NT 6.2")) return "Windows 8";
        if (ua.Contains("Windows NT 6.1")) return "Windows 7";
        if (ua.Contains("Windows")) return "Windows";
        if (ua.Contains("Mac OS X")) return "macOS";
        if (ua.Contains("Android")) return "Android";
        if (ua.Contains("iPhone") || ua.Contains("iPad")) return "iOS";
        if (ua.Contains("Linux")) return "Linux";
        return "Unknown";
    }
}

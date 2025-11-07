using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Drx.Sdk.Network.V2.Web;
using Drx.Sdk.Shared;
using Drx.Sdk.Shared.Utility;

namespace KaxSocket.Handlers;

public class KaxHttp
{
    private static string GenerateLoginToken(UserData user)
    {
        var tokenSource = $"{user.UserName}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}:{Guid.NewGuid()}";
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(CommonUtility.ComputeSHA256Hash(tokenSource)));
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
            Logger.Info($"用户注册请求：{userName}, {email}");

            // 检查用户名或邮箱是否已被注册
            var userExists = KaxGlobal.UserDatabase.QueryFirstAsync(u => u.UserName == userName || u.Email == email).Result;
            if (userExists != null)
            {
                Logger.Warn($"用户注册失败，用户名或电子邮箱已被注册：{userName}, {email}");
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

            Logger.Info($"用户注册成功：{userName}, {email}");
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
    public static async Task<HttpResponse> PostLogin(HttpRequest request)
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
            userExists.LoginToken = GenerateLoginToken(userExists);
            userExists.LastLoginAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await KaxGlobal.UserDatabase.EditWhereAsync(u => u.Id == userExists.Id, userExists);

            Logger.Info($"生成登录令牌：{userExists.LoginToken}");
            Logger.Info($"用户登录成功：{userName}");
            return new HttpResponse()
            {
                StatusCode = 200,
                Body = new JsonObject
                {
                    ["message"] = "登录成功。",
                    ["login_token"] = userExists.LoginToken,
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

    /*
    * 测试用的受保护路由，需提供有效的登录令牌才能访问
    * 用于测试登录令牌的有效性
    */
    [HttpHandle("/api/hello/{token}", "GET")]
    public static HttpResponse Get_SayHello(HttpRequest request)
    {
        var token = request.PathParameters["token"];
        if (string.IsNullOrEmpty(token))
        {
            return new HttpResponse()
            {
                StatusCode = 400,
                Body = "无效的登录令牌。",
            };
        }

        var userExists = KaxGlobal.UserDatabase.QueryFirstAsync(u => u.LoginToken == token).Result;
        if (userExists == null)
        {
            return new HttpResponse()
            {
                StatusCode = 401,
                Body = "无效的登录令牌。",
            };
        }
        else if (userExists.LoginToken != token)
        {
            return new HttpResponse()
            {
                StatusCode = 401,
                Body = "无效的登录令牌。",
            };
        }

        return new HttpResponse()
        {
            StatusCode = 200,
            Body = $"Hello, your token is: {token}",
        };
    }
}

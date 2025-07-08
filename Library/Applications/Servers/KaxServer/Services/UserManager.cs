using System;
using System.IO;
using System.Threading.Tasks;
using Drx.Sdk.Network.Sqlite;
using DRX.Framework;
using KaxServer.Models;
using Microsoft.AspNetCore.Http;

namespace KaxServer.Services;

public static class UserManager
{
    private static readonly string DbPath;
    public static SqliteUnified<UserData> UserSql;

    static UserManager()
    {
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var dataDirectory = Path.Combine(baseDirectory, "data");

        if (!Directory.Exists(dataDirectory))
        {
            Directory.CreateDirectory(dataDirectory);
        }

        DbPath = Path.Combine(dataDirectory, "user.db");
        UserSql = new SqliteUnified<UserData>(DbPath);
    }

    public class RegisterResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public UserData User { get; set; }
    }

    public class LoginResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public UserData User { get; set; }
    }

    public static async Task<RegisterResult> Register(string userName, string password, string email, string verificationCode, EmailVerificationCode verificationService)
    {
        if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(email))
        {
            return new RegisterResult { Success = false, Message = "用户名、密码和邮箱不能为空" };
        }

        if (!verificationService.ValidateCode(email, verificationCode))
        {
            return new RegisterResult { Success = false, Message = "验证码无效或已过期" };
        }

        var existingUser = await UserSql.ReadSingleAsync("UserName", userName);
        if (existingUser != null)
        {
            return new RegisterResult { Success = false, Message = "用户名已被使用" };
        }

        existingUser = await UserSql.ReadSingleAsync("Email", email);
        if (existingUser != null)
        {
            return new RegisterResult { Success = false, Message = "该邮箱已被注册" };
        }

        try
        {
            var userData = new UserData
            {
                UserName = userName,
                Password = password, // 注意：实际应用中应该对密码进行加密
                Email = email
            };

            await UserSql.SaveAsync(userData);

            return new RegisterResult
            {
                Success = true,
                Message = "注册成功",
                User = userData
            };
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message);
            return new RegisterResult
            {
                Success = false,
                Message = "注册过程中发生错误，请稍后重试"
            };
        }
    }

    public static async Task<LoginResult> Login(string userNameOrEmail, string password)
    {
        try
        {
            if (string.IsNullOrEmpty(userNameOrEmail) || string.IsNullOrEmpty(password))
            {
                return new LoginResult { Success = false, Message = "用户名/邮箱和密码不能为空" };
            }

            UserData userData = await UserSql.ReadSingleAsync("UserName", userNameOrEmail);
            if (userData == null)
            {
                userData = await UserSql.ReadSingleAsync("Email", userNameOrEmail);
            }

            if (userData == null)
            {
                return new LoginResult { Success = false, Message = "用户不存在" };
            }

            if (userData.Password != password)
            {
                return new LoginResult { Success = false, Message = "密码错误" };
            }

            return new LoginResult
            {
                Success = true,
                Message = "登录成功",
                User = userData
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"登录过程中发生错误: {ex.Message}");
            return new LoginResult
            {
                Success = false,
                Message = "登录过程中发生错误，请稍后重试"
            };
        }
    }
    
    public static async Task<UserData> GetCurrentUserAsync(HttpContext httpContext)
    {
        // 首先尝试从Session中获取用户ID
        var userId = httpContext.Session.GetInt32("UserId");
        
        // 如果Session中没有，则尝试从Cookie中获取
        if (!userId.HasValue)
        {
            var userIdStr = httpContext.Request.Cookies["UserId"];
            if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int cookieUserId))
            {
                userId = cookieUserId;
                // 如果从Cookie中获取到了用户ID，同时更新Session
                httpContext.Session.SetInt32("UserId", cookieUserId);
                var userName = httpContext.Request.Cookies["UserName"];
                if (!string.IsNullOrEmpty(userName))
                {
                    httpContext.Session.SetString("UserName", userName);
                }
            }
        }

        if (userId.HasValue)
        {
            return await UserSql.ReadSingleAsync("Id", userId.Value.ToString());
        }
        return null;
    }
}

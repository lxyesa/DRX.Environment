using System;
using System.IO;
using System.Runtime.CompilerServices;
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

    // 添加延迟配置常量
    private const int LOGIN_DELAY_MS = 1000; // 登录延迟1秒
    private const int REGISTER_DELAY_MS = 1500; // 注册延迟1.5秒

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

    private static int generateUserId(string userName, string email)
    {
        // 通过 Sha256 + Base64 编码生成一个唯一的用户ID
        using (var sha256 = System.Security.Cryptography.SHA256.Create())
        {
            var input = $"{userName}{email}{DateTime.UtcNow.Ticks}";
            var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
            // 将字节数组转换为 Base64 字符串
            var base64String = Convert.ToBase64String(hashBytes);
            // 取前8个字符作为用户ID 
            var userIdString = base64String.Substring(0, 8);
            // 将 Base64 字符串转换为整数
            if (int.TryParse(userIdString, System.Globalization.NumberStyles.HexNumber, null, out int userId))
            {
                return userId;
            }
            // 如果转换失败，返回一个默认值
            return 0;
        }
    }

    public static async Task<RegisterResult> Register(string userName, string password, string email, string verificationCode, EmailVerificationCode verificationService)
    {
        if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(email))
        {
            await Task.Delay(REGISTER_DELAY_MS);
            return new RegisterResult { Success = false, Message = "用户名、密码和邮箱不能为空" };
        }
        if (verificationService == null)
        {
            await Task.Delay(REGISTER_DELAY_MS);
            return new RegisterResult { Success = false, Message = "验证码服务不可用" };
        }
        if (!verificationService.ValidateCode(email, verificationCode))
        {
            await Task.Delay(REGISTER_DELAY_MS);
            return new RegisterResult { Success = false, Message = "验证码无效或已过期" };
        }
        var existingUsers = await UserSql.QueryAsync("UserName", userName);
        var existingUser = existingUsers?.Count > 0 ? existingUsers[0] : null;
        if (existingUser != null)
        {
            await Task.Delay(REGISTER_DELAY_MS);
            return new RegisterResult { Success = false, Message = "用户名已被使用" };
        }
        var emailUsers = await UserSql.QueryAsync("Email", email);
        existingUser = emailUsers?.Count > 0 ? emailUsers[0] : null;
        if (existingUser != null)
        {
            await Task.Delay(REGISTER_DELAY_MS);
            return new RegisterResult { Success = false, Message = "该邮箱已被注册" };
        }
        try
        {
            var userData = new UserData
            {
                Id = generateUserId(userName, email),
                UserName = userName,
                Password = password,
                Email = email,
                UserSettingData = new UserSettingData
                {
                    EmailNotifications = true // 默认开启邮件通知
                }
            };
            await UserSql.PushAsync(userData);
            await Task.Delay(REGISTER_DELAY_MS);
            return new RegisterResult
            {
                Success = true,
                Message = "注册成功",
                User = userData
            };
        }
        catch (Exception ex)
        {
            Logger.Error(ex?.Message ?? "注册异常");
            await Task.Delay(REGISTER_DELAY_MS);
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
                await Task.Delay(LOGIN_DELAY_MS);
                return new LoginResult { Success = false, Message = "用户名/邮箱和密码不能为空" };
            }
            var userDataList = await UserSql.QueryAsync("UserName", userNameOrEmail);
            UserData userData = userDataList?.Count > 0 ? userDataList[0] : null;
            if (userData == null)
            {
                var emailList = await UserSql.QueryAsync("Email", userNameOrEmail);
                userData = emailList?.Count > 0 ? emailList[0] : null;
            }
            if (userData == null)
            {
                await Task.Delay(LOGIN_DELAY_MS);
                return new LoginResult { Success = false, Message = "用户不存在" };
            }
            if (userData.Password == null || userData.Password != password)
            {
                await Task.Delay(LOGIN_DELAY_MS);
                return new LoginResult { Success = false, Message = "密码错误" };
            }
            await Task.Delay(LOGIN_DELAY_MS);
            return new LoginResult
            {
                Success = true,
                Message = "登录成功",
                User = userData
            };
        }
        catch (Exception ex)
        {
            Logger.Error($"登录过程中发生错误: {ex?.Message ?? "未知异常"}");
            await Task.Delay(LOGIN_DELAY_MS);
            return new LoginResult
            {
                Success = false,
                Message = "登录过程中发生错误，请稍后重试"
            };
        }
    }

    public static async Task<UserData> GetCurrentUserAsync(HttpContext httpContext)
    {
        if (httpContext == null)
            return null;
        // 首先尝试从Session中获取用户ID
        var userId = httpContext.Session?.GetInt32("UserId");
        // 如果Session中没有，则尝试从Cookie中获取
        if (!userId.HasValue)
        {
            var userIdStr = httpContext.Request?.Cookies["UserId"];
            if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int cookieUserId))
            {
                userId = cookieUserId;
                // 如果从Cookie中获取到了用户ID，同时更新Session
                httpContext.Session?.SetInt32("UserId", cookieUserId);
                var userName = httpContext.Request?.Cookies["UserName"];
                if (!string.IsNullOrEmpty(userName))
                {
                    httpContext.Session?.SetString("UserName", userName);
                }
            }
        }
        if (userId.HasValue)
        {
            var userList = await UserSql.QueryAsync("Id", userId.Value.ToString());
            return userList?.Count > 0 ? userList[0] : null;
        }
        return null;
    }

    public static async Task<bool> ChangeUserNameAsync(UserData user, string newUserName)
    {
        if (user == null || string.IsNullOrEmpty(newUserName))
            return false;
        var existingUsers = await UserSql.QueryAsync("UserName", newUserName);
        if (existingUsers?.Count > 0 && existingUsers[0].Id != user.Id)
        {
            return false;
        }
        user.UserName = newUserName;
        user.UserSettingData.LastChangeNameTime = DateTime.UtcNow;
        user.UserSettingData.NextChangeNameTime = DateTime.UtcNow.AddDays(7);
        await UserSql.UpdateAsync(user);
        return true;
    }

    public static async Task<bool> UpdateUserAsync(UserData user)
    {
        if (user == null)
            return false;
        await UserSql.UpdateAsync(user);
        return true;
    }
}

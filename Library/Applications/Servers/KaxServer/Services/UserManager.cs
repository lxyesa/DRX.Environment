using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Drx.Sdk.Network.Sqlite;
using DRX.Framework;
using KaxServer.Models;
using Microsoft.AspNetCore.Http;
using Drx.Sdk.Shared.Cryptography;

namespace KaxServer.Services;

    /// <summary>
    /// 用户管理器，负责用户注册、登录、信息获取、状态变更等核心操作。
    /// 提供统一的用户数据访问与业务逻辑入口。
    /// </summary>
    public static class UserManager
    {
        /// <summary>
        /// 用户数据库文件路径（绝对路径）
        /// </summary>
        private static readonly string DbPath;

        /// <summary>
        /// 用户数据表操作对象（SqliteUnified 封装）
        /// </summary>
        public static SqliteUnified<UserData> UserSql;

        /// <summary>
        /// 登录延迟（毫秒），用于防止暴力破解
        /// </summary>
        private const int LOGIN_DELAY_MS = 1000; // 登录延迟1秒

        /// <summary>
        /// 注册延迟（毫秒），用于防止批量注册
        /// </summary>
        private const int REGISTER_DELAY_MS = 1500; // 注册延迟1.5秒

        /// <summary>
        /// 静态构造函数。初始化用户数据库路径和SqliteUnified实例。
        /// </summary>
        static UserManager()
        {
            // 获取应用根目录
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            // 数据目录
            var dataDirectory = Path.Combine(baseDirectory, "data");

            // 若目录不存在则自动创建
            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
            }

            // 拼接数据库文件路径
            DbPath = Path.Combine(dataDirectory, "user.db");
            // 初始化用户数据表操作对象
            UserSql = new SqliteUnified<UserData>(DbPath);
        }

    /// <summary>
    /// 用户注册结果模型
    /// </summary>
    public class RegisterResult
    {
        /// <summary>
        /// 是否注册成功
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// 结果消息
        /// </summary>
        public string Message { get; set; }
        /// <summary>
        /// 注册成功时返回的用户对象
        /// </summary>
        public UserData User { get; set; }
    }

    /// <summary>
    /// 用户登录结果模型
    /// </summary>
    public class LoginResult
    {
        /// <summary>
        /// 是否登录成功
        /// </summary>
        public bool Success { get; set; }
        /// <summary>
        /// 结果消息
        /// </summary>
        public string Message { get; set; }
        /// <summary>
        /// 登录成功时返回的用户对象
        /// </summary>
        public UserData User { get; set; }
    }

    /// <summary>
    /// 生成唯一的用户ID。通过用户名、邮箱和当前时间戳进行SHA256哈希并Base64编码，取前8位尝试转为整数。
    /// </summary>
    /// <param name="userName">用户名</param>
    /// <param name="email">邮箱</param>
    /// <returns>生成的用户ID（int），如转换失败返回0</returns>
    /// <exception cref="System.Security.Cryptography.CryptographicException">哈希计算异常</exception>
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

    /// <summary>
    /// 用户注册方法。校验参数、验证码、用户名和邮箱唯一性，注册新用户并写入数据库。
    /// </summary>
    /// <param name="userName">用户名</param>
    /// <param name="password">密码</param>
    /// <param name="email">邮箱</param>
    /// <param name="verificationCode">邮箱验证码</param>
    /// <param name="verificationService">验证码服务实例</param>
    /// <returns>注册结果，包含是否成功、消息和用户信息</returns>
    /// <exception cref="Exception">数据库写入或未知异常</exception>
    public static async Task<RegisterResult> Register(string userName, string password, string email, string verificationCode, EmailVerificationCode verificationService)
    {
        Logger.Info($"用户注册请求: {userName} ({email})");
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
        var existingUsers = await UserSql.QueryAsync("Username", userName);
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
                Username = userName,
                PasswordHash = SHA256.ComputeHashString(password),
                Email = email,
                UserSettingData = new UserSettingData
                {
                    EmailNotifications = true // 默认开启邮件通知
                }
            };
            await UserSql.PushAsync(userData);
            Logger.Info($"用户注册成功: {userName} ({email})");
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

    /// <summary>
    /// 异步处理Web用户登录请求
    /// </summary>
    /// <param name="userNameOrEmail">用户名或邮箱</param>
    /// <param name="password">用户密码</param>
    /// <returns>包含登录结果的Task对象，包含是否成功、消息和用户数据</returns>
    /// <remarks>
    /// 该方法会依次检查：1)输入是否为空 2)用户名是否存在 3)邮箱是否存在 4)密码是否正确
    /// 所有失败情况都会添加延迟(LOGIN_DELAY_MS)后返回
    /// </remarks>
    public static async Task<LoginResult> LoginWebAsync(string userNameOrEmail, string password)
    {
        try
        {
            if (string.IsNullOrEmpty(userNameOrEmail) || string.IsNullOrEmpty(password))
            {
                await Task.Delay(LOGIN_DELAY_MS);
                return new LoginResult { Success = false, Message = "用户名/邮箱和密码不能为空" };
            }
            var userDataList = await UserSql.QueryAsync("Username", userNameOrEmail);
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
            var passwordHash = SHA256.ComputeHashString(password);
            if (userData.PasswordHash == null || userData.PasswordHash != passwordHash)
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

    public static async Task<bool> LogoutWebAsync(int userId)
    {
        if (userId <= 0)
            return false;

        var userDataList = await UserSql.QueryAsync("Id", userId);
        if (userDataList?.Count > 0)
        {
            var userData = userDataList[0];
            userData.UserStatusData.IsWebLogin = false;
            await UserSql.UpdateAsync(userData);
            return true;
        }
        return false;
    }


    /// <summary>
    /// 异步处理应用程序登录请求
    /// </summary>
    /// <param name="userNameOrEmail">用户名或邮箱</param>
    /// <param name="password">用户密码</param>
    /// <returns>登录成功的用户数据，若登录失败则返回null</returns>
    /// <remarks>
    /// 该方法首先通过用户名查询用户，若未找到则尝试通过邮箱查询。
    /// 验证密码成功后，会生成新的AppToken并更新用户登录状态。
    /// </remarks>
    public static async Task<UserData> LoginAppAsync(string userNameOrEmail, string password)
    {
        var userDataList = await UserSql.QueryAsync("Username", userNameOrEmail);
        if (userDataList?.Count > 0)
        {
            var userData = userDataList[0];
            var passwordHash = SHA256.ComputeHashString(password);
            if (userData.PasswordHash == passwordHash)
            {
                var uData = userDataList[0];
                uData.UserStatusData.IsAppLogin = true;
                uData.UserStatusData.AppToken = Guid.NewGuid().ToString();
                await UserSql.UpdateAsync(uData);
                return uData;
            }
        }
        else
        {
            userDataList = await UserSql.QueryAsync("Email", userNameOrEmail);
            if (userDataList?.Count > 0)
            {
                var userData = userDataList[0];
                var passwordHash = SHA256.ComputeHashString(password);
                if (userData.PasswordHash == passwordHash)
                {
                    var uData = userDataList[0];
                    uData.UserStatusData.IsAppLogin = true;
                    uData.UserStatusData.AppToken = Guid.NewGuid().ToString();
                    await UserSql.UpdateAsync(uData);
                    return uData;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// 异步注销用户APP登录状态
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns>是否注销成功</returns>
    public static async Task<bool> LogoutAppAsync(int userId)
    {
        if (userId <= 0)
            return false;

        var userDataList = await UserSql.QueryAsync("Id", userId);
        if (userDataList?.Count > 0)
        {
            var userData = userDataList[0];
            userData.UserStatusData.IsAppLogin = false;
            userData.UserStatusData.AppToken = string.Empty;
            await UserSql.UpdateAsync(userData);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 获取当前登录用户信息（根据HttpContext的Session或Cookie）。
    /// </summary>
    /// <param name="httpContext">HTTP上下文</param>
    /// <returns>当前用户的UserData对象，未登录返回null</returns>
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
                var username = httpContext.Request?.Cookies["Username"];
                if (!string.IsNullOrEmpty(username))
                {
                    httpContext.Session?.SetString("Username", username);
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

    /// <summary>
    /// 修改用户昵称。校验新昵称唯一性，更新用户信息及改名时间。
    /// </summary>
    /// <param name="user">用户对象</param>
    /// <param name="newUserName">新昵称</param>
    /// <returns>修改成功返回true，否则返回false</returns>
    public static async Task<bool> ChangeUserNameAsync(UserData user, string newUserName)
    {
        if (user == null || string.IsNullOrEmpty(newUserName))
            return false;
        var existingUsers = await UserSql.QueryAsync("Username", newUserName);
        if (existingUsers?.Count > 0 && existingUsers[0].Id != user.Id)
        {
            return false;
        }
        user.Username = newUserName;
        user.UserSettingData.LastChangeNameTime = DateTime.UtcNow;
        user.UserSettingData.NextChangeNameTime = DateTime.UtcNow.AddDays(7);
        await UserSql.UpdateAsync(user);
        return true;
    }

    /// <summary>
    /// 用户金币增减操作。支持正负变动，防止变为负数。
    /// </summary>
    /// <param name="user">用户对象</param>
    /// <param name="amount">金币变动值（可正可负）</param>
    /// <returns>操作成功返回true，否则返回false</returns>
    public static async Task<bool> DoDelta(UserData user, int amount)
    {
        if (user == null)
            return false;
        if (user.Coins + amount < 0)
            return false;
        user.Coins += amount;
        await UserSql.UpdateAsync(user);
        return true;
    }

    /// <summary>
    /// 获取所有用户列表。
    /// </summary>
    /// <returns>用户列表</returns>
    public static async Task<List<UserData>> GetAllUsersAsync()
    {
        return await UserSql.GetAllAsync();
    }
    /// <summary>
    /// 根据用户ID获取用户信息
    /// </summary>
    public static async Task<UserData?> GetUserByIdAsync(int userId)
    {
        var users = await GetAllUsersAsync();
        return users.FirstOrDefault(u => u.Id == userId);
    }

    /// <summary>
    /// 更新用户信息到数据库。
    /// </summary>
    /// <param name="user">用户对象</param>
    /// <returns>更新成功返回true，否则返回false</returns>
    public static async Task<bool> UpdateUserAsync(UserData user)
    {
        if (user == null)
            return false;
        await UserSql.UpdateAsync(user);
        return true;
    }

   /// <summary>
   /// 保存或更新用户所有字段（含嵌套设置/状态）。
   /// </summary>
   /// <param name="user">用户对象</param>
   /// <returns>操作成功返回true，否则返回false</returns>
   public static async Task<bool> SaveOrUpdateUserAsync(UserData user)
   {
       if (user == null)
           return false;
       // 直接更新所有字段，包含 UserSettingData、UserStatusData
       await UserSql.UpdateAsync(user);
       return true;
   }
}

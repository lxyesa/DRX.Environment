using System;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Threading.Tasks;
using NetworkCoreSandard.Enums;
using NetworkCoreSandard.Models;

namespace NetworkCoreSandard;

public class UserManager
{
    private const string USER_DATA_DIR = "Data";
    private const string SERVER_DATA_DIR = "ServerData";
    private const string USER_DATA_FILE = "users.json";
    private readonly ConcurrentDictionary<string, UserInstance> _users = new();
    private readonly ConcurrentDictionary<string, UserInstance> _onlineUsers = new();
    
    public UserManager()
    {
        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 用户管理器初始化...");
        LoadAllUsersAsync().Wait();

        Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 用户管理器初始化完成");
        foreach (var user in _users)
        {
            Console.WriteLine($"用户名: {user.Key}");
        }
    }

    public async Task<(bool success, string message)> RegisterUserAsync(string username, string password, string email)
    {
        if (_users.ContainsKey(username))
            return (false, "用户名已存在");

        // 创建密码盐
        string salt = GenerateSalt();
        string hashedPassword = HashPassword(password, salt);

        var user = new UserInstance
        {
            Username = username,
            Password = hashedPassword,
            Salt = salt,
            Email = email,
            RegisterTime = DateTime.Now,
            UserGroup = UserGroup.Normal
        };

        if (_users.TryAdd(username, user))
        {
            await SaveUserDataAsync();  // 保存用户数据
            return (true, "注册成功");
        }

        return (false, "注册失败");
    }

    public async Task<(UserLoginResult result, UserInstance? user)> LoginUserAsync(
        string username, 
        string password, 
        string machineCode, 
        IPAddress ipAddress)
    {
        if (!_users.TryGetValue(username, out UserInstance? user))
            return (UserLoginResult.UserNotFound, null);

        if (_onlineUsers.ContainsKey(username))
            return (UserLoginResult.AlreadyOnline, null);

        string hashedPassword = HashPassword(password, user.Salt);
        if (user.Password != hashedPassword)
            return (UserLoginResult.WrongPassword, null);

        user.UpdateLoginInfo(ipAddress, machineCode);
        _onlineUsers.TryAdd(username, user);
        
        await SaveUserDataAsync();  // 保存用户数据
        return (UserLoginResult.Success, user);
    }

    public void LogoutUser(string username)
    {
        if (_onlineUsers.TryRemove(username, out UserInstance? user))
        {
            user.IsOnline = false;
            user.CurrentToken = null;
            // 不要从 _users 中移除用户
        }
    }

    public bool ValidateToken(string username, string token)
    {
        if (_users.TryGetValue(username, out UserInstance? user))
        {
            return user.CurrentToken == token;
        }
        return false;
    }

    // 添加通过token重新激活用户的方法
#pragma warning disable CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
    public async Task<(bool success, UserInstance? user)> ReactivateUserAsync(string username, string token)
#pragma warning restore CS1998 // 异步方法缺少 "await" 运算符，将以同步方式运行
    {
        if (_users.TryGetValue(username, out UserInstance? user))
        {
            if (user.CurrentToken == token)
            {
                user.IsOnline = true;
                _onlineUsers.TryAdd(username, user);
                return (true, user);
            }
        }
        return (false, null);
    }

    public async Task<bool> LoadAllUsersAsync()
    {
        try
        {
            string serverPath = AppDomain.CurrentDomain.BaseDirectory;
            string filePath = Path.Combine(serverPath, SERVER_DATA_DIR, USER_DATA_FILE);

            if (!File.Exists(filePath))
                return false;

            string jsonString = await Task.Run(() => File.ReadAllText(filePath));
            var loadedUsers = JsonSerializer.Deserialize<ConcurrentDictionary<string, UserInstance>>(jsonString);

            if (loadedUsers != null)
            {
                _users.Clear(); // 清除现有数据
                foreach (var user in loadedUsers)
                {
                    _users.TryAdd(user.Key, user.Value);
                }
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载所有用户数据时出错: {ex.Message}");
            return false;
        }
    }

    private string GenerateSalt()
    {
        byte[] saltBytes = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(saltBytes);
        }
        return Convert.ToBase64String(saltBytes);
    }

    private string HashPassword(string password, string salt)
    {
        using var sha256 = SHA256.Create();
        var passwordBytes = Encoding.UTF8.GetBytes(password + salt);
        var hashBytes = sha256.ComputeHash(passwordBytes);
        return Convert.ToBase64String(hashBytes);
    }

    private async Task SaveUserDataAsync()
    {
        try
        {
            // 获取服务器程序集所在目录
            string serverPath = AppDomain.CurrentDomain.BaseDirectory;
            // 构建服务器数据目录路径
            string dirPath = Path.Combine(serverPath, SERVER_DATA_DIR);
            Directory.CreateDirectory(dirPath);

            // 构建完整的用户数据文件路径
            string filePath = Path.Combine(dirPath, USER_DATA_FILE);

            // 将用户数据序列化为JSON
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All)
            };
            string jsonString = JsonSerializer.Serialize(_users, options);

            // 异步写入文件
            await Task.Run(() => File.WriteAllText(filePath, jsonString));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存用户数据时出错: {ex.Message}");
            throw; // 重新抛出异常,让调用者知道保存失败
        }
    }

    private async Task<UserInstance?> LoadUserDataAsync(string userName)
    {
        try
        {
            // 获取服务器程序集所在目录
            string serverPath = AppDomain.CurrentDomain.BaseDirectory;
            string filePath = Path.Combine(serverPath, SERVER_DATA_DIR, USER_DATA_FILE);

            if (!File.Exists(filePath))
                return null;

            string jsonString = await Task.Run(() => File.ReadAllText(filePath));
            var users = JsonSerializer.Deserialize<ConcurrentDictionary<string, UserInstance>>(jsonString);

            if (users != null && users.TryGetValue(userName, out UserInstance? user))
            {
                // 更新内存中的用户字典
                _users[userName] = user;
                return user;
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"加载用户数据时出错: {ex.Message}");
            return null;
        }
    }

    public UserInstance? GetUserByUsername(string username)
    {
        _users.TryGetValue(username, out UserInstance? user);
        return user;
    }

    public bool IsUserOnline(string username)
    {
        return _onlineUsers.ContainsKey(username);
    }
}
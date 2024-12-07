using System;
using System.Collections.Concurrent;
using NetworkCoreStandard;
using NetworkCoreStandard.EventArgs;
using NetworkCoreStandard.Interface;
using NetworkCoreStandard.Models;
using NetworkCoreStandard.Utils;
using NetworkCoreStandard.IO;
using System.Text.Json;

namespace NetworkCoreStandard.Components;

public class UserManagerComponent : IComponent
{
    public object? Owner { get; set; }
    public HashSet<UserModel> Users { get; set; } = new HashSet<UserModel>();

    public void Awake()
    {
        Logger.Log("Server", $"{Owner?.GetType().Name} 的 UserManagerComponent 已启动");

        // 初始化用户管理器
        Initialize();
    }

    public void Start()
    {
        // TODO: 尝试查找用户数据文件是否存在，如果不存在，则创建一个新的
        string usersFilePath = Path.Combine(PathFinder.GetAppPath(), "config", "users.json");

        if (!System.IO.File.Exists(usersFilePath))
        {
            Logger.Log("UserManager", "由于找不到现有（或是首次运行服务器），正在创建新的用户数据文件");
            System.IO.File.WriteAllText(usersFilePath, "{}");
        }
    }

    public void Initialize()
    {
        // TODO: 初始化用户管理器
        var server = (NetworkServer)Owner!;
        server.AddListener("OnDataReceived", (sender, args) =>
        {
            if (args.Packet?.Type == (int)PacketType.Request)
            {
                if (args.Packet.Header == "login")
                {
                    _ = server.RaiseEventAsync("OnUserLogin",
                        new NetworkEventArgs(
                            socket: args.Socket,
                            packet: args.Packet,
                            sender: this,
                            message: $"一个用户{args.Packet.GetBodyValue("username")}正在尝试登录",
                            eventType: NetworkEventType.HandlerEvent)
                            .AddElement("endpoint", args.Socket.RemoteEndPoint));
                }

                if (args?.Packet.Header == "register")
                {
                    _ = server.RaiseEventAsync("OnUserRegister",
                        new NetworkEventArgs(
                            socket: args.Socket,
                            packet: args.Packet,
                            sender: this,
                            message: $"一个用户{args.Packet.GetBodyValue("username")}正在尝试注册",
                            eventType: NetworkEventType.HandlerEvent)
                            .AddElement("endpoint", args.Socket.RemoteEndPoint));
                }
            }
        });
    }

    public async Task<(bool success, T? user)> TryGetFormFile<T>(string username)
    {
        try
        {
            string usersFilePath = Path.Combine(PathFinder.GetAppPath(), "config", "users.json");

            // 直接使用 File.ReadJsonKeyAsync 方法
            return await NetworkCoreStandard.IO.File.ReadJsonKeyAsync<T>(usersFilePath, username);
        }
        catch (Exception ex)
        {
            Logger.Log("UserManager", $"读取用户数据时发生错误: {ex.Message}");
            return (false, default);
        }
    }

    public async Task<(bool success, T? user)> TryGetFormMemory<T>(string username) where T : UserModel
    {
        try
        {
            // 1. 从内存中查找用户
            var existingUser = Users.FirstOrDefault(u =>
                u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

            // 2. 如果找到用户
            if (existingUser != null)
            {
                Logger.Log("UserManager", $"从内存中找到用户 {username}");
                return (true, existingUser as T);
            }

            // 3. 如果未找到用户
            Logger.Log("UserManager", $"未在内存中找到用户 {username}");
            return (false, null);
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "UserManager", $"从内存中获取用户时发生错误: {ex.Message}");
            return (false, null);
        }
    }

    public async Task<(string result, bool success)> TryRegister(UserModel user)
    {
        try
        {
            string usersFilePath = Path.Combine(PathFinder.GetAppPath(), "config", "users.json");

            // 直接使用 File.WriteJsonKeyAsync 方法
            await NetworkCoreStandard.IO.File.WriteJsonKeyAsync(usersFilePath, user.Username, user);

            return ("注册成功", true);
        }
        catch (Exception ex)
        {
            Logger.Log("UserManager", $"注册用户时发生错误: {ex.Message}");
            return (ex.Message, false);
        }
    }

    public async Task<bool> TryAdd(UserModel user)
    {
        try
        {
            // 1. 首先检查内存中是否已存在该用户
            if (Users.Contains(user))
            {
                Logger.Log("UserManager", $"用户 {user.Username} 已存在于内存中");
                return false;
            }

            // 2. 检查文件中是否存在该用户
            var (fileExist, existingUser) = await TryGetFormFile<UserModel>(user.Username);
            if (fileExist)
            {
                Logger.Log("UserManager", $"用户 {user.Username} 已存在于文件中，正在加载到内存");
                Users.Add(existingUser ?? user);
                return true;
            }

            // 3. 如果用户不存在，则尝试注册新用户
            var (result, registerSuccess) = await TryRegister(user);
            if (registerSuccess)
            {
                Logger.Log("UserManager", $"用户 {user.Username} 注册成功，已添加到内存");
                Users.Add(user);
                return true;
            }

            Logger.Log(LogLevel.Error, "UserManager", $"尝试添加用户 {user.Username} 时发生错误: {result}");
            return false;
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "UserManager", $"添加用户过程中发生异常: {ex.Message}");
            return false;
        }
    }


}

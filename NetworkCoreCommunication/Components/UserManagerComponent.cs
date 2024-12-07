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
    private ConcurrentDictionary<string, UserModel> _users = new ConcurrentDictionary<string, UserModel>();
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

        // 1. 添加事件监听，并转发为OnUserLogin和OnUserRegister事件
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

        // 2. 添加事件监听，并转发为 OnUserLogout 事件
        server.AddListener("OnClientDisconnected", (sender, args) =>
        {

        });
    }

    /// <summary>
    /// 从文件中查找用户信息
    /// </summary>
    /// <typeparam name="T">用户模型类型</typeparam>
    /// <param name="username">要查找的用户名</param>
    /// <param name="enableLogging">是否启用日志记录，默认为 false</param>
    /// <returns>返回一个元组，包含操作是否成功和找到的用户对象</returns>
    public async Task<(bool success, T? user)> TryGetFormFile<T>(string username, bool enableLogging = false) where T : UserModel
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            Logger.Log(LogLevel.Error, "UserManager", "用户名不能为空");
            return (false, default);
        }

        try
        {
            string usersFilePath = Path.Combine(PathFinder.GetAppPath(), "config", "users.json");

            if (!System.IO.File.Exists(usersFilePath))
            {
                if (enableLogging)
                {
                    Logger.Log("UserManager", "用户数据文件不存在");
                }
                return (false, default);
            }

            var result = await NetworkCoreStandard.IO.File.ReadJsonKeyAsync<T>(usersFilePath, username);

            if (enableLogging && result.success)
            {
                Logger.Log("UserManager", $"成功从文件中读取用户 {username} 的数据");
            }

            return result;
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "UserManager", $"读取用户 {username} 数据时发生错误: {ex.Message}");
            return (false, default);
        }
    }


    /// <summary>
    /// 从内存中查找用户信息
    /// </summary>
    /// <typeparam name="T">用户模型类型</typeparam>
    /// <param name="username">要查找的用户名</param>
    /// <param name="enableLogging">是否启用日志记录，默认为 false</param>
    /// <returns>返回一个元组，包含操作是否成功和找到的用户对象</returns>
    public async Task<(bool success, T? user)> TryGetFormMemory<T>(string username, bool enableLogging = false) where T : UserModel
    {
        // 步骤 1: 验证用户名是否有效
        if (string.IsNullOrWhiteSpace(username))
        {
            Logger.Log(LogLevel.Error, "UserManager", "用户名不能为空");
            return (false, default);
        }

        // 步骤 2: 从内存字典中尝试获取用户信息
        if (_users.TryGetValue(username, out var user))
        {
            // 步骤 3: 如果找到用户且启用了日志，记录成功信息
            if (enableLogging)
            {
                Logger.Log("UserManager", $"成功从内存中读取用户 {username} 的数据");
            }

            // 步骤 4: 返回找到的用户信息，将用户对象转换为请求的类型
            return (true, user as T);
        }

        // 步骤 5: 如果未找到用户，返回失败结果
        if (enableLogging)
        {
            Logger.Log("UserManager", $"未在内存中找到用户 {username} 的数据");
        }
        return (false, default);
    }

    /// <summary>
    /// 检查文件中是否存在指定用户
    /// </summary>
    /// <param name="username">要检查的用户名</param>
    /// <returns>如果用户存在返回true，否则返回false</returns>
    public async Task<bool> HasUserFormFile(string username, bool enableLogging = false)
    {
        // 步骤 1: 验证用户名是否有效
        if (string.IsNullOrWhiteSpace(username))
        {
            Logger.Log(LogLevel.Error, "UserManager", "用户名不能为空");
            return false;
        }

        try
        {
            // 步骤 2: 获取用户数据文件路径
            string usersFilePath = Path.Combine(PathFinder.GetAppPath(), "config", "users.json");

            // 步骤 3: 检查文件是否存在
            if (!System.IO.File.Exists(usersFilePath))
            {
                if (enableLogging)
                {
                    Logger.Log("UserManager", "用户数据文件不存在");
                }
                return false;
            }

            // 步骤 4: 尝试从文件中读取用户数据
            var result = await NetworkCoreStandard.IO.File.ReadJsonKeyAsync<UserModel>(usersFilePath, username);

            // 步骤 5: 返回查找结果
            return result.success;
        }
        catch (Exception ex)
        {
            // 步骤 6: 异常处理和日志记录
            Logger.Log(LogLevel.Error, "UserManager", $"读取用户 {username} 数据时发生错误: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 检查内存中是否存在指定用户
    /// </summary>
    /// <param name="username">要检查的用户名</param>
    /// <returns>如果用户存在返回true，否则返回false</returns>
    public async Task<bool> HasUserFormMemory(string username, bool enableLogging = false)
    {
        // 步骤 1: 验证用户名参数
        if (string.IsNullOrWhiteSpace(username))
        {
            Logger.Log(LogLevel.Error, "UserManager", "用户名不能为空");
            return false;
        }

        // 步骤 2: 从内存字典中检查用户
        var exists = _users.ContainsKey(username);

        // 步骤 3: 记录检查结果（可选）
        if (enableLogging)
        {
            Logger.Log("UserManager", exists
                ? $"用户 {username} 在内存中找到"
                : $"用户 {username} 在内存中未找到");
        }

        // 步骤 4: 返回检查结果
        return exists;
    }
}

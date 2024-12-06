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
                            eventType: NetworkEventType.HandlerEvent));
                }

                if (args?.Packet.Header == "register")
                {
                    _ = server.RaiseEventAsync("OnUserRegister",
                        new NetworkEventArgs(
                            socket: args.Socket,
                            packet: args.Packet,
                            sender: this,
                            message: $"一个用户{args.Packet.GetBodyValue("username")}正在尝试注册",
                            eventType: NetworkEventType.HandlerEvent));
                }
            }
        });
    }

    public async Task<(bool success, T? user)> TryGetUser<T>(string username)
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

    public async Task<(string result, bool success)> TryRegisterUser(UserComponent user)
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
}

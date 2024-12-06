using System;
using NetworkCoreStandard.EventArgs;
using NetworkCoreStandard.Models;
using NetworkCoreStandard.User.EventArgs;
using NetworkCoreStandard.Utils;

namespace NetworkCoreStandard.User.Extensions;

public static class User
{
    public static void AddUserManager(this NetworkServer server, Action<object?, NetworkEventArgs?> callback)
    {
        // 使用模式匹配和简化的条件判断
        server.AddListener("OnDataReceived", async (sender, args) =>
        {
            // 使用模式匹配简化判断
            if (args?.Packet is not { Type: (int)PacketType.Request, Header: "login" })
            {
                return;
            }

            try
            {
                // 如果有回调则执行回调
                if (callback != null)
                {
                    callback(sender, args);
                    return;
                }

                // 创建登录事件参数
                var loginArgs = new NetworkEventArgs(
                    model: args.Model,
                    socket: args.Socket,
                    packet: args.Packet,
                    eventType: NetworkEventType.HandlerEvent,
                    message: "用户登录"
                );

                // 添加扩展参数
                var extArgs = loginArgs.AddExtensionArgs<LoginEventArgs>();

                // 获取用户名和密码,使用 GetBodyValue<T> 泛型方法
                var username = args.Packet.GetBodyValue<string>("username") ?? string.Empty;
                var password = args.Packet.GetBodyValue<string>("password") ?? string.Empty;

                extArgs.SetArgs(username, password);

                // 触发登录事件
                await server.RaiseEventAsync("OnUserLogin", loginArgs);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "UserManager", $"处理登录请求时发生错误: {ex.Message}");
            }
        });
    }
}

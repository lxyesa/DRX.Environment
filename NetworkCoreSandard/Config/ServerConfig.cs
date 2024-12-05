using System;
using System.Net.Sockets;
using NetworkCoreStandard.Attributes;
using NetworkCoreStandard.Utils;

namespace NetworkCoreStandard.Config;

public class ServerConfig : BaseConfig
{
    public string IP { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 8888;
    public int MaxClients { get; set; } = 100; // 最大客户端数
    public List<string> BlacklistIPs { get; set; } = new(); // IP黑名单
    public List<string> WhitelistIPs { get; set; } = new(); // IP白名单
    public Func<Socket, bool>? CustomValidator { get; set; } // 自定义验证
    public float TickRate { get; set; } = 20;
    public int GCInterval { get; set; } = 5 * 1000 * 60; // 垃圾回收间隔
    public string OnServerStartedTip { get; set; } = "服务器已启动";
}

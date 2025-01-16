using DRX.Framework;
using DRX.Framework.Common;
using NDVServerLib.Config;

namespace NDVServerLib;

/// <summary>
/// 网络服务器类，处理TCP连接和事件分发
/// </summary>

public class NDVServer : DRXServer
{
    private readonly ServerConfig _config;

    public NDVServer(ServerConfig config) : base(config.MessageQueueChannels, config.MessageQueueSize, config.MessageQueueDelay)
    {
        _config = config;
        _key = _config.Key;
    }

    public void Start()
    {
        base.Start(_config.IP, _config.Port);
    }

    public override void BeginVerifyClient()
    {
        base.BeginVerifyClient();
        Logger.Log("Server", "客户端连接检查已启动");
    }

    public string? GetKey()
    {
        return _key;
    }
}
using DRX.Framework;
using DRX.Framework.Common;
using DRX.Framework.Common.Args;
using DRX.Framework.Common.Engine;
using NDVServerLib.Config;

namespace NDVServerLib;

/// <summary>
/// 网络服务器类，处理TCP连接和事件分发
/// </summary>

public class NdvServerEngine : ServerEngine
{

    private readonly ServerConfig _config;

    public NdvServerEngine(ServerConfig config) : base(config.MessageQueueChannels, config.MessageQueueSize, config.MessageQueueDelay)
    {
        _config = config;
        Key = _config.Key;
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

    public override void BlockClient(DRXSocket clientSocket, int timeH)
    {
        base.BlockClient(clientSocket, timeH);
    }

    public string? GetKey()
    {
        return Key;
    }
}
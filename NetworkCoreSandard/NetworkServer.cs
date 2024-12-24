using NetworkCoreStandard.Utils;
using NetworkCoreStandard.Config;
using NetworkCoreStandard.Utils.Common;

namespace NetworkCoreStandard;

/// <summary>
/// 网络服务器类，处理TCP连接和事件分发
/// </summary>

public class NetworkServer : DRXServer 
{
    private readonly ServerConfig _config;
    
    public NetworkServer(ServerConfig config) : base(config.MessageQueueChannels, config.MessageQueueSize, config.MessageQueueDelay)
    {
        _config = config;
    }

    public void Start()
    {
        base.Start(_config.IP, _config.Port);
        Logger.Log("Server", $"服务器已启动，监听 {_config.IP}:{_config.Port}");
    }
}
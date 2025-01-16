using System;
using NetworkCoreStandard.Common.Base;

namespace NetworkCoreStandard.Config;

public class ServerConfig : BaseConfig
{
    public string IP { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 8463;
    public int MaxConnections { get; set; } = 0;
    public bool WriteLog { get; set; } = false;
    public int MessageQueueChannels { get; set; } = Environment.ProcessorCount;
    public int MessageQueueSize { get; set; } = 10000;
    public int MessageQueueDelay { get; set; } = 500;
    public string Key { get; set; } = Guid.NewGuid().ToString();
}

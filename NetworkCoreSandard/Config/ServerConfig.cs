using System;
using NetworkCoreStandard.Utils.Common.Config;

namespace NetworkCoreStandard.Config;

public class ServerConfig : ConfigItem
{
    public string IP { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 8463;
    public int MaxConnections { get; set; } = 0;
    public bool WriteLog { get; set; } = false;
    public int MessageQueueChannels { get; set; } = Environment.ProcessorCount;
    public int MessageQueueSize { get; set; } = 10000;
    public int MessageQueueDelay { get; set; } = 500;
}

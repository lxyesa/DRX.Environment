using NetworkCoreStandard.Common;

namespace NetworkCoreStandard;

public class NDVClient : DRXClient
{
    /// <summary>
    /// 初始化网络客户端
    /// </summary>
    public NDVClient(string serverIP, int serverPort) : base(serverIP, serverPort)
    {
    }
}

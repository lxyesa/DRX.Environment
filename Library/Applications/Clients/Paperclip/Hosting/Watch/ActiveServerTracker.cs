using Drx.Sdk.Network.Http;

namespace DrxPaperclip.Hosting.Watch;

/// <summary>
/// 跟踪脚本执行期间创建的活跃 HTTP 服务器实例。
/// 在 watch 模式重载前，批量停止上一轮所有服务器以释放端口和线程。
/// </summary>
public static class ActiveServerTracker
{
    private static readonly object Gate = new();
    private static readonly List<DrxHttpServer> Servers = new();

    /// <summary>
    /// 注册一个新创建的服务器实例。
    /// </summary>
    public static void Register(DrxHttpServer server)
    {
        ArgumentNullException.ThrowIfNull(server);

        lock (Gate)
        {
            Servers.Add(server);
        }
    }

    /// <summary>
    /// 停止并释放所有已注册的服务器实例，清空跟踪列表。
    /// </summary>
    public static async Task StopAllAsync()
    {
        List<DrxHttpServer> snapshot;
        lock (Gate)
        {
            snapshot = new List<DrxHttpServer>(Servers);
            Servers.Clear();
        }

        foreach (var server in snapshot)
        {
            try
            {
                server.Stop();
            }
            catch
            {
                // 忽略停止失败，确保尽力清理所有实例
            }
        }

        foreach (var server in snapshot)
        {
            try
            {
                await server.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // 忽略释放失败
            }
        }
    }

    /// <summary>
    /// 获取当前跟踪的服务器数量（用于调试）。
    /// </summary>
    public static int Count
    {
        get
        {
            lock (Gate)
            {
                return Servers.Count;
            }
        }
    }
}

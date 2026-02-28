using System;
using System.Linq;
using Drx.Sdk.Network.Http.Commands;
using Drx.Sdk.Network.Http;
using Drx.Sdk.Shared;

namespace KaxSocket.Handlers.Command;

/// <summary>
/// 系统级别命令处理器 - SSE 客户端检查、服务器状态等
/// </summary>
public class SystemCommandHandler
{
    /// <summary>
    /// 查询当前已连接的 SSE 客户端列表
    /// </summary>
    [Command("listsse [path]", "system:系统工具", "显示已连接的 SSE 客户端列表，可选参数指定端点路径")]
    public static void Cmd_ListSseClients(DrxHttpServer server, string? path = null)
    {
        var clients = server.GetSseClients(path);
        
        if (clients.Count == 0)
        {
            Console.WriteLine(path != null 
                ? $"路径 {path} 上无活跃的 SSE 客户端" 
                : "无活跃的 SSE 客户端");
            return;
        }

        Console.WriteLine($"活跃 SSE 客户端数: {clients.Count}" + (path != null ? $" (路径: {path})" : ""));
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
        Console.WriteLine("{0,-36} {1,-30} {2,-16} {3}", "客户端 ID", "路径", "远程地址", "连接时长");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");

        foreach (var client in clients.OrderByDescending(c => c.ConnectedAt))
        {
            var duration = DateTime.Now - client.ConnectedAt;
            var durationStr = FormatDuration(duration);
            var remoteAddr = string.IsNullOrEmpty(client.RemoteAddress) ? "未知" : client.RemoteAddress;

            Console.WriteLine("{0,-36} {1,-30} {2,-16} {3}",
                client.ClientId.Substring(0, Math.Min(36, client.ClientId.Length)),
                client.Path.Length > 30 ? client.Path.Substring(0, 27) + "..." : client.Path,
                remoteAddr.Length > 16 ? remoteAddr.Substring(0, 13) + "..." : remoteAddr,
                durationStr);
        }

        Console.WriteLine("─────────────────────────────────────────────────────────────────");
    }

    /// <summary>
    /// 显示指定路径上的 SSE 客户端数量
    /// </summary>
    [Command("ssestats [path]", "system:系统工具", "显示 SSE 客户端统计信息")]
    public static void Cmd_SseStats(DrxHttpServer server, string? path = null)
    {
        var count = server.GetSseClientCount(path);
        
        if (path != null)
        {
            Console.WriteLine($"路径 {path} 上的 SSE 客户端数: {count}");
        }
        else
        {
            var clients = server.GetSseClients();
            var groupedByPath = clients.GroupBy(c => c.Path).OrderByDescending(g => g.Count());
            
            Console.WriteLine($"总 SSE 客户端数: {count}");
            Console.WriteLine("按路径统计:");
            foreach (var group in groupedByPath)
            {
                Console.WriteLine($"  {group.Key}: {group.Count()} 个");
            }
        }
    }

    /// <summary>
    /// 格式化时间段为易读的字符串
    /// </summary>
    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return $"{Math.Floor(duration.TotalHours)}h {duration.Minutes}m";
        
        if (duration.TotalMinutes >= 1)
            return $"{Math.Floor(duration.TotalMinutes)}m {duration.Seconds}s";
        
        return $"{duration.Seconds}s";
    }
}

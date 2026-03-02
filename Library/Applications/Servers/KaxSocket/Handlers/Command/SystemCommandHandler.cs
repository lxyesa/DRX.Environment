using System;
using System.Linq;
using System.Diagnostics;
using Drx.Sdk.Network.Http.Commands;
using Drx.Sdk.Network.Http;

namespace KaxSocket.Handlers.Command;

public class SystemCommandHandler
{
    [Command("ping [message]", "system:系统工具", "健康检查命令，返回 pong 与当前时间，可选携带消息")]
    public static void Cmd_Ping(string? message = null)
    {
        var now = DateTime.Now;
        Console.WriteLine($"pong {now:yyyy-MM-dd HH:mm:ss.fff}" + (string.IsNullOrWhiteSpace(message) ? string.Empty : $" | {message}"));
    }

    [Command("uptime", "system:系统工具", "显示当前进程运行时长")]
    public static void Cmd_Uptime()
    {
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        Console.WriteLine("运行时长");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
        Console.WriteLine($"天数: {uptime.Days}");
        Console.WriteLine($"时长: {uptime:dd\\.hh\\:mm\\:ss}");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
    }

    [Command("memstats", "system:系统工具", "显示当前进程内存与 GC 信息")]
    public static void Cmd_MemStats()
    {
        var proc = Process.GetCurrentProcess();
        var gcMem = GC.GetTotalMemory(false);

        Console.WriteLine("内存统计");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
        Console.WriteLine($"托管内存: {FormatBytes(gcMem)}");
        Console.WriteLine($"工作集:   {FormatBytes(proc.WorkingSet64)}");
        Console.WriteLine($"私有内存: {FormatBytes(proc.PrivateMemorySize64)}");
        Console.WriteLine($"GC Gen0:  {GC.CollectionCount(0)}");
        Console.WriteLine($"GC Gen1:  {GC.CollectionCount(1)}");
        Console.WriteLine($"GC Gen2:  {GC.CollectionCount(2)}");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
    }

    [Command("clear", "system:系统工具", "清空控制台输出")]
    public static void Cmd_Clear()
    {
        Console.Clear();
        Console.WriteLine("控制台已清空。");
    }

    [Command("listsse [path]", "system:系统工具", "显示已连接的 SSE 客户端列表，可选参数指定端点路径")]
    public static void Cmd_ListSseClients(DrxHttpServer server, string? path = null)
    {
        if (server == null)
        {
            Console.WriteLine("命令上下文缺少服务器实例，无法查询 SSE 客户端。请在服务器上下文中执行该命令。");
            return;
        }

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

    [Command("ssestats [path]", "system:系统工具", "显示 SSE 客户端统计信息")]
    public static void Cmd_SseStats(DrxHttpServer server, string? path = null)
    {
        if (server == null)
        {
            Console.WriteLine("命令上下文缺少服务器实例，无法统计 SSE 客户端。请在服务器上下文中执行该命令。");
            return;
        }

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

    [Command("dbstats", "system:系统工具", "显示用户/资源/CDK 数据库统计信息")]
    public static void Cmd_DbStats()
    {
        var userCount = KaxGlobal.UserDatabase.SelectAll().Count;
        var bannedCount = KaxGlobal.UserDatabase.SelectAll().Count(u => u.Status.IsBanned);
        var assetCount = KaxGlobal.AssetDataBase.SelectAll().Count;
        var cdkAll = KaxGlobal.CdkDatabase.SelectAll();
        var cdkTotal = cdkAll.Count;
        var cdkUsed = cdkAll.Count(c => c.IsUsed);

        Console.WriteLine("数据库统计");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
        Console.WriteLine($"用户总数: {userCount}");
        Console.WriteLine($"封禁用户: {bannedCount}");
        Console.WriteLine($"资产总数: {assetCount}");
        Console.WriteLine($"CDK 总数: {cdkTotal}");
        Console.WriteLine($"CDK 已使用: {cdkUsed}");
        Console.WriteLine($"CDK 未使用: {cdkTotal - cdkUsed}");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
    }

    [Command("time", "system:系统工具", "显示当前系统时间与 Unix 时间戳")]
    public static void Cmd_Time()
    {
        var nowLocal = DateTime.Now;
        var nowUtc = DateTime.UtcNow;
        var unixSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        Console.WriteLine("时间信息");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
        Console.WriteLine($"本地时间: {nowLocal:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"UTC 时间:  {nowUtc:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"Unix 秒:   {unixSec}");
        Console.WriteLine($"Unix 毫秒: {unixMs}");
        Console.WriteLine("─────────────────────────────────────────────────────────────────");
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return $"{Math.Floor(duration.TotalHours)}h {duration.Minutes}m";
        
        if (duration.TotalMinutes >= 1)
            return $"{Math.Floor(duration.TotalMinutes)}m {duration.Seconds}s";
        
        return $"{duration.Seconds}s";
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var idx = 0;
        while (size >= 1024 && idx < units.Length - 1)
        {
            size /= 1024;
            idx++;
        }
        return $"{size:0.##} {units[idx]}";
    }
}

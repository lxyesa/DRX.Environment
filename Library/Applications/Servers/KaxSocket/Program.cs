using Drx.Sdk.Network.V2.Socket;
using Drx.Sdk.Network.V2.Web;
using Drx.Sdk.Shared;
using Drx.Sdk.Shared.Serialization;
using Drx.Sdk.Shared.Utility;
using System;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using HttpMethod = System.Net.Http.HttpMethod;

public class Program
{
    // 简单测试：启动 HttpServer 并注册处理方法
    public static async Task Main(string[] args)
    {
        if (!GlobalUtility.IsAdministrator())
        {
            var err_NotAdmin = "权限不足，正在尝试以管理员权限重启...";
            Logger.Warn(err_NotAdmin);
            Logger.Info("如果重启失败，请以管理员权限手动运行此程序。");

            _ = GlobalUtility.RestartAsAdministratorAsync();
            // 结束当前进程
            Environment.Exit(0);
        }

        var prefixes = new[] { "http://+:8462/" };
        var server = new HttpServer(prefixes);
        HttpServer.RegisterHandlersFromAssembly(typeof(Program).Assembly, server);

        try
        {
            await server.StartAsync();
        }
        catch (Exception ex)
        {

            Logger.Error($"启动 HttpServer 时发生错误: {ex.Message}");
            Console.WriteLine("启动 HttpServer失败: " + ex.Message);
            Console.WriteLine("提示: HttpListener 不支持 '0.0.0.0' 前缀。若需监听所有接口，请使用 'http://+:<port>/' 并为该 URL 注册 ACL（需要管理员权限）。");
            throw;
        }
    }
}
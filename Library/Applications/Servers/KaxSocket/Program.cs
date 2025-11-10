using Drx.Sdk.Network.V2.Socket;
using Drx.Sdk.Network.V2.Web;
using Drx.Sdk.Shared;
using Drx.Sdk.Shared.Serialization;
using Drx.Sdk.Shared.Utility;
using KaxSocket;
using KaxSocket.Handlers;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
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
        var server = new DrxHttpServer(prefixes);

        try
        {
            var uploadToken = ConfigUtility.Read($"{AppDomain.CurrentDomain.BaseDirectory}configs.ini", "upload_token", "general");
            var version = ConfigUtility.Read($"{AppDomain.CurrentDomain.BaseDirectory}configs.ini", "version", "general");

            if (string.IsNullOrEmpty(uploadToken))
            {
                uploadToken = CommonUtility.GenerateGeneralCode("UPL", 8, 4, true, true);
                ConfigUtility.Push($"{AppDomain.CurrentDomain.BaseDirectory}configs.ini", "upload_token", uploadToken, "general");
            }

            if (string.IsNullOrEmpty(version))
            {
                version = "1.0.0";
                ConfigUtility.Push($"{AppDomain.CurrentDomain.BaseDirectory}configs.ini", "version", version, "general");
            }

            server.AddRoute(HttpMethod.Get, "/", req => new HtmlResultFromFile($"{AppDomain.CurrentDomain.BaseDirectory}Views/index.html"));
            server.AddRoute(HttpMethod.Get, "/login", req => new HtmlResultFromFile($"{AppDomain.CurrentDomain.BaseDirectory}Views/login.html"));
            server.AddRoute(HttpMethod.Get, "/register", req => new HtmlResultFromFile($"{AppDomain.CurrentDomain.BaseDirectory}Views/register.html"));
            server.RegisterHandlersFromAssembly(typeof(KaxHttp));

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
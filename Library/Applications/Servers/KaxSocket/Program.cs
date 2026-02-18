using Drx.Sdk.Network.DataBase.Sqlite;
using Drx.Sdk.Network.DataBase.Sqlite.V2;
using Drx.Sdk.Network.DataBase.Sqlite.V2.Tests;
using Drx.Sdk.Network.V2.Socket;
using Drx.Sdk.Network.V2.Web;
using Drx.Sdk.Network.V2.Web.Models;
using Drx.Sdk.Shared;
using Drx.Sdk.Shared.Serialization;
using Drx.Sdk.Shared.Utility;
using KaxSocket;
using KaxSocket.Handlers;
using KaxSocket.Handlers.Command;
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
        // await TableListPerformanceTest.RunAllTests();
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

            server.FileRootPath = $"{AppDomain.CurrentDomain.BaseDirectory}Views";

            server.AddRoute(HttpMethod.Get, "/", req => new HtmlResultFromFile($"{AppDomain.CurrentDomain.BaseDirectory}Views/index.html"));
            server.AddRoute(HttpMethod.Get, "/login", req => new HtmlResultFromFile($"{AppDomain.CurrentDomain.BaseDirectory}Views/login.html"));
            server.AddRoute(HttpMethod.Get, "/register", req => new HtmlResultFromFile($"{AppDomain.CurrentDomain.BaseDirectory}Views/register.html"));
            server.AddRoute(HttpMethod.Get, "/cdk/admin", req=> new HtmlResultFromFile($"{AppDomain.CurrentDomain.BaseDirectory}Views/cdkadmin.html"));
            server.AddRoute(HttpMethod.Get, "/asset/admin", req => new HtmlResultFromFile($"{AppDomain.CurrentDomain.BaseDirectory}Views/assetadmin.html"));
            server.AddRoute(HttpMethod.Get, "/profile", req => new HtmlResultFromFile($"{AppDomain.CurrentDomain.BaseDirectory}Views/profile.html"));
            server.AddRoute(HttpMethod.Get, "/profile/{uid}", req => new HtmlResultFromFile($"{AppDomain.CurrentDomain.BaseDirectory}Views/profile.html"));
            server.RegisterHandlersFromAssembly(typeof(DLTBModPackerHttp));
            server.RegisterHandlersFromAssembly(typeof(KaxHttp));
            server.RegisterCommandsFromType(typeof(KaxCommandHandler));

            server.DoTicker(1000 * 60, async (s) =>
            {
                await KaxGlobal.CleanUpAssets();
                Logger.Info("已执行定时清理过期资源任务");
            });

            Logger.Info("HttpServer 正在启动，监听地址: " + string.Join(", ", prefixes));
            await server.StartAsync();
        }
        catch (Exception ex)
        {

            Logger.Error($"启动 HttpServer 时发生错误: {ex.Message}");
            throw;
        }
    }
}
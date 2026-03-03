using Drx.Sdk.Network.DataBase;
using Drx.Sdk.Network.Tcp;
using Drx.Sdk.Network.Http;
using Drx.Sdk.Network.Http.Configs;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.Performance;
using Drx.Sdk.Network.Http.Models;
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
using System.Collections.Generic;
using System.Text;
using System.Threading;
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
        var serverOptions = new DrxHttpServerOptions
        {
            DevRuntime = new DevRuntimeOptions
            {
                Enabled = true,
                WatchDirectories = new List<string>
                {
                    $"{AppDomain.CurrentDomain.BaseDirectory}Views"
                },
                DebounceMilliseconds = 200,
            }
        };
        var server = new DrxHttpServer(prefixes, null, serverOptions);

        try
        {
            await server.InitializeResourceIndexAsync();

            server.FileRootPath = $"{AppDomain.CurrentDomain.BaseDirectory}Views";
            server.NotFoundPagePath = $"{AppDomain.CurrentDomain.BaseDirectory}Views/html/404.html";

            server.AddRoute(HttpMethod.Get, "/", req => new HtmlResultFromFile($"{AppDomain.CurrentDomain.BaseDirectory}Views/html/index.html"));
            server.AddRoute(HttpMethod.Get, "/login", req => new HtmlResultFromFile($"{AppDomain.CurrentDomain.BaseDirectory}Views/html/login.html"));
            server.AddRoute(HttpMethod.Get, "/register", req => new HtmlResultFromFile($"{AppDomain.CurrentDomain.BaseDirectory}Views/html/register.html"));
            server.AddRoute(HttpMethod.Get, "/oauth/authorize", req => new HtmlResultFromFile($"{AppDomain.CurrentDomain.BaseDirectory}Views/html/oauth_authorize.html"));
            server.AddRoute(HttpMethod.Get, "/profile", req => new HtmlResultFromFile($"{AppDomain.CurrentDomain.BaseDirectory}Views/html/profile.html"));
            server.AddRoute(HttpMethod.Get, "/profile/{uid}", req => new HtmlResultFromFile($"{AppDomain.CurrentDomain.BaseDirectory}Views/html/profile.html"));
            server.AddRoute(HttpMethod.Get, "/shop", req => new HtmlResultFromFile($"{AppDomain.CurrentDomain.BaseDirectory}Views/html/shop.html"));
            server.AddRoute(HttpMethod.Get, "/asset", req => new HtmlResultFromFile($"{AppDomain.CurrentDomain.BaseDirectory}Views/html/shop.html"));
            server.AddRoute(HttpMethod.Get, "/shop/detail", req =>
            {
                var legacyIdRaw = req.Query["id"];
                if (int.TryParse(legacyIdRaw, out var legacyId) && legacyId > 0)
                {
                    return new RedirectResult($"/asset/detail/{legacyId}");
                }

                return new RedirectResult("/asset");
            });
            server.AddRoute(HttpMethod.Get, "/shop/detail/{id}", req =>
            {
                var legacyIdRaw = req.PathParameters["id"];
                if (int.TryParse(legacyIdRaw, out var legacyId) && legacyId > 0)
                {
                    return new RedirectResult($"/asset/detail/{legacyId}");
                }

                return new RedirectResult("/asset");
            });
            server.AddRoute(HttpMethod.Get, "/asset/detail/{id}", req => new HtmlResultFromFile($"{AppDomain.CurrentDomain.BaseDirectory}Views/html/shop_detail.html"));
            server.AddRoute(HttpMethod.Get, "/user/verify-email", req => new HtmlResultFromFile($"{AppDomain.CurrentDomain.BaseDirectory}Views/html/verify-email.html"));
            server.AddRoute(HttpMethod.Get, "/forgot-password", req => new HtmlResultFromFile($"{AppDomain.CurrentDomain.BaseDirectory}Views/html/forgot-password.html"));
            server.AddRoute(HttpMethod.Get, "/reset-password", req => new HtmlResultFromFile($"{AppDomain.CurrentDomain.BaseDirectory}Views/html/reset-password.html"));
            server.AddRoute(HttpMethod.Get, "/console", req => new HtmlResultFromFile($"{AppDomain.CurrentDomain.BaseDirectory}Views/html/console.html"));
            server.AddRoute(HttpMethod.Get, "/developer", req => new HtmlResultFromFile($"{AppDomain.CurrentDomain.BaseDirectory}Views/html/developer.html"));

            server.FileUploadRouter("/api/file/upload", "uploads");

            server.RegisterHandlersFromAssembly(typeof(KaxHttp));
            KaxHttp.RegisterOAuthTokenApi(server);
            server.RegisterCommandsFromType(typeof(AssetCommandHandler));
            server.RegisterCommandsFromType(typeof(UserCommandHandler));
            server.RegisterCommandsFromType(typeof(SystemCommandHandler));

            server.DoTicker(1000 * 60, async (s) =>
            {
                await KaxGlobal.CleanUpAssets();
                await KaxGlobal.CleanUpPasswordResetTokensAsync();
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
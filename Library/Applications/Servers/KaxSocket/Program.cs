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
using System.IO;
using HttpMethod = System.Net.Http.HttpMethod;

public class Program
{
    /// <summary>
    /// 应用配置（用于 JWT 和 SMTP）。
    /// </summary>
    public static class Config
    {
        // JWT 配置
        public const string JwtSecretKey = "your-secret-key-here-min-32-chars-recommended";
        public const string JwtIssuer = "KaxSocket";
        public const string JwtAudience = "KaxUsers";
        public const int JwtExpirationDays = 7;

        // SMTP 配置
        public const string SmtpEmail = "157335596@qq.com";  // 发送者邮箱
        public const string SmtpAuthCode = "eymlrhwykskccbdb";  // 授权码
        public const string SmtpHost = "smtp.qq.com";
        public const int SmtpPort = 587;
        public const bool SmtpEnableSsl = true;
    }

    public static async Task Main(string[] args)
    {
        // 配置 JWT
        var jwtConfig = new Drx.Sdk.Network.Http.Auth.JwtHelper.JwtConfig();
        Drx.Sdk.Network.Http.Auth.JwtHelper.Configure(jwtConfig);
        Logger.Info($"JWT 已配置 - Issuer: {jwtConfig.Issuer}, Audience: {jwtConfig.Audience}, Expiration: {jwtConfig.Expiration.Days} 天");

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
            #if DEBUG
            server.DebugMode(true);
            #endif

            server.FileRootPath = $"{AppDomain.CurrentDomain.BaseDirectory}Views";
            server.NotFoundPagePath = $"{AppDomain.CurrentDomain.BaseDirectory}Views/html/404.html";

            server.AddRoute(HttpMethod.Get, "/", req => new HtmlResultFromFile("html/index.html"));
            server.AddRoute(HttpMethod.Get, "/login", req => new HtmlResultFromFile("html/login.html"));
            server.AddRoute(HttpMethod.Get, "/register", req => new HtmlResultFromFile("html/register.html"));
            server.AddRoute(HttpMethod.Get, "/oauth/authorize", req => new HtmlResultFromFile("html/oauth_authorize.html"));
            server.AddRoute(HttpMethod.Get, "/profile", req => new HtmlResultFromFile("html/profile.html"));
            server.AddRoute(HttpMethod.Get, "/profile/{uid}", req => new HtmlResultFromFile("html/profile.html"));
            server.AddRoute(HttpMethod.Get, "/shop", req => new HtmlResultFromFile("html/shop.html"));
            server.AddRoute(HttpMethod.Get, "/asset", req => new HtmlResultFromFile("html/shop.html"));
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
            server.AddRoute(HttpMethod.Get, "/asset/detail/{id}", req => new HtmlResultFromFile("html/shop_detail.html"));
            server.AddRoute(HttpMethod.Get, "/user/verify-email", req => new HtmlResultFromFile("html/verify-email.html"));
            server.AddRoute(HttpMethod.Get, "/forgot-password", req => new HtmlResultFromFile("html/forgot-password.html"));
            server.AddRoute(HttpMethod.Get, "/reset-password", req => new HtmlResultFromFile("html/reset-password.html"));
            server.AddRoute(HttpMethod.Get, "/console", req => new HtmlResultFromFile("html/console.html"));
            server.AddRoute(HttpMethod.Get, "/manage-users", req => new HtmlResultFromFile("html/manage-users.html"));
            server.AddRoute(HttpMethod.Get, "/developer", req => new HtmlResultFromFile("html/developer.html"));

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
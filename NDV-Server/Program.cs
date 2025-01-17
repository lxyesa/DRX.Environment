using DRX.Framework;
using DRX.Framework.Common;
using DRX.Framework.Common.Base;
using DRX.Framework.Common.Models;
using DRX.Framework.Common.Utility;
using NDV_Server.Components;
using NDVServerLib;
using NDVServerLib.Command;
using NDVServerLib.Config;

namespace NDV_Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            SocketServer.Start();

            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();
            // 添加控制器服务
            builder.Services.AddControllers();

            // 配置跨域（根据需求）
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll",
                    builder => builder.AllowAnyOrigin()
                                      .AllowAnyMethod()
                                      .AllowAnyHeader());
            });

            var app = builder.Build();

            // 使用跨域策略
            app.UseCors("AllowAll");

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error", createScopeForErrors: true);
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseAntiforgery();

            app.MapStaticAssets();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            // 配置控制器路由
            app.MapControllers();

            app.Run();
        }
    }


    public static class SocketServer
    {
        public static NDVServer Server { get; private set; }

        static SocketServer()
        {
            var config = new ServerConfig
            {
                IP = "0.0.0.0",
                Port = 8463,
                MessageQueueChannels = 1,
                MessageQueueSize = 1024,
                MessageQueueDelay = 2000,
                Key = "ffffffffffffffff"
            };
            var savedTask = config.SaveToFileAsync(DRXFile.ConfigPath);
            savedTask.Wait();
            bool saved = savedTask.Result;

            if (!saved)
            {
                Logger.Log("Server", "保存配置文件失败");
                var loadedTask = config.LoadFromFileAsync(DRXFile.ConfigPath);
                loadedTask.Wait();
                bool loaded = loadedTask.Result;
                if (!loaded)
                {
                    Logger.Log("Server", "加载配置文件失败");
                }
            }


            Server = new NDVServer(config);

            Server.OnDataReceived += (sender, e) =>
            {
                var packet = DRXPacket.Unpack(e.Packet, config.Key);
                Logger.Log("Server", $"Received packet: {packet?.Hash}");
            };
        }

        public static void Start()
        {
            InitCommanad();

            Server.BeginReceiveCommand();
            Server.OnError += (sender, e) =>
            {
                Logger.Log("Server", e.Message);
            };
            Server.Start();
        }

        public static void InitCommanad()
        {
            Server.RegisterCommand("test", new NDVRegister());
        }

        public static int GetConnectionCount()
        {
            return Server.GetConnectedSockets().Count;
        }

        public static HashSet<DRXSocket> GetConnectedSockets()
        {
            return Server.GetConnectedSockets();
        }
    }
}

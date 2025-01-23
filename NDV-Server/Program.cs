using DRX.Framework;
using DRX.Framework.Common;
using DRX.Framework.Common.Base;
using DRX.Framework.Common.Engine;
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
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            var app = builder.Build();

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

            SocketServer.Start();

            app.Run();
        }
    }


    public static class SocketServer
    {
        public static NdvServerEngine Server { get; private set; }

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
            var savedTask = config.SaveToFileAsync(DrxFile.ConfigPath);
            savedTask.Wait();
            bool saved = savedTask.Result;

            if (!saved)
            {
                Logger.Log("Server", "保存配置文件失败");
                var loadedTask = config.LoadFromFileAsync(DrxFile.ConfigPath);
                loadedTask.Wait();
                bool loaded = loadedTask.Result;
                if (!loaded)
                {
                    Logger.Log("Server", "加载配置文件失败");
                }
            }


            Server = new NdvServerEngine(config);

            Server.OnDataReceived += (sender, e) =>
            {
                var packet = DRXPacket.Unpack(e.Packet, config.Key);
            };
        }

        public static void Start()
        {
            InitCommanad();

            Server.OnError += (sender, e) =>
            {
                Logger.Log("Server", e.Message);
            };
            Server.Start();
            PluginEngine.LoadPlugins(Server);
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

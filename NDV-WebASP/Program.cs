using System.Text;
using NetworkCoreStandard;
using NetworkCoreStandard.Config;
using NetworkCoreStandard.Utils;
using NetworkCoreStandard.Utils.Common.Config;
using NetworkCoreStandard.Utils.Common.Models;
using NetworkCoreStandard.Utils.Extensions;

public partial class Program
{
    public static WebApplicationBuilder Builder { get; set; } = null!;
    public static WebApplication App { get; set; } = null!;
    public static NDVServer Server { get; set; } = null!;
    public static void Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
            // Windows平台还需要设置输入编码
            Console.InputEncoding = Encoding.UTF8;

            _ = LoadSocketServer();


            Builder = WebApplication.CreateBuilder(args);
            // 注册服务
            Builder.Services.AddControllers();
            Builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.WithOrigins("http://localhost:8462")  // 指定允许的源
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();  // 允许凭证
                });
            });

            Builder.Services.AddAuthorization();

            Builder.WebHost.UseUrls("http://0.0.0.0:8462");
            App = Builder.Build();

            // 中间件配置
            App.UseAuthentication();
            App.UseAuthorization();
            App.UseCors();
            App.MapControllers();

            // 配置默认文件选项
            var defaultFileOptions = new DefaultFilesOptions();
            defaultFileOptions.DefaultFileNames.Clear();
            defaultFileOptions.DefaultFileNames.Add("/Pages/Index.html");
            App.UseDefaultFiles(defaultFileOptions);

            // 添加静态文件支持
            App.UseStaticFiles();

            Server.Start();
            App.Run();

        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    public static async Task LoadSocketServer()
    {
        try
        {
            var config = new ServerConfig
            {
                IP = "0.0.0.0",
                Port = 8463,
                MessageQueueChannels = 1,
                MessageQueueSize = 1024,
                MessageQueueDelay = 500,
            };
            bool saved = await config.SaveToFileAsync(ConfigPath.ServerConfigPath);

            if (!saved)
            {
                Logger.Log("Server", "保存配置文件失败");
                bool loaded = await config.LoadFromFileAsync(ConfigPath.ServerConfigPath);
                if (!loaded)
                {
                    Logger.Log("Server", "加载配置文件失败");
                }
            }

            Server = new NDVServer(config);

            Server.AddListener("OnError", (sender, e) =>
            {
                Logger.Log(NetworkCoreStandard.Utils.LogLevel.Error, "Server", e.Message);
            });
            Server.AddListener("OnClientConnected", (sender, e) =>
            {
                Logger.Log("Server", $"客户端 {e.Socket.RemoteEndPoint} 已连接");
                Logger.Log("Server", $"当前连接数：{Server.GetConnectedSockets().Count}");
            });

            Server.BeginVerifyClient();
            Server.Start();
        }
        catch (Exception ex)
        {
            Logger.Log(NetworkCoreStandard.Utils.LogLevel.Error, "Server", ex.Message);
        }
    }
}
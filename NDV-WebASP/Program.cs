using System.Text;
using NetworkCoreStandard;
using NetworkCoreStandard.Components;
using NetworkCoreStandard.Config;
using NetworkCoreStandard.Enums;
using NetworkCoreStandard.EventArgs;
using NetworkCoreStandard.Extensions;
using NetworkCoreStandard.Models;
using NetworkCoreStandard.Script;
using NetworkCoreStandard.Utils;

public partial class Program
{
    public static WebApplicationBuilder Builder { get; set; } = null!;
    public static WebApplication App { get; set; } = null!;
    public static NetworkServer Server { get; set; } = null!;
    public static NetworkServerUDP ServerUDP { get; set; } = null!;
    public static LuaScriptEngine LuaEngine { get; set; } = null!;
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
            LuaEngine = new LuaScriptEngine();
            var config = new ServerConfig
            {
                IP = "0.0.0.0",
                Port = 8463,
                MaxClients = 100,
                TickRate = 1f / 30f,
                OnServerStartedTip = "服务器已启动"
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

            Server = new NetworkServer(config);
            LuaEngine.LoadFile($"{PathFinder.GetAppPath()}Scripts\\Main.lua", Server);
            Server.BeginHeartBeatListener(5000, TimeUnit.Minute, 10, false);
            Server.AddListener("OnError", (sender, e) =>
            {
                Logger.Log(NetworkCoreStandard.Utils.LogLevel.Error, "Server", e.Message);
            });

            // Server.Start(); // 由于在 Lua 脚本中启动，所以这里不需要再次启动
        }
        catch (Exception ex)
        {
            Logger.Log(NetworkCoreStandard.Utils.LogLevel.Error, "Server", ex.Message);
        }
    }
}
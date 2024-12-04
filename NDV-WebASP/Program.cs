using NetworkCoreStandard;
using NetworkCoreStandard.Events;
using NetworkCoreStandard.Extensions;

public partial class Program
{
    public static WebApplicationBuilder Builder { get; set; } = null!;
    public static WebApplication App { get; set; } = null!;
    public static NetworkServer Server { get; set; } = null!;
    public static void Main(string[] args)
    {
        try
        {
            var config = new ConnectionConfig
            {
                IP = "0.0.0.0",
                Port = 8463,
                MaxClients = 100,
                TickRate = 1f/30f,
            };
            Server = new NetworkServer(config);
            Server.BeginHeartBeatListener(5000, true);
            Server.Start();

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
            App.MapControllers();  // 替换 App.MapUserRoutes();

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
}
using NetworkCoreStandard;
using NetworkCoreStandard.Events;

public partial class Program
{
    public static WebApplicationBuilder Builder { get; set; } = null!;
    public static WebApplication App { get; set; } = null!;
    public static NetworkServer Server { get; set; } = null!;
    public static void Main(string[] args)
    {
        try
        {
            Builder = WebApplication.CreateBuilder(args);
            Server = new NetworkServer(8463);

            NetworkEventBus.AddListener("OnServerStarted", (sender, args) =>
            {
                Console.WriteLine($"[{args.Timestamp}] 服务器已启动，监听端口: {args.Socket.LocalEndPoint}");
            });

            Server.Start();


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
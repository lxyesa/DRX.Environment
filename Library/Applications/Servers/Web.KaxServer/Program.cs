using Web.KaxServer.Services;
using Drx.Sdk.Network.Session;
using Web.KaxServer.Models;
using Drx.Sdk.Network.Socket;
using System.Xml.Linq;
using Web.KaxServer.SocketCommands;
using Drx.Sdk.Network.Security;
using Drx.Sdk.Network.Socket.Services;
using Drx.Sdk.Text.Serialization;
using Microsoft.AspNetCore.OutputCaching;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();

// 添加输出缓存服务
builder.Services.AddOutputCache();

builder.Services.AddScoped<MessageBoxService>();
builder.Services.AddControllers(); // 添加Controllers服务

builder.Services.AddScoped<StoreService>(provider =>
{
    var env = provider.GetRequiredService<IWebHostEnvironment>();
    var userService = provider.GetRequiredService<IUserService>();
    var logger = provider.GetRequiredService<ILogger<StoreService>>();
    return new StoreService(env.ContentRootPath, userService, logger);
});

// 添加Session服务
builder.Services.AddDistributedMemoryCache();

// 添加自定义会话服务（验证码会话，有效期10分钟）
builder.Services.AddCustomSession<EmailVerificationSession>("KAX_VERIFY_");

builder.Services.AddHostedService<AssetCleanupService>();

// 添加socket服务
var socket = builder.Services.AddSocketService()
    .WithEncryption<AesEncryptor>();

CommandRegistry.RegisterCommands(socket);

builder.Services.AddSingleton<SessionManager>();
builder.Services.AddScoped<ICdkService, CdkService>();
builder.Services.AddSingleton<IUserService, UserService>();
builder.Services.AddSingleton<ForumDataHelper>();

builder.WebHost.UseUrls("http://*:8462");
builder.Services.AddLogging(logging =>{
    logging.AddConsole();
    logging.AddDebug();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// 在路由之后，授权之前，添加输出缓存中间件
app.UseOutputCache();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   // 直接在终结点上定义缓存策略
   .CacheOutput(policy => policy.Expire(TimeSpan.FromSeconds(30)));
app.MapControllers(); // 添加API控制器路由映射

app.Run();

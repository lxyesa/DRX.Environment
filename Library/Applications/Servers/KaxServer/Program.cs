using Drx.Sdk.Network.Session;
using KaxServer.Services;
using Microsoft.AspNetCore.Http;
using Drx.Sdk.Network.Extensions;
using Drx.Sdk.Network.Socket;
using System.Runtime.Intrinsics.Arm;
using Drx.Sdk.Network.Security;
using DRX.Framework;
using Drx.Sdk.Shared.ConsoleCommand;

#if DEBUG
Logger.Debug("KAX Server 正在以调试模式运行");
Logger.Debug("请注意，调试模式下可能会有额外的日志输出和性能开销");
#endif
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();

// 配置DRX会话系统
builder.Services.AddDRXSession(options =>
{
    options.ApplicationName = "KaxServer";
    options.KeysDirectory = "Data/Keys";
});

var socker = builder.Services.AddSocketService()
    .WithEncryption<AesEncryptor>();
socker.AddService<SocketClientService>();
SocketCommandRegister.Register(socker);

builder.Services.AddSingleton<EmailVerificationCode>();
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var senderEmail = config["Email:SenderEmail"] ?? string.Empty;
    var authCode = config["Email:AuthCode"] ?? string.Empty;
    var emailVerificationCode = sp.GetRequiredService<EmailVerificationCode>();
    return new EmailService(
        senderEmail,
        authCode,
        emailVerificationCode
    );
});

builder.WebHost.UseUrls("http://*:8462");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapControllers();
app.UseAuthorization();
app.UseDRXSession();
app.MapRazorPages();

// 注册命令控制器
new ConsoleCommandProcessor();

app.Run();
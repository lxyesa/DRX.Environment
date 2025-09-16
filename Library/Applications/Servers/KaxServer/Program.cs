using KaxServer.Services;
using Microsoft.AspNetCore.Http;
using Drx.Sdk.Network.Extensions;
using Drx.Sdk.Network.Socket;
using System.Runtime.Intrinsics.Arm;
using Drx.Sdk.Network.Security;
using Drx.Sdk.Shared.JavaScript;
using Microsoft.Extensions.FileProviders;
using KaxServer.Handlers;
using Drx.Sdk.Shared;

#if DEBUG
Logger.Debug("KAX Server 正在以调试模式运行");
Logger.Debug("请注意，调试模式下可能会有额外的日志输出和性能开销");
#endif
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// 启用 Razor Pages 并开启运行时编译与外部物理目录
var externalViews = @"D:\ExternalViews\KaxServer";
builder.Services
    .AddRazorPages()
    .AddRazorRuntimeCompilation(options =>
    {
        if (Directory.Exists(externalViews))
        {
            options.FileProviders.Add(new PhysicalFileProvider(externalViews));
        }
    });
builder.Services.AddHttpContextAccessor();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
    });
builder.Services.AddAuthorization();

// 配置DRX会话系统
builder.Services.AddDRXSession(options =>
{
    options.ApplicationName = "KaxServer";
    options.KeysDirectory = "Data/Keys";
});

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

// 顺序：先认证再授权
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.UseDRXSession();
app.MapRazorPages();
JavaScript.Execute("help.GetHelp()");
KaxSocket.Initialize();
KaxSocket.Start().GetAwaiter().GetResult();
app.Run();
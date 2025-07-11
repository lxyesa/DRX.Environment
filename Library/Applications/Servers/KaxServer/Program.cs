using Drx.Sdk.Network.Session;
using KaxServer.Services;
using Microsoft.AspNetCore.Http;
using Drx.Sdk.Network.Extensions;

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

app.UseAuthorization();
app.UseDRXSession();
app.MapRazorPages();
app.Run();

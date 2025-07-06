using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
// 添加API控制器支持
builder.Services.AddControllers();

// 设置应用程序端口为8462
builder.WebHost.UseUrls("http://*:8462");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// 启用静态文件
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();
app.MapRazorPages();
// 添加API路由映射
app.MapControllers();

app.Run();

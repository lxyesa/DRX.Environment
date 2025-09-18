var builder = WebApplication.CreateBuilder(args);

// 启用 Razor Pages 服务
builder.Services.AddRazorPages();

var app = builder.Build();
app.MapGet("/", () => Results.Redirect("/main"));

app.MapRazorPages();

app.Run();

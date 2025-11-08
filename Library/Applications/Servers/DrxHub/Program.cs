using Drx.Sdk.Network.V2.Web.Asp;
using Microsoft.AspNetCore.Builder;

// 添加一个简单的路由，返回一个 cshtml（作为 text/html 响应返回）
string cshtmlContent = @"@{ Layout = null; }
<!DOCTYPE html>
<html>
<head>
	<meta charset=""utf-8"" />
	<title>DrxHub CSHTML Demo</title>
</head>
<body>
	<h1>这是一个 cshtml 示例页面</h1>
	<p>当前时间：@DateTime.Now</p>
</body>
</html>";

DrxHttpAspServer drxHttpAsp = new DrxHttpAspServer(5000, app =>
{
	// 返回 cshtml 内容（注意：这里只是把 cshtml 模板原文返回为 HTML，未运行 Razor 引擎）
	// 尝试把路由注册到 IEndpointRouteBuilder（WebApplication 实现该接口）
	if (app is Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints)
	{
		endpoints.MapGet("/cshtml", async ctx =>
		{
			ctx.Response.ContentType = "text/html; charset=utf-8";
			await ctx.Response.WriteAsync(cshtmlContent);
		});
	}
	else
	{
		// 备选方案：在中间件中拦截请求路径
		app.Use(async (ctx, next) =>
		{
			if (ctx.Request.Path == "/cshtml")
			{
				ctx.Response.ContentType = "text/html; charset=utf-8";
				await ctx.Response.WriteAsync(cshtmlContent);
				return;
			}
			await next();
		});
	}
});

await drxHttpAsp.StartAsync();
using System;
using Drx.Sdk.Network.Http;
using Drx.Sdk.Network.Http.Protocol;

class Program
{
    static async Task Main()
    {
        var server = new DrxHttpServer(new[] { "http://localhost:8080/" });

        // 添加会话中间件 - 自动管理Cookie
        server.AddSessionMiddleware();

        // 登录路由 - 设置自动登录标记
        server.AddRoute("POST", "/login", (req) =>
        {
            var body = req.Body;
            // 简单的用户名密码验证 (实际应用中应该更安全)
            if (body.Contains("username=admin&password=123456"))
            {
                if (req.Session != null)
                {
                    req.Session.Data["user"] = "admin";
                    req.Session.Data["login_time"] = DateTime.Now;
                    req.Session.Data["auto_login"] = true; // 标记为自动登录
                    return new HttpResponse(200, "登录成功");
                }
            }
            return new HttpResponse(401, "用户名或密码错误");
        });

        // 检查登录状态路由
        server.AddRoute("GET", "/auth/status", (req) =>
        {
            if (req.Session?.Data.TryGetValue("user", out var user) == true)
            {
                var autoLogin = req.Session.Data.GetValueOrDefault("auto_login", false);
                return new HttpResponse(200, $"{{\"user\":\"{user}\",\"auto_login\":{autoLogin.ToString().ToLower()}}}");
            }
            return new HttpResponse(401, "未登录");
        });

        // 受保护的资源路由
        server.AddRoute("GET", "/protected", (req) =>
        {
            if (req.Session?.Data.ContainsKey("user") == true)
            {
                return new HttpResponse(200, "这是受保护的内容，只有登录用户才能访问");
            }
            return new HttpResponse(401, "需要登录才能访问");
        });

        // 购物车示例 - 演示会话数据持久化
        server.AddRoute("POST", "/cart/add", (req) =>
        {
            if (req.Session?.Data.ContainsKey("user") != true)
                return new HttpResponse(401, "请先登录");

            var item = req.Query["item"];
            if (string.IsNullOrEmpty(item))
                return new HttpResponse(400, "缺少商品参数");

            // 获取或创建购物车
            var cart = req.Session.Data.GetValueOrDefault("cart", new System.Collections.Generic.List<string>());
            if (cart is not System.Collections.Generic.List<string> cartList)
            {
                cartList = new System.Collections.Generic.List<string>();
            }

            cartList.Add(item);
            req.Session.Data["cart"] = cartList;

            return new HttpResponse(200, $"已添加商品: {item}");
        });

        // 提供HTML页面
        server.AddRoute("GET", "/login.html", (req) =>
        {
            var htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "login.html");
            if (File.Exists(htmlPath))
            {
                var html = File.ReadAllText(htmlPath);
                var response = new HttpResponse(200, html);
                response.Headers.Add("Content-Type", "text/html; charset=utf-8");
                return response;
            }
            return new HttpResponse(404, "页面未找到");
        });

        // 注销路由
        server.AddRoute("POST", "/logout", (req) =>
        {
            if (req.Session != null)
            {
                req.Session.Data.Clear();
                return new HttpResponse(200, "注销成功");
            }
            return new HttpResponse(500, "会话不可用");
        });

        await server.StartAsync();

        Console.WriteLine("=== 会话系统服务器已启动 ===");
        Console.WriteLine("浏览器客户端测试:");
        Console.WriteLine("1. 打开浏览器访问: http://localhost:8080/login.html");
        Console.WriteLine("2. 或直接使用curl测试:");
        Console.WriteLine("   登录: curl -X POST -d \"username=admin&password=123456\" http://localhost:8080/login");
        Console.WriteLine("   检查状态: curl http://localhost:8080/auth/status");
        Console.WriteLine("   访问保护内容: curl http://localhost:8080/protected");
        Console.WriteLine("   添加购物车: curl -X POST \"http://localhost:8080/cart/add?item=苹果\"");
        Console.WriteLine("   查看购物车: curl http://localhost:8080/cart");
        Console.WriteLine("");
        Console.WriteLine("注意: Cookie会自动在请求间传递，无需手动处理会话ID");
        Console.WriteLine("按任意键停止服务器...");

        Console.ReadKey();
        server.Stop();
    }
}
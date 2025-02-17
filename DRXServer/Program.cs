// See https://aka.ms/new-console-template for more information
using Drx.Sdk.Network;

class Program
{
    static async Task Main(string[] args)
    {
        // 创建 HTTP 服务器实例
        var server = new HttpServer("http://localhost:5000/");

        //

        // 启动服务器
        await server.StartAsync();
    }
}

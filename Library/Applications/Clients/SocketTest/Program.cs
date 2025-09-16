using Drx.Sdk.Shared;

namespace SocketTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // 创建 DrxTcpClient 实例
            var client = new Drx.Sdk.Network.Socket.DrxTcpClient();
            try
            {
                // 设置 AES 加密器
                var encryptor = new Drx.Sdk.Network.Security.AesEncryptor();
                // client.SetEncryptor(encryptor);

                // await client.ConnectAsync("1.116.135.26", 8463);
                await client.ConnectAsync("127.0.0.1", 8463);
                Console.WriteLine("已连接到服务器 1.116.135.26:8463");

                var heartbeat = new
                {
                    command = "ping",
                };

                await client.SendPacketAsync(heartbeat, (c, data) =>
                {
                    Logger.Info($"心跳响应: {data}");
                }, TimeSpan.FromSeconds(30));
                
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发生异常: {ex.Message}");
            }
        }
    }
}
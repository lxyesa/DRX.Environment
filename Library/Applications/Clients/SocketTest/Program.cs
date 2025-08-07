using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Network.DataBase;
using Microsoft.Data.Sqlite;
using System.IO;
using DRX.Framework;
using System.Threading.Tasks.Dataflow;
using System.Text.Json;
using Drx.Sdk.Network.Extensions;
using System.Net.Http;
using System.Net.Http.Headers;

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
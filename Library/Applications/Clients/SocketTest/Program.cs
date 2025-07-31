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
                client.SetEncryptor(encryptor);

                // await client.ConnectAsync("1.116.135.26", 8463);
                await client.ConnectAsync("127.0.0.1", 8463);
                Console.WriteLine("已连接到服务器 1.116.135.26:8463");

                var heartbeat = new
                {
                    command = "heartbeat",
                };

                var login = new
                {
                    command = "login",
                    args = new[]{
                        new { username = "DRX", password = "Xiren123456" },
                    }
                };

                var getuid = new
                {
                    command = "getuid",
                };

                await client.SendPacketAsync(heartbeat, (c, data) =>
                {
                    var response = Encoding.UTF8.GetString(data);
                    var message = response.GetJsonProperty<string>("message");
                    var statusCode = response.GetJsonProperty<uint>("status_code");

                    Logger.Info($"心跳响应: {message}, 状态码: 0x{statusCode:X2}");
                }, TimeSpan.FromSeconds(30));

                await client.SendPacketAsync(login, (c, data) =>
                {
                    var response = Encoding.UTF8.GetString(data);
                    var message = response.GetJsonProperty<string>("message");
                    var statusCode = response.GetJsonProperty<uint>("status_code");
                    var userToken = response.GetJsonProperty<string>("user_token");

                    Logger.Info($"登录响应: {message}, 状态码: 0x{statusCode:X2}, 用户令牌: {userToken}");
                }, TimeSpan.FromSeconds(30));

                await client.SendPacketAsync(login, (c, data) =>
                {
                    var response = Encoding.UTF8.GetString(data);
                    var message = response.GetJsonProperty<string>("message");
                    var statusCode = response.GetJsonProperty<uint>("status_code");
                    var userToken = response.GetJsonProperty<string>("user_token");

                    Logger.Info($"登录响应: {message}, 状态码: 0x{statusCode:X2}, 用户令牌: {userToken ?? "未获取"}");
                }, TimeSpan.FromSeconds(30));

                // 等待一段时间以确保所有操作完成
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发生异常: {ex.Message}");
            }
        }
    }
}
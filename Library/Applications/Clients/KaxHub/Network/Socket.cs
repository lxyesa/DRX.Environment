using Drx.Sdk.Network.V2.Socket.Client;
using Drx.Sdk.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace KaxHub.Network
{
    public static class Socket
    {
        public const int Port = 8462;
        public const string Host = "127.0.0.1";
        public readonly static DrxTcpClient drxTcpClient = new DrxTcpClient();

        private static bool connected = false;

        public static async Task Initialize()
        {
            connected = await drxTcpClient.ConnectAsync(Host, Port, 8000);
            if (connected)
            {
                Logger.Info("Connected to KaxSocket server.");
            }
            else
            {
                MessageBox.Show("无法连接至服务器，请检查你的网络连接，若无任何异常，请重启应用程序。", "连接错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static bool IsConnected()
        {
            return connected && drxTcpClient.Connected;
        }
    }
}

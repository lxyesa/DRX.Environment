using DRX.Framework;
using DRX.Framework.Common.Models;
using Microsoft.Graph.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DRX.Framework.Common.Args;
using DRX.Library.Kax;

namespace NDVDemoWinUI.Models
{
    public sealed class NetManager
    {
        private static readonly Lazy<NetManager> Lazy =
            new Lazy<NetManager>(() => new NetManager(), true);
        private static readonly DrxClient? ClientSocket;
        public static NetManager Instance => Lazy.Value;

        static NetManager()
        {
            ClientSocket = new DrxClient("127.0.0.1", 8463, "ffffffffffffffff");
            ClientSocket.OnErrorCallback += _clientSocket_OnErrorCallback;
            ClientSocket.OnReceiveCallback += _clientSocket_OnReceiveCallback;
            ClientSocket.OnConnectedCallback += _clientSocket_OnConnectedCallback;
        }

        /* ----------------------- 字段属性 ----------------------- */
        private static bool _isConnected = false;

        /* ----------------------- 事件处理/声明 ----------------------- */

        private static void _clientSocket_OnErrorCallback(object? sender, DRX.Framework.Common.Args.NetworkEventArgs e)
        {
#if DEBUG
            Logger.Error(e?.Message);
#endif
        }

        private static void _clientSocket_OnReceiveCallback(object? sender, DRX.Framework.Common.Args.NetworkEventArgs e)
        {
            // throw new NotImplementedException();
        }
        private static void _clientSocket_OnConnectedCallback(object? sender, NetworkEventArgs e)
        {
#if DEBUG
            Logger.Debug("已经成功连接到服务器");
#endif
            _isConnected = true;    // 连接成功, 设置连接状态
        }

        /* ----------------------- 公开方法 ----------------------- */
        public void Connect()
        {
            ClientSocket?.Connect();
        }

        public bool IsConnected() => _isConnected;
        public DrxClient? GetClient() => ClientSocket;
    }
}

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Network.Security;

// 注释全部采用中文
namespace Drx.Sdk.Network.Socket
{
    // 简单的 UDP 客户端包装，提供发送、接收和可选的加密器支持
    public class DrxUdpClient : IDisposable
    {
        private UdpClient _udp = null!;
        private IPEndPoint? _remoteEndPoint;
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private IPacketEncryptor? _encryptor;
        private bool _disposed;

        public bool Connected => _udp != null && _remoteEndPoint != null;

        public DrxUdpClient()
        {
        }

        // 连接到远端（UDP 本质上不保持连接，但我们保存远端地址以便发送）
        public bool Connect(string host, int port)
        {
            if (string.IsNullOrEmpty(host) || port <= 0) return false;
            try
            {
                var addresses = Dns.GetHostAddresses(host);
                if (addresses == null || addresses.Length == 0) return false;
                _remoteEndPoint = new IPEndPoint(addresses[0], port);
                _udp = new UdpClient();
                return true;
            }
            catch
            {
                return false;
            }
        }

        // 发送数据（异步），返回是否发送成功
        public async Task<bool> SendAsync(byte[] data, int timeoutMilliseconds = 5000)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DrxUdpClient));
            if (!Connected || data == null) return false;
            await _sendLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var payload = _encryptor is not null ? _encryptor.Encrypt(data) : data;
                var sendTask = _udp.SendAsync(payload, payload.Length, _remoteEndPoint!);
                if (timeoutMilliseconds <= 0) { await sendTask.ConfigureAwait(false); return true; }
                var completed = await Task.WhenAny(sendTask, Task.Delay(timeoutMilliseconds)).ConfigureAwait(false);
                return completed == sendTask;
            }
            catch
            {
                return false;
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public void SetEncryptor(IPacketEncryptor encryptor)
        {
            _encryptor = encryptor;
        }

        // 关闭/断开
        public void Disconnect()
        {
            if (_udp != null)
            {
                try { _udp.Close(); } catch { }
                _udp = null!;
            }
            _remoteEndPoint = null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _udp?.Dispose(); } catch { }
            _sendLock.Dispose();
        }
    }
}

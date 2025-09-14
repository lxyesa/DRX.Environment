using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Network.Security;
using DRX.Framework;

namespace Drx.Sdk.Network.Socket
{
    /// <summary>
    /// 简单的 UDP 服务器实现：在指定端口监听 UDP 包，支持可选的加密/完整性处理，
    /// 并通过事件/回调用于上层处理收到的数据和发送回复。
    /// 注释全部采用中文，风格与库内其他类一致。
    /// </summary>
    public class DrxUdpServer : IDisposable
    {
        private readonly UdpClient _udp;
        private readonly IPEndPoint _localEp;
        private readonly IPacketEncryptor? _encryptor;
        private readonly IPacketIntegrityProvider? _integrity;
        private CancellationTokenSource? _cts;

        /// <summary>
        /// 当接收到明文字节时触发。处理器可以返回要发送回客户端的响应（明文字节），返回 null 表示不发送回复。
        /// 参数：远端端点、收到的数据、取消令牌 -> 返回响应字节或 null
        /// </summary>
        public Func<IPEndPoint, ReadOnlyMemory<byte>, CancellationToken, Task<byte[]?>>? OnReceiveAsync { get; set; }

        public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;

        public DrxUdpServer(int port, IPacketEncryptor? encryptor = null, IPacketIntegrityProvider? integrity = null)
        {
            _localEp = new IPEndPoint(IPAddress.Any, port);
            _udp = new UdpClient(_localEp);
            _encryptor = encryptor;
            _integrity = integrity;
            if (_encryptor != null && _integrity != null)
                throw new InvalidOperationException("不能同时启用加密与完整性校验，请选择一种。");
        }

        /// <summary>
        /// 启动异步接收循环
        /// </summary>
        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (IsRunning) return Task.CompletedTask;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = _cts.Token;
            Task.Run(() => ReceiveLoopAsync(token), token);
            Logger.Info($"[udp] UDP 服务器已启动，监听: {_localEp}");
            return Task.CompletedTask;
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    UdpReceiveResult result;
                    try
                    {
                        result = await _udp.ReceiveAsync().ConfigureAwait(false);
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (SocketException ex)
                    {
                        Logger.Error($"[udp] 接收 UDP 包发生 SocketException: {ex}");
                        await Task.Delay(1000, token).ConfigureAwait(false);
                        continue;
                    }

                    var remote = result.RemoteEndPoint;
                    var data = result.Buffer;

                    // 先尝试解密/验签
                    byte[]? payload = data;
                    if (_encryptor != null)
                    {
                        payload = _encryptor.Decrypt(data);
                        if (payload == null)
                        {
                            Logger.Warn($"[udp] 来自 {remote} 的包解密失败，已忽略。");
                            continue;
                        }
                    }
                    else if (_integrity != null)
                    {
                        payload = _integrity.Unprotect(data);
                        if (payload == null)
                        {
                            Logger.Warn($"[udp] 来自 {remote} 的包完整性校验失败，已忽略。");
                            continue;
                        }
                    }

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            if (OnReceiveAsync != null)
                            {
                                var resp = await OnReceiveAsync(remote, payload, token).ConfigureAwait(false);
                                if (resp != null && resp.Length > 0)
                                {
                                    byte[] toSend = resp;
                                    if (_encryptor != null)
                                        toSend = _encryptor.Encrypt(toSend);
                                    else if (_integrity != null)
                                        toSend = _integrity.Protect(toSend);

                                    try
                                    {
                                        await _udp.SendAsync(toSend, toSend.Length, remote).ConfigureAwait(false);
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Error($"[udp] 发送响应到 {remote} 失败: {ex}");
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"[udp] 处理收到的包时发生异常: {ex}");
                        }
                    }, token);
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info("[udp] 接收循环已取消。");
            }
            catch (Exception ex)
            {
                Logger.Error($"[udp] 接收循环发生未处理异常: {ex}");
            }
            finally
            {
                Logger.Info("[udp] 接收循环退出。即将关闭 UdpClient。");
            }
        }

        public Task StopAsync()
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
            try { _udp.Close(); } catch { }
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            try { _cts?.Cancel(); } catch { }
            try { _udp.Dispose(); } catch { }
        }
    }
}

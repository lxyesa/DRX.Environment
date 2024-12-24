using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Buffers;
using NetworkCoreStandard.EventArgs;
using NetworkCoreStandard.Components;
using NetworkCoreStandard.Utils.Common;
using NetworkCoreStandard.Utils.Common.Pool;
using NetworkCoreStandard.Utils.Extensions;
using System.Runtime.CompilerServices;
using NetworkCoreStandard.Models;

namespace NetworkCoreStandard.Utils.Common;

public abstract class DRXServer : DRXBehaviour
{
    #region 字段
    protected DRXSocket _socket;
    protected int _port;
    protected string _ip = string.Empty;
    protected readonly ConcurrentDictionary<DRXSocket, byte> _clients = new();
    protected readonly DRXQueuePool _messageQueue;
    protected const int BUFFER_SIZE = 8192;
    #endregion

    #region 构造函数
    protected DRXServer()
    {
        _socket = new DRXSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        // 初始化消息队列
        _messageQueue = new DRXQueuePool(
            maxChannels: Environment.ProcessorCount,
            maxQueueSize: 10000,
            defaultDelay: 500
        );

        // 订阅队列事件
        _messageQueue.ItemFailed += (sender, args) =>
        {
            _ = PushEventAsync("OnError", new NetworkEventArgs(
                socket: null!,
                eventType: NetworkEventType.HandlerEvent,
                message: $"消息处理失败: {args.Exception.Message}"
            ));
        };
    }

    protected DRXServer(int maxChannels, int maxQueueSize, int defaultDelay)
    {
        _socket = new DRXSocket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        // 初始化消息队列
        _messageQueue = new DRXQueuePool(
            maxChannels: maxChannels,
            maxQueueSize: maxQueueSize,
            defaultDelay: defaultDelay
        );

        // 订阅队列事件
        _messageQueue.ItemFailed += (sender, args) =>
        {
            _ = PushEventAsync("OnError", new NetworkEventArgs(
                socket: null!,
                eventType: NetworkEventType.HandlerEvent,
                message: $"消息处理失败: {args.Exception.Message}"
            ));
        };
    }

    #endregion

    #region 核心方法
    /// <summary>
    /// 启动服务器
    /// </summary>
    /// <param name="ip">服务器IP地址</param>
    /// <param name="port">服务器端口</param>
    public virtual void Start(string ip, int port)
    {
        try
        {
            InitializeServer(ip, port);
            StartListening();
            NotifyServerStarted();
        }
        catch (Exception ex)
        {
            HandleStartupError(ex);
        }
    }

    /// <summary>
    /// 初始化服务器配置
    /// </summary>
    /// <param name="ip">服务器IP地址</param>
    /// <param name="port">服务器端口</param>
    protected virtual void InitializeServer(string ip, int port)
    {
        _ip = ip;
        _port = port;
        _socket.Bind(new IPEndPoint(IPAddress.Parse(_ip), _port));
    }

    /// <summary>
    /// 开始监听客户端连接
    /// </summary>
    protected virtual void StartListening()
    {
        _socket.Listen(10);
        _ = _socket.BeginAccept(AcceptCallback, null);
    }

    /// <summary>
    /// 通知服务器启动事件
    /// </summary>
    protected virtual void NotifyServerStarted()
    {
        _ = PushEventAsync("OnServerStarted", new NetworkEventArgs(
            socket: _socket,
            eventType: NetworkEventType.ServerStarted,
            message: $"服务器已启动，监听 {_ip}:{_port}"
        ));
    }

    /// <summary>
    /// 处理服务器启动错误
    /// </summary>
    /// <param name="ex">异常信息</param>
    protected virtual void HandleStartupError(Exception ex)
    {
        _ = PushEventAsync("OnError", new NetworkEventArgs(
            socket: _socket,
            eventType: NetworkEventType.HandlerEvent,
            message: $"启动服务器时发生错误: {ex.Message}"
        ));
    }

    /// <summary>
    /// 停止服务器
    /// </summary>
    public virtual void Stop()
    {
        if (_socket?.IsBound != true) return;

        try
        {
            StopMessageQueue();
            CloseServerSocket();
            ClearConnections();
        }
        catch (Exception ex)
        {
            HandleStopError(ex);
        }
    }

    /// <summary>
    /// 停止消息队列
    /// </summary>
    protected virtual void StopMessageQueue()
    {
        _messageQueue.Stop();
        _messageQueue.Dispose();
    }

    /// <summary>
    /// 关闭服务器Socket
    /// </summary>
    protected virtual void CloseServerSocket()
    {
        _socket.Close();
    }

    /// <summary>
    /// 清理所有客户端连接
    /// </summary>
    protected virtual void ClearConnections()
    {
        foreach (DRXSocket client in _clients.Keys)
        {
            _ = HandleDisconnectAsync(client);
        }
        _clients.Clear();
    }

    /// <summary>
    /// 处理服务器停止错误
    /// </summary>
    /// <param name="ex">异常信息</param>
    protected virtual void HandleStopError(Exception ex)
    {
        _ = PushEventAsync("OnError", new NetworkEventArgs(
            socket: _socket,
            eventType: NetworkEventType.HandlerEvent,
            message: $"停止服务器时发生错误: {ex.Message}"
        ));
    }
    #endregion

    #region 客户端连接管理
    /// <summary>
    /// 处理客户端连接回调
    /// </summary>
    /// <param name="ar">异步操作结果</param>
    protected virtual async void AcceptCallback(IAsyncResult ar)
    {
        DRXSocket? clientSocket = null;
        try
        {
            clientSocket = await AcceptClientSocketAsync(ar);
            if (clientSocket != null)
            {
                await HandleNewClientAsync(clientSocket);
            }
        }
        catch (Exception ex)
        {
            await HandleAcceptErrorAsync(clientSocket, ex);
        }
        finally
        {
            ContinueAccepting();
        }
    }

    /// <summary>
    /// 接受客户端Socket连接
    /// </summary>
    /// <param name="ar">异步操作结果</param>
    /// <returns>转换后的DRXSocket对象，如果失败返回null</returns>
    protected virtual async Task<DRXSocket?> AcceptClientSocketAsync(IAsyncResult ar)
    {
        Socket baseSocket = _socket.EndAccept(ar);
        return baseSocket.TakeOver<DRXSocket>();
    }

    /// <summary>
    /// 处理新的客户端连接
    /// </summary>
    /// <param name="clientSocket">客户端Socket对象</param>
    protected virtual async Task HandleNewClientAsync(DRXSocket clientSocket)
    {
        if (_clients.TryAdd(clientSocket, 1))
        {
            await InitializeClientSocket(clientSocket);
            BeginReceive(clientSocket);
        }
        else
        {
            clientSocket.Close();
        }
    }

    /// <summary>
    /// 初始化客户端Socket
    /// </summary>
    /// <param name="clientSocket">客户端Socket对象</param>
    protected virtual async Task InitializeClientSocket(DRXSocket clientSocket)
    {
        _ = clientSocket.AddComponent<ClientComponent>();
        await PushEventAsync("OnClientConnected", new NetworkEventArgs(
            socket: clientSocket,
            eventType: NetworkEventType.ServerClientConnected
        ));
    }

    /// <summary>
    /// 处理接受连接时的错误
    /// </summary>
    /// <param name="clientSocket">客户端Socket对象</param>
    /// <param name="ex">异常信息</param>
    protected virtual async Task HandleAcceptErrorAsync(DRXSocket? clientSocket, Exception ex)
    {
        await PushEventAsync("OnError", new NetworkEventArgs(
            socket: clientSocket,
            eventType: NetworkEventType.HandlerEvent,
            message: $"处理客户端连接时发生错误: {ex.Message}"
        ));
        clientSocket?.Close();
    }

    /// <summary>
    /// 继续接受新的客户端连接
    /// </summary>
    protected virtual void ContinueAccepting()
    {
        if (_socket?.IsBound == true)
        {
            try
            {
                _socket.BeginAccept(AcceptCallback, null);
            }
            catch (Exception ex)
            {
                _ = PushEventAsync("OnError", new NetworkEventArgs(
                    socket: _socket,
                    eventType: NetworkEventType.HandlerEvent,
                    message: $"继续接受连接时发生错误: {ex.Message}"
                ));
            }
        }
    }

    /// <summary>
    /// 断开指定客户端连接
    /// </summary>
    /// <param name="clientSocket">要断开的客户端Socket</param>
    /// <returns>断开操作是否成功</returns>
    public virtual bool DisconnectClient(DRXSocket clientSocket)
    {
        try
        {
            _ = HandleDisconnectAsync(clientSocket);
            return true;
        }
        catch (Exception ex)
        {
            _ = PushEventAsync("OnError", new NetworkEventArgs(
                socket: clientSocket,
                eventType: NetworkEventType.HandlerEvent,
                message: $"断开客户端连接时发生错误: {ex.Message}"
            ));
            return false;
        }
    }

    /// <summary>
    /// 处理客户端断开连接
    /// </summary>
    /// <param name="clientSocket">断开连接的客户端Socket</param>
    protected virtual async Task HandleDisconnectAsync(DRXSocket clientSocket)
    {
        try
        {
            await HandleClientDisconnection(clientSocket);
        }
        catch (Exception ex)
        {
            await HandleDisconnectErrorAsync(clientSocket, ex);
        }
    }

    /// <summary>
    /// 处理客户端断开连接的具体逻辑
    /// </summary>
    /// <param name="clientSocket">断开连接的客户端Socket</param>
    protected virtual async Task HandleClientDisconnection(DRXSocket clientSocket)
    {
        if (_clients.TryRemove(clientSocket, out _))
        {
            await PushEventAsync("OnClientDisconnected", new NetworkEventArgs(
                socket: clientSocket,
                eventType: NetworkEventType.ServerClientDisconnected
            ));
        }
        CloseSocketSafely(clientSocket);
    }

    /// <summary>
    /// 处理断开连接时的错误
    /// </summary>
    /// <param name="clientSocket">客户端Socket</param>
    /// <param name="ex">异常信息</param>
    protected virtual async Task HandleDisconnectErrorAsync(DRXSocket clientSocket, Exception ex)
    {
        await PushEventAsync("OnError", new NetworkEventArgs(
            socket: clientSocket,
            eventType: NetworkEventType.HandlerEvent,
            message: $"处理断开连接时发生错误: {ex.Message}"
        ));
    }

    /// <summary>
    /// 安全关闭Socket连接
    /// </summary>
    /// <param name="socket">要关闭的Socket对象</param>
    protected virtual void CloseSocketSafely(Socket socket)
    {
        try
        {
            if (socket.Connected)
            {
                try { socket.Shutdown(SocketShutdown.Both); }
                catch { } // 忽略潜在的异常
                finally { socket.Close(); }
            }
        }
        catch { } // 忽略关闭过程中的异常
    }
    #endregion

    #region 数据处理
    /// <summary>
    /// 开始接收数据
    /// </summary>
    protected virtual void BeginReceive(DRXSocket clientSocket)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(BUFFER_SIZE);
        try
        {
            _ = clientSocket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None,
                ar =>
                {
                    try
                    {
                        HandleDataReceived(ar, clientSocket, buffer);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }, null);
        }
        catch (Exception)
        {
            ArrayPool<byte>.Shared.Return(buffer);
            _ = HandleDisconnectAsync(clientSocket);
        }
    }

    /// <summary>
    /// 处理接收到的数据
    /// </summary>
    protected virtual void HandleDataReceived(IAsyncResult ar, DRXSocket clientSocket, byte[] buffer)
    {
        try
        {
            int bytesRead = clientSocket.EndReceive(ar);
            if (bytesRead > 0)
            {
                // 将数据处理委托给消息队列
                var data = new byte[bytesRead];
                Buffer.BlockCopy(buffer, 0, data, 0, bytesRead);

                _ = _messageQueue.PushAsync(
                    () => ProcessReceivedData(clientSocket, data), 0);
                BeginReceive(clientSocket);
            }
            else
            {
                _ = HandleDisconnectAsync(clientSocket);
            }
        }
        catch
        {
            _ = HandleDisconnectAsync(clientSocket);
        }
    }

    /// <summary>
    /// 处理接收到的数据包
    /// </summary>
    protected virtual void ProcessReceivedData(DRXSocket clientSocket, byte[] data)
    {
        try
        {
            OnDataReceived(clientSocket, data);
        }
        catch (Exception ex)
        {
            _ = PushEventAsync("OnError", new NetworkEventArgs(
                socket: clientSocket,
                eventType: NetworkEventType.HandlerEvent,
                message: $"处理数据时发生错误: {ex.Message}"
            ));
        }
    }

    /// <summary>
    /// 数据接收事件
    /// </summary>
    protected virtual void OnDataReceived(DRXSocket clientSocket, byte[] data)
    {
        _ = PushEventAsync("OnDataReceived", new NetworkEventArgs(
            socket: clientSocket,
            eventType: NetworkEventType.DataReceived,
            packet: data
        ));
    }
    #endregion

    #region 工具方法
    public virtual HashSet<DRXSocket> GetConnectedSockets()
    {
        return new HashSet<DRXSocket>(_clients.Keys);
    }
    #endregion

    protected override void OnDestroy()
    {
        Stop();
        base.OnDestroy();
    }

    #region 消息发送
    /// <summary>
    /// 预处理待发送的数据
    /// </summary>
    protected virtual byte[] PrepareDataForSend(byte[] data)
    {
        var sendBuffer = ArrayPool<byte>.Shared.Rent(data.Length);
        Buffer.BlockCopy(data, 0, sendBuffer, 0, data.Length);
        return sendBuffer;
    }

    /// <summary>
    /// 创建发送回调
    /// </summary>
    protected virtual AsyncCallback CreateSendCallback(byte[] buffer)
    {
        return ar =>
        {
            try
            {
                HandleSendCallback(ar);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        };
    }

    /// <summary>
    /// 验证客户端连接状态
    /// </summary>
    protected virtual bool ValidateClientForSend(DRXSocket clientSocket)
    {
        if (!_clients.ContainsKey(clientSocket))
        {
            _ = PushEventAsync("OnError", new NetworkEventArgs(
                socket: clientSocket,
                eventType: NetworkEventType.HandlerEvent,
                message: "客户端未连接"
            ));
            return false;
        }
        return true;
    }

    /// <summary>
    /// 执行实际的数据发送
    /// </summary>
    protected virtual void ExecuteSend(DRXSocket clientSocket, byte[] buffer, int length)
    {
        _ = clientSocket.BeginSend(
            buffer,
            0,
            length,
            SocketFlags.None,
            CreateSendCallback(buffer),
            clientSocket
        );
    }

    /// <summary>
    /// 处理发送完成的回调
    /// </summary>
    protected virtual void HandleSendCallback(IAsyncResult ar)
    {
        if (ar.AsyncState is not DRXSocket clientSocket) return;

        try
        {
            int bytesSent = clientSocket.EndSend(ar);
            OnSendComplete(clientSocket, bytesSent);
        }
        catch (Exception ex)
        {
            OnSendError(clientSocket, ex);
        }
    }

    /// <summary>
    /// 发送完成时触发
    /// </summary>
    protected virtual void OnSendComplete(DRXSocket clientSocket, int bytesSent)
    {
        _ = PushEventAsync("OnDataSent", new NetworkEventArgs(
            socket: clientSocket,
            eventType: NetworkEventType.HandlerEvent,
            message: $"Successfully sent {bytesSent} bytes"
        ));
    }

    /// <summary>
    /// 发送错误时触发
    /// </summary>
    protected virtual void OnSendError(DRXSocket clientSocket, Exception ex)
    {
        _ = PushEventAsync("OnError", new NetworkEventArgs(
            socket: clientSocket,
            eventType: NetworkEventType.HandlerEvent,
            message: $"发送数据时发生错误: {ex.Message}"
        ));
        _ = HandleDisconnectAsync(clientSocket);
    }

    /// <summary>
    /// 向指定客户端发送数据
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual void Send(DRXSocket clientSocket, byte[] data)
    {
        try
        {
            if (!ValidateClientForSend(clientSocket)) return;

            byte[] sendBuffer = PrepareDataForSend(data);
            ExecuteSend(clientSocket, sendBuffer, data.Length);
        }
        catch (Exception ex)
        {
            OnSendError(clientSocket, ex);
        }
    }

    /// <summary>
    /// 向所有已连接的客户端广播数据
    /// </summary>
    public virtual async Task BroadcastAsync(byte[] data)
    {
        var tasks = new List<Task>();
        var deadClients = new ConcurrentBag<DRXSocket>();
        var buffer = PrepareDataForSend(data);

        try
        {
            foreach (DRXSocket client in _clients.Keys)
            {
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        ExecuteSend(client, buffer, data.Length);
                    }
                    catch
                    {
                        deadClients.Add(client);
                    }
                }));
            }

            await Task.WhenAll(tasks);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);

            // 清理断开的连接
            foreach (var client in deadClients)
            {
                _ = HandleDisconnectAsync(client);
            }
        }
    }
    #endregion
}
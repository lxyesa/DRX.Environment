using System.IO.Pipes;
using NetworkCoreStandard.Utils;
using NetworkCoreStandard.Utils.Common;
using NetworkStandard.Pip;
using NetworkStandard.Pip.Config;

public class PipeServer : DRXBehaviour, IDisposable
{
    private static PipeServer? _pipeServer;
    private static PipeServerConfig? _config;
    private readonly PipeMessageQueue _messageQueue;
    private readonly PipeConnectionPool _connectionPool;
    private readonly CancellationTokenSource _cts;
    private bool _isRunning;
    private readonly object _lock = new();

    public bool IsRunning => _isRunning;

    public PipeServer(PipeServerConfig config)
    {
        _config = config;
        _cts = new CancellationTokenSource();
        _messageQueue = new PipeMessageQueue(config.MaxQueueSize);
        _connectionPool = new PipeConnectionPool(config.MaxConnections);
        _isRunning = false;
    }

    public async Task StartAsync()
    {
        if (_isRunning) return;

        try
        {
            Logger.Log("PipeServer", $"正在启动管道服务器 {_config?.PipeName}");
            _isRunning = true;

            // 启动消息处理
            _ = _messageQueue.StartProcessingAsync(HandleMessageAsync);
            
            // 开始监听客户端
            await ListenForClientsAsync();
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "PipeServer", $"启动管道服务器时出错: {ex.Message}");
            throw;
        }
    }

    private async Task ListenForClientsAsync()
    {
        while (!_cts.Token.IsCancellationRequested && _isRunning)
        {
            var pipeStream = new NamedPipeServerStream(
                _config?.PipeName ?? "NDV_Pipe",
                PipeDirection.InOut,
                _config?.MaxConnections ?? 1,
                _config?.TransmissionMode ?? PipeTransmissionMode.Byte,
                _config?.PipeOptions ?? PipeOptions.Asynchronous
            );

            try
            {
                await pipeStream.WaitForConnectionAsync(_cts.Token);
                var clientId = Guid.NewGuid().ToString();
                
                if (_connectionPool.TryAdd(clientId, pipeStream))
                {
                    Logger.Log("PipeServer", $"客户端 {clientId} 已连接");
                    _ = HandleClientAsync(clientId, pipeStream);
                }
                else
                {
                    Logger.Log(LogLevel.Warning, "PipeServer", "连接池已满，拒绝新的连接");
                    pipeStream.Dispose();
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.Log(LogLevel.Error, "PipeServer", $"处理客户端连接时出错: {ex.Message}");
            }
        }
    }

    private async Task HandleClientAsync(string clientId, NamedPipeServerStream pipeStream)
    {
        var buffer = new byte[_config?.BufferSize ?? 4096];

        try
        {
            while (pipeStream.IsConnected && !_cts.Token.IsCancellationRequested)
            {
                int bytesRead = await pipeStream.ReadAsync(buffer, 0, buffer.Length, _cts.Token);
                if (bytesRead == 0) break;

                var message = new PipeMessage(clientId, buffer.Take(bytesRead).ToArray());
                if (!_messageQueue.TryEnqueue(message))
                {
                    Logger.Log(LogLevel.Warning, "PipeServer", "消息队列已满，丢弃消息");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log(LogLevel.Error, "PipeServer", $"处理客户端 {clientId} 消息时出错: {ex.Message}");
        }
        finally
        {
            _connectionPool.TryRemove(clientId);
            pipeStream.Dispose();
        }
    }

    private async Task HandleMessageAsync(PipeMessage message)
    {
        if (_connectionPool.TryGetConnection(message.ClientId, out var pipeStream))
        {
            try
            {
                // 这里处理消息，示例中简单回显
                await pipeStream.WriteAsync(message.Data, 0, message.Data.Length, _cts.Token);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "PipeServer", $"处理消息时出错: {ex.Message}");
            }
        }
    }

    public async Task BroadcastAsync(byte[] data)
    {
        foreach (var pipe in _connectionPool.GetAllConnections())
        {
            try
            {
                await pipe.WriteAsync(data, 0, data.Length, _cts.Token);
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "PipeServer", $"广播消息时出错: {ex.Message}");
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (!_isRunning) return;

            _isRunning = false;
            _cts.Cancel();
            _messageQueue.Stop();
            
            foreach (var pipe in _connectionPool.GetAllConnections())
            {
                pipe.Dispose();
            }
        }
    }

    public void Dispose()
    {
        Stop();
        _cts.Dispose();
    }
}
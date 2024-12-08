using System;
using System.Collections.Concurrent;
using NetworkCoreStandard.Utils;

namespace NetworkStandard.Pip;

public class PipeMessageQueue
{
    private readonly ConcurrentQueue<PipeMessage> _messageQueue = new();
    private readonly CancellationTokenSource _processingCts = new();
    private readonly int _maxQueueSize;
    private bool _isProcessing;

    public PipeMessageQueue(int maxQueueSize = 1000)
    {
        _maxQueueSize = maxQueueSize;
    }

    public bool TryEnqueue(PipeMessage message)
    {
        if (_messageQueue.Count >= _maxQueueSize)
            return false;
            
        _messageQueue.Enqueue(message);
        return true;
    }

    public async Task StartProcessingAsync(Func<PipeMessage, Task> messageHandler)
    {
        if (_isProcessing) return;
        _isProcessing = true;

        while (!_processingCts.Token.IsCancellationRequested)
        {
            if (_messageQueue.TryDequeue(out var message))
            {
                try
                {
                    await messageHandler(message);
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Error, "PipeMessageQueue", $"处理消息时出错: {ex.Message}");
                }
            }
            else
            {
                await Task.Delay(10);
            }
        }
    }

    public void Stop()
    {
        _isProcessing = false;
        _processingCts.Cancel();
    }
}
using System;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.V2.Web
{
    /// <summary>
    /// 基于 Channel 的简单消息队列（线程安全，带有边界容量）
    /// </summary>
    public class MessageQueue<T>
    {
        private readonly Channel<T> _channel;

        public MessageQueue(int capacity = 1024)
        {
            var options = new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            };
            _channel = Channel.CreateBounded<T>(options);
        }

        public ValueTask WriteAsync(T item) => _channel.Writer.WriteAsync(item);

        public ValueTask<T> ReadAsync(System.Threading.CancellationToken cancellationToken = default) => _channel.Reader.ReadAsync(cancellationToken);

        public void Complete() => _channel.Writer.Complete();

        public bool TryRead(out T item) => _channel.Reader.TryRead(out item!);
    }
}

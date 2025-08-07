using System;

namespace Drx.Sdk.Network.Socket.Hosting
{
    /// <summary>
    /// 极简的组件解析/创建器：优先用无参构造；否则使用 Activator.CreateInstance(type, true)。
    /// 不做生命周期管理，交由调用方持有与释放。
    /// </summary>
    public sealed class DefaultSocketComponentResolver : ISocketComponentResolver
    {
        public T ResolveOrNull<T>() where T : class => null;

        public object ResolveOrNull(Type type) => null;

        public T Create<T>() where T : class
        {
            return Activator.CreateInstance(typeof(T), nonPublic: true) as T;
        }

        public object Create(Type type)
        {
            return Activator.CreateInstance(type, nonPublic: true);
        }
    }
}
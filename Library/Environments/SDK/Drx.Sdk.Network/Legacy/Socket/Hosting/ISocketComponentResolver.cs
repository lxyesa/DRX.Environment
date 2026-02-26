using System;

namespace Drx.Sdk.Network.Legacy.Socket.Hosting
{
    /// <summary>
    /// 极简组件解析器接口（独立模式用，不依赖 Microsoft.Extensions.*）
    /// </summary>
    public interface ISocketComponentResolver
    {
        T ResolveOrNull<T>() where T : class;
        object ResolveOrNull(Type type);
        T Create<T>() where T : class;
        object Create(Type type);
    }
}
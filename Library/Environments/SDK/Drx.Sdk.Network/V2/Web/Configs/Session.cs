using System;
using System.Collections.Concurrent;

namespace Drx.Sdk.Network.V2.Web.Configs
{
    /// <summary>
    /// HTTP 标准会话类，完全遵循 HTTP/S 会话管理规范。
    /// 会话数据存储在服务器端，通过 HttpOnly Cookie 传输会话 ID。
    /// </summary>
    public class Session
    {
        /// <summary>
        /// 会话唯一标识（由服务器生成和维护）
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// 会话数据存储容器（线程安全）
        /// </summary>
        public ConcurrentDictionary<string, object> Data { get; }

        /// <summary>
        /// 会话创建时间（UTC）
        /// </summary>
        public DateTime CreatedAt { get; }

        /// <summary>
        /// 会话最后访问时间（UTC），用于计算会话过期
        /// </summary>
        public DateTime LastAccessAt { get; private set; }

        /// <summary>
        /// 初始化会话（仅在创建时调用）
        /// </summary>
        /// <param name="id">会话标识符</param>
        internal Session(string id)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Data = new ConcurrentDictionary<string, object>();
            CreatedAt = DateTime.UtcNow;
            LastAccessAt = CreatedAt;
        }

        /// <summary>
        /// 更新会话的最后访问时间（由会话管理器自动调用）
        /// </summary>
        internal void UpdateLastAccess()
        {
            LastAccessAt = DateTime.UtcNow;
        }
    }
}

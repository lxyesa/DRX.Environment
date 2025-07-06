using System;

namespace Drx.Sdk.Network.Session
{
    /// <summary>
    /// 自定义会话接口
    /// </summary>
    public interface ISession
    {
        /// <summary>
        /// 会话唯一标识符
        /// </summary>
        string ID { get; set; }
        
        /// <summary>
        /// 会话名称
        /// </summary>
        string Name { get; set; }
        
        /// <summary>
        /// 会话过期时间
        /// </summary>
        DateTime ExpireTime { get; set; }
    }
} 
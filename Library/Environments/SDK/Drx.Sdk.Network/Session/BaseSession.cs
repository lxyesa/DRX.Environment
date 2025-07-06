using System;

namespace Drx.Sdk.Network.Session
{
    /// <summary>
    /// 会话基类，实现ISession接口并自动生成ID
    /// </summary>
    public abstract class BaseSession : ISession
    {
        /// <summary>
        /// 会话唯一标识符
        /// </summary>
        public string ID { get; set; }
        
        /// <summary>
        /// 会话名称
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// 会话过期时间
        /// </summary>
        public DateTime ExpireTime { get; set; }
        
        /// <summary>
        /// 构造函数，自动生成ID
        /// </summary>
        /// <param name="name">会话名称</param>
        /// <param name="expireSeconds">过期时间（秒）</param>
        protected BaseSession(string name, int expireSeconds = 3600)
        {
            ID = GenerateSessionId();
            Name = name;
            ExpireTime = DateTime.Now.AddSeconds(expireSeconds);
        }
        
        /// <summary>
        /// 生成唯一的会话ID
        /// </summary>
        /// <returns>会话ID</returns>
        private string GenerateSessionId()
        {
            return Guid.NewGuid().ToString("N");
        }
        
        /// <summary>
        /// 检查会话是否已过期
        /// </summary>
        /// <returns>是否已过期</returns>
        public bool IsExpired()
        {
            return DateTime.Now > ExpireTime;
        }
        
        /// <summary>
        /// 延长会话过期时间
        /// </summary>
        /// <param name="seconds">延长的秒数</param>
        public void ExtendExpireTime(int seconds)
        {
            ExpireTime = ExpireTime.AddSeconds(seconds);
        }
    }
} 
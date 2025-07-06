using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace Drx.Sdk.Network.Session
{
    /// <summary>
    /// 会话管理器
    /// </summary>
    public class SessionManager
    {
        private readonly ConcurrentDictionary<string, ISession> _sessions;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly string _cookiePrefix;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="httpContextAccessor">HTTP上下文访问器</param>
        /// <param name="cookiePrefix">Cookie前缀</param>
        public SessionManager(IHttpContextAccessor httpContextAccessor, string cookiePrefix = "KAX_SESSION_")
        {
            _sessions = new ConcurrentDictionary<string, ISession>();
            _httpContextAccessor = httpContextAccessor;
            _cookiePrefix = cookiePrefix;
        }
        
        /// <summary>
        /// 创建新会话
        /// </summary>
        /// <typeparam name="T">会话类型，必须实现ISession接口</typeparam>
        /// <param name="session">会话实例</param>
        /// <param name="setCookie">是否设置Cookie</param>
        /// <returns>会话ID</returns>
        public string CreateSession<T>(T session, bool setCookie = true) where T : ISession
        {
            if (_sessions.TryAdd(session.ID, session))
            {
                if (setCookie && _httpContextAccessor.HttpContext != null)
                {
                    // 设置Cookie
                    _httpContextAccessor.HttpContext.Response.Cookies.Append(
                        _cookiePrefix + session.Name,
                        session.ID,
                        new CookieOptions
                        {
                            Expires = session.ExpireTime,
                            HttpOnly = true,
                            Secure = _httpContextAccessor.HttpContext.Request.IsHttps,
                            SameSite = SameSiteMode.Lax
                        });
                }
                return session.ID;
            }
            
            throw new Exception("创建会话失败");
        }

        /// <summary>
        /// 获取会话
        /// </summary>
        /// <typeparam name="T">会话类型</typeparam>
        /// <param name="sessionId">会话ID</param>
        /// <param name="autoRemoveExpired">是否自动移除过期会话</param>
        /// <returns>会话实例，如果不存在或已过期则返回null</returns>
        public T GetSession<T>(string sessionId, bool autoRemoveExpired = true) where T : ISession
        {
            if (_sessions.TryGetValue(sessionId, out ISession session))
            {
                if (session is T typedSession)
                {
                    // 检查是否过期
                    if (DateTime.Now > session.ExpireTime)
                    {
                        if (autoRemoveExpired)
                        {
                            RemoveSession(sessionId);
                        }
                        return default;
                    }
                    return typedSession;
                }
            }
            return default;
        }
        
        /// <summary>
        /// 从Cookie中获取会话
        /// </summary>
        /// <typeparam name="T">会话类型</typeparam>
        /// <param name="sessionName">会话名称</param>
        /// <param name="autoRemoveExpired">是否自动移除过期会话</param>
        /// <returns>会话实例，如果不存在或已过期则返回null</returns>
        public T GetSessionFromCookie<T>(string sessionName, bool autoRemoveExpired = true) where T : ISession
        {
            if (_httpContextAccessor.HttpContext != null)
            {
                if (_httpContextAccessor.HttpContext.Request.Cookies.TryGetValue(_cookiePrefix + sessionName, out string sessionId))
                {
                    return GetSession<T>(sessionId, autoRemoveExpired);
                }
            }
            return default;
        }
        
        /// <summary>
        /// 更新会话
        /// </summary>
        /// <typeparam name="T">会话类型</typeparam>
        /// <param name="session">会话实例</param>
        /// <param name="updateCookie">是否更新Cookie</param>
        /// <returns>是否更新成功</returns>
        public bool UpdateSession<T>(T session, bool updateCookie = true) where T : ISession
        {
            if (_sessions.TryUpdate(session.ID, session, _sessions[session.ID]))
            {
                if (updateCookie && _httpContextAccessor.HttpContext != null)
                {
                    // 更新Cookie
                    _httpContextAccessor.HttpContext.Response.Cookies.Append(
                        _cookiePrefix + session.Name,
                        session.ID,
                        new CookieOptions
                        {
                            Expires = session.ExpireTime,
                            HttpOnly = true,
                            Secure = _httpContextAccessor.HttpContext.Request.IsHttps,
                            SameSite = SameSiteMode.Lax
                        });
                }
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// 移除会话
        /// </summary>
        /// <param name="sessionId">会话ID</param>
        /// <param name="removeCookie">是否移除Cookie</param>
        /// <returns>是否移除成功</returns>
        public bool RemoveSession(string sessionId, bool removeCookie = true)
        {
            if (_sessions.TryRemove(sessionId, out ISession session) && removeCookie && _httpContextAccessor.HttpContext != null)
            {
                // 移除Cookie
                _httpContextAccessor.HttpContext.Response.Cookies.Delete(_cookiePrefix + session.Name);
                return true;
            }
            return false;
        }
        
        /// <summary>
        /// 清理过期会话
        /// </summary>
        /// <returns>清理的会话数量</returns>
        public int CleanupExpiredSessions()
        {
            int count = 0;
            var expiredSessions = _sessions.Where(s => DateTime.Now > s.Value.ExpireTime).ToList();
            
            foreach (var session in expiredSessions)
            {
                if (RemoveSession(session.Key))
                {
                    count++;
                }
            }
            
            return count;
        }
        
        /// <summary>
        /// 获取所有会话
        /// </summary>
        /// <returns>会话列表</returns>
        public IEnumerable<ISession> GetAllSessions()
        {
            return _sessions.Values;
        }
        
        /// <summary>
        /// 获取会话数量
        /// </summary>
        /// <returns>会话数量</returns>
        public int GetSessionCount()
        {
            return _sessions.Count;
        }
    }
} 
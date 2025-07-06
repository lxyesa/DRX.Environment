using System;
using Microsoft.Extensions.DependencyInjection;

namespace Drx.Sdk.Network.Session
{
    /// <summary>
    /// 会话服务扩展方法
    /// </summary>
    public static class SessionServiceExtensions
    {
        /// <summary>
        /// 添加自定义会话服务
        /// </summary>
        /// <typeparam name="T">会话类型，必须实现ISession接口</typeparam>
        /// <param name="services">服务集合</param>
        /// <param name="cookiePrefix">Cookie前缀</param>
        /// <returns>服务集合</returns>
        public static IServiceCollection AddCustomSession<T>(this IServiceCollection services, string cookiePrefix = "KAX_SESSION_") where T : ISession
        {
            // 确保已注册HttpContextAccessor
            services.AddHttpContextAccessor();
            
            // 注册会话管理器为单例服务
            services.AddSingleton<SessionManager>(provider => 
            {
                var httpContextAccessor = provider.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
                return new SessionManager(httpContextAccessor, cookiePrefix);
            });
            
            // 注册会话清理后台服务
            services.AddHostedService<SessionCleanupService>();
            
            return services;
        }
    }
    
    /// <summary>
    /// 会话清理后台服务
    /// </summary>
    public class SessionCleanupService : Microsoft.Extensions.Hosting.BackgroundService
    {
        private readonly SessionManager _sessionManager;
        private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="sessionManager">会话管理器</param>
        public SessionCleanupService(SessionManager sessionManager)
        {
            _sessionManager = sessionManager;
        }
        
        /// <summary>
        /// 执行后台任务
        /// </summary>
        /// <param name="stoppingToken">取消令牌</param>
        protected override async System.Threading.Tasks.Task ExecuteAsync(System.Threading.CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                // 清理过期会话
                int cleanedCount = _sessionManager.CleanupExpiredSessions();
                
                // 记录日志（如果需要）
                if (cleanedCount > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"已清理 {cleanedCount} 个过期会话");
                }
                
                // 等待下一次执行
                await System.Threading.Tasks.Task.Delay(_interval, stoppingToken);
            }
        }
    }
} 
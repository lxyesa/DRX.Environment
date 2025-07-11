using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.DataProtection;
using System.IO;

namespace Drx.Sdk.Network.Extensions;

public static class Global
{
    public class SessionOptions
    {
        public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromDays(7);
        public bool CookieHttpOnly { get; set; } = true;
        public bool CookieIsEssential { get; set; } = true;
        public TimeSpan CookieMaxAge { get; set; } = TimeSpan.FromDays(7);
        public string KeysDirectory { get; set; } = "Data/Keys";
        public string ApplicationName { get; set; } = "DRXApplication";
        public TimeSpan KeyLifetime { get; set; } = TimeSpan.FromDays(90);
        public SameSiteMode CookieSameSiteMode { get; set; } = SameSiteMode.Lax;
        public bool RequireCookieConsent { get; set; } = false;
    }

    /// <summary>
    /// 配置DRX会话系统，包括Session和Cookie策略
    /// </summary>
    public static IServiceCollection AddDRXSession(
        this IServiceCollection services,
        Action<SessionOptions> configureOptions = null)
    {
        var options = new SessionOptions();
        configureOptions?.Invoke(options);

        // 配置数据保护
        var keysDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, options.KeysDirectory);
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keysDirectory))
            .SetApplicationName(options.ApplicationName)
            .SetDefaultKeyLifetime(options.KeyLifetime);

        // 配置分布式缓存
        services.AddDistributedMemoryCache();

        // 配置Session
        services.AddSession(sessionOptions =>
        {
            sessionOptions.IdleTimeout = options.IdleTimeout;
            sessionOptions.Cookie.HttpOnly = options.CookieHttpOnly;
            sessionOptions.Cookie.IsEssential = options.CookieIsEssential;
            sessionOptions.Cookie.MaxAge = options.CookieMaxAge;
        });

        // 配置Cookie策略
        services.Configure<CookiePolicyOptions>(cookieOptions =>
        {
            cookieOptions.CheckConsentNeeded = context => options.RequireCookieConsent;
            cookieOptions.MinimumSameSitePolicy = options.CookieSameSiteMode;
        });

        return services;
    }

    /// <summary>
    /// 使用DRX会话中间件
    /// </summary>
    public static IApplicationBuilder UseDRXSession(this IApplicationBuilder app)
    {
        app.UseCookiePolicy();
        app.UseSession();
        return app;
    }
}

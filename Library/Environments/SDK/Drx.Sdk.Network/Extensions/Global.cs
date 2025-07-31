using System.Text;
using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.DataProtection;
using System.IO;
using Microsoft.JSInterop;
using System.Text.Json;

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
        Action<SessionOptions>? configureOptions = null)
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
    /// 通过JS脚本获取指定HTML元素的value，适用于Blazor/JSInterop场景。
    /// 推荐：在ASP.NET标准Web环境下，建议通过表单提交或AJAX与后端交互获取元素value，
    /// 并可在Controller中通过参数直接接收，无需前端JS获取。
    /// </summary>
    /// <param name="jsRuntime">Blazor IJSRuntime实例</param>
    /// <param name="selector">元素ID（如#id）或CSS选择器</param>
    /// <returns>元素的value字符串</returns>
    public static async Task<string?> GetHtmlElementValueAsync(Microsoft.JSInterop.IJSRuntime jsRuntime, string selector)
    {
        if (jsRuntime == null) throw new ArgumentNullException(nameof(jsRuntime));
        // 注入JS脚本（仅需一次，可优化为静态注入）
        const string jsFunc = @"
                window.drxGetElementValue = function(selector) {
                    var el = document.querySelector(selector);
                    if (!el) return null;
                    return el.value !== undefined ? el.value : el.textContent;
                };
            ";
        await jsRuntime.InvokeVoidAsync("eval", jsFunc);
        return await jsRuntime.InvokeAsync<string>("drxGetElementValue", selector);
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

    /// <summary>
    /// 启动后台线程监听控制台输入，返回 ConsoleCommandProcessor 实例
    /// </summary>
    public static ConsoleCommandProcessor UseConsoleCommandProcessor(this IApplicationBuilder app)
    {
        var processor = new ConsoleCommandProcessor();
        processor.Start();
        return processor;
    }

    /// <summary>
    /// 从 JSON 字符串数组中提取指定字段的值。
    /// </summary>
    /// <param name="args">包含 JSON 字符串的数组。</param>
    /// <param name="field">要提取的 JSON 字段名。</param>
    /// <param name="index">数组中的索引，默认为 0。</param>
    /// <returns>返回字段的字符串值，如果字段不存在或数组为空，则返回 null。</returns>
    public static string? GetJsonField(this string[] args, string field, int index = 0)
    {
        if (args == null || args.Length <= index) return null;
        using var doc = JsonDocument.Parse(args[index]);
        if (doc.RootElement.TryGetProperty(field, out var prop))
            return prop.GetString();
        return null;
    }

    public static T GetJsonProperty<T>(this string jsonStr, string propertyName)
    {
        if (string.IsNullOrEmpty(jsonStr)) return default;

        try
        {
            using var doc = JsonDocument.Parse(jsonStr);
            if (doc.RootElement.TryGetProperty(propertyName, out var property))
            {
                return property.Deserialize<T>() ?? default;
            }
        }
        catch (JsonException)
        {
            // 解析失败，返回默认值
        }

        return default;
    }
}


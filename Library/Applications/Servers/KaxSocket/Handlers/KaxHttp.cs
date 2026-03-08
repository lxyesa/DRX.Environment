using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Drx.Sdk.Network.Http;
using Drx.Sdk.Network.Http.Auth;
using Drx.Sdk.Network.Http.Configs;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.Results;
using KaxSocket;
using KaxSocket.Cache;
using Drx.Sdk.Shared;
using Drx.Sdk.Network.Http.Api;

namespace KaxSocket.Handlers;

/// <summary>
/// KaxHttp - HTTP 请求处理器
/// 通过 Partial Classes 分离不同功能模块：
///
///   认证与用户
///   - Authentication:    用户认证（登录、注册、令牌刷新）
///   - UserProfile:       用户资料与头像管理
///   - UserAssets:        用户已激活资产查询
///
///   资产与商品
///   - AssetManagement:   后台资产 CRUD（管理员）
///   - AssetQueries:      公开资产查询 + 套餐列表
///   - CdkManagement:     CDK 生成与批量查询（管理员）
///   - AssetVerification: CDK 兑换与资产验证
///
///   购物与订阅
///   - Shopping:          金币驱动的购买 / 更变套餐 / 取消订阅
///   - OrderManagement:   订单记录查询（用户视图 + 管理员视图）
/// </summary>
public partial class KaxHttp
{
    private static readonly AvatarCacheManager _avatarCache = new(maxCacheSize: 100, cacheExpirationSeconds: 3600);

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(KaxHttp))]
    public KaxHttp()
    {
    }

    static KaxHttp()
    {
        var jwtSecretKey = Program.Config.JwtSecretKey;
        var jwtIssuer = Program.Config.JwtIssuer;
        var jwtAudience = Program.Config.JwtAudience;
        var jwtExpirationDays = Program.Config.JwtExpirationDays;

        if (string.IsNullOrWhiteSpace(jwtSecretKey))
        {
            jwtSecretKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
            Logger.Warn("[Config] JWT 密钥为空，已使用进程内随机密钥；重启后旧 JWT 将失效。请检查 Program.Config.JwtSecretKey。");
        }

        JwtHelper.Configure(new JwtHelper.JwtConfig
        {
            SecretKey = jwtSecretKey,
            Issuer = jwtIssuer,
            Audience = jwtAudience,
            Expiration = TimeSpan.FromDays(jwtExpirationDays)
        });

        ApiGuard.Configure((token) =>
        {
            return JwtHelper.ValidateToken(token);
        });
    }

    #region Rate Limit Callback

    public static HttpResponse RateLimitCallback(int count, HttpRequest request, OverrideContext overrideContext)
    {
        if (count > 20)
        {
            var userToken = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
            var userName = JwtHelper.ValidateToken(userToken!)?.Identity?.Name ?? "未知用户";
            _ = KaxGlobal.BanUser(userName, "短时间内请求过于频繁，自动封禁。", 60); // 封禁 1 分钟
            return new HttpResponse(429, "请求过于频繁，您的账号暂时被封禁。");
        }
        else
        {
            Logger.Warn($"请求过于频繁: {request.Method} {request.Path} from {request.ClientAddress.Ip}:{request.ClientAddress.Port}");
            return new HttpResponse(429, "请求过于频繁，请稍后再试。");
        }
    }

    #endregion

    #region HTTP Handlers

    [HttpMiddleware]
    public static async Task<HttpResponse?> Echo(HttpRequest request, Func<HttpRequest, Task<HttpResponse?>> next)
    {
        _ = Logger.InfoAsync($"收到 HTTP 请求: {request.Method} {request.Path} from {request.ClientAddress.Ip}:{request.ClientAddress.Port}");
        // 继续处理请求
        return await next(request).ConfigureAwait(false);
    }

    /// <summary>
    /// 根据单位和数字计算持续时间（毫秒）
    /// </summary>
    private static long CalculateDurationInMilliseconds(string unit, int duration)
    {
        return unit.ToLower() switch
        {
            "year" => duration * 365L * 24 * 60 * 60 * 1000,
            "month" => duration * 30L * 24 * 60 * 60 * 1000,
            "day" => duration * 24L * 60 * 60 * 1000,
            "hour" => duration * 60L * 60 * 1000,
            "minute" => duration * 60L * 1000,
            _ => 0L
        };
    }

    /// <summary>
    /// 生成指定长度的随机字符串
    /// </summary>
    private static string RandomString(int length, string? charset = null)
    {
        if (string.IsNullOrEmpty(charset)) charset = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var chars = charset.ToCharArray();
        var sb = new StringBuilder(length);
        using var rng = RandomNumberGenerator.Create();
        var buf = new byte[4];
        for (int i = 0; i < length; i++)
        {
            rng.GetBytes(buf);
            uint v = BitConverter.ToUInt32(buf, 0);
            sb.Append(chars[(int)(v % (uint)chars.Length)]);
        }
        return sb.ToString();
    }

    private sealed class SmtpRuntimeConfig
    {
        public string SenderAddress { get; init; } = string.Empty;
        public string AuthCode { get; init; } = string.Empty;
        public string Host { get; init; } = "smtp.qq.com";
        public int Port { get; init; } = 587;
        public bool EnableSsl { get; init; } = true;
    }

    private static SmtpRuntimeConfig? ResolveSmtpRuntimeConfig(string scenario)
    {
        var senderAddress = Program.Config.SmtpEmail?.Trim() ?? string.Empty;
        var authCode = Program.Config.SmtpAuthCode?.Trim() ?? string.Empty;
        var host = Program.Config.SmtpHost ?? "smtp.qq.com";
        var port = Program.Config.SmtpPort;
        var enableSsl = Program.Config.SmtpEnableSsl;

        if (string.IsNullOrWhiteSpace(senderAddress) || string.IsNullOrWhiteSpace(authCode))
        {
            Logger.Warn($"[Config][{scenario}] SMTP 配置缺失：请在 Program.Config 中设置 SmtpEmail 与 SmtpAuthCode。");
            return null;
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            host = "smtp.qq.com";
            Logger.Warn($"[Config][{scenario}] SmtpHost 为空，已回退到 smtp.qq.com。");
        }

        if (port <= 0)
        {
            port = 587;
            Logger.Warn($"[Config][{scenario}] SmtpPort 配置无效: {Program.Config.SmtpPort}，已回退到 587。");
        }

        return new SmtpRuntimeConfig
        {
            SenderAddress = senderAddress,
            AuthCode = authCode,
            Host = host,
            Port = port,
            EnableSsl = enableSsl
        };
    }

    #endregion
}

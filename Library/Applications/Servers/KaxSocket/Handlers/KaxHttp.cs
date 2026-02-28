using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Drx.Sdk.Network.Http;
using Drx.Sdk.Network.Http.Auth;
using Drx.Sdk.Network.Http.Configs;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.Results;
using KaxSocket;
using KaxSocket.Cache;
using Drx.Sdk.Shared;

namespace KaxSocket.Handlers;

/// <summary>
/// KaxHttp - HTTP 请求处理器
/// 通过 Partial Classes 分离不同功能模块：
/// - Authentication: 用户认证
/// - UserProfile: 用户资料管理
/// - UserAssets: 用户资产管理
/// - CdkManagement: CDK 管理
/// - AssetManagement: 资源管理
/// - AssetQueries: 公开资源查询
/// - Shopping: 购物与套餐管理
/// - AssetVerification: 资源验证
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
        // 配置 JWT
        JwtHelper.Configure(new JwtHelper.JwtConfig
        {
            SecretKey = "A1b2C3d4E5f6G7h8I9j0K1l2M3n4O5p6", // 建议使用环境变量
            Issuer = "KaxSocket",
            Audience = "KaxUsers",
            Expiration = TimeSpan.FromHours(1)
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

    // 检查当前用户是否属于允许使用 CDK 管理 API 的权限组（Console/Root/Admin）
    private static async Task<bool> IsCdkAdminUser(string? userName)
    {
        if (string.IsNullOrWhiteSpace(userName)) return false;
        var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (user == null) return false;
        var g = user.PermissionGroup;
        return g == UserPermissionGroup.System || g == UserPermissionGroup.Console || g == UserPermissionGroup.Admin;
    }

    // 检查当前用户是否属于允许使用 Asset 管理 API 的权限组（Console/Root/Admin）
    private static async Task<bool> IsAssetAdminUser(string? userName)
    {
        if (string.IsNullOrWhiteSpace(userName)) return false;
        var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (user == null) return false;
        var g = user.PermissionGroup;
        return g == UserPermissionGroup.System || g == UserPermissionGroup.Console || g == UserPermissionGroup.Admin;
    }

    [HttpMiddleware]
    public static HttpResponse Echo(HttpRequest request, Func<HttpRequest, HttpResponse> next)
    {
        Logger.Info($"收到 HTTP 请求: {request.Method} {request.Path} from {request.ClientAddress.Ip}:{request.ClientAddress.Port}");
        // 继续处理请求
        return next(request);
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

    #endregion
}

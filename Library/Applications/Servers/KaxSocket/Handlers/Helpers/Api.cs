using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Drx.Sdk.Network.Http;
using Drx.Sdk.Network.Http.Api;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Shared;
using Drx.Sdk.Shared.Utility;

namespace KaxSocket.Handlers.Helpers
{
    /// <summary>
    /// 文件职责：提供 KaxSocket Handler 可复用的认证、权限与统一 API Envelope 辅助能力。
    /// 关键依赖：Drx.Sdk.Network.Http.Protocol（IActionResult/JsonResult）、KaxGlobal 用户仓储与 JWT 认证上下文。
    /// </summary>

    /// <summary>
    /// 统一 API 响应模型。
    /// </summary>
    public sealed class ApiEnvelope
    {
        /// <summary>
        /// 业务状态码，0 表示成功，其它值表示失败或业务异常。
        /// </summary>
        public int Code { get; init; }

        /// <summary>
        /// 返回消息，建议提供用户可读文本。
        /// </summary>
        public string Message { get; init; } = "ok";

        /// <summary>
        /// 响应数据载荷，失败时可为空。
        /// </summary>
        public object? Data { get; init; }

        /// <summary>
        /// 请求追踪标识，用于定位服务端日志。
        /// </summary>
        public string TraceId { get; init; } = string.Empty;
    }

    /// <summary>
    /// 分页响应数据模型。
    /// </summary>
    /// <typeparam name="T">分页项类型。</typeparam>
    public sealed class ApiPagedData<T>
    {
        /// <summary>
        /// 当前页结果。
        /// </summary>
        public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

        /// <summary>
        /// 当前页码（从 1 开始）。
        /// </summary>
        public int Page { get; init; }

        /// <summary>
        /// 每页大小。
        /// </summary>
        public int PageSize { get; init; }

        /// <summary>
        /// 总记录数。
        /// </summary>
        public int Total { get; init; }
    }

    /// <summary>
    /// 统一业务错误码常量。
    /// 用于后端响应与前端提示映射，避免各模块散落硬编码数值。
    /// </summary>
    public static class ApiErrorCodes
    {
        /// <summary>请求成功。</summary>
        public const int Success = 0;

        /// <summary>通用请求错误。</summary>
        public const int BadRequest = 4000;

        /// <summary>参数无效或校验失败。</summary>
        public const int InvalidArgument = 4001;

        /// <summary>未授权访问。</summary>
        public const int Unauthorized = 4010;

        /// <summary>令牌缺失。</summary>
        public const int TokenMissing = 4011;

        /// <summary>令牌无效或已过期。</summary>
        public const int TokenInvalid = 4012;

        /// <summary>权限不足。</summary>
        public const int Forbidden = 4030;

        /// <summary>用户被封禁。</summary>
        public const int AccountBanned = 4031;

        /// <summary>目标资源不存在。</summary>
        public const int NotFound = 4040;

        /// <summary>资源冲突（如唯一键冲突）。</summary>
        public const int Conflict = 4090;

        /// <summary>请求频率过高。</summary>
        public const int TooManyRequests = 4290;

        /// <summary>服务端内部异常。</summary>
        public const int InternalServerError = 5000;

        /// <summary>CDK 不存在。</summary>
        public const int CdkNotFound = 2001;

        /// <summary>CDK 已被使用。</summary>
        public const int CdkUsed = 2002;

        /// <summary>CDK 已过期。</summary>
        public const int CdkExpired = 2003;

        /// <summary>用户未拥有资源。</summary>
        public const int AssetNotOwned = 2004;

        /// <summary>邮箱流程参数无效。</summary>
        public const int EmailInvalidArgument = 46001;

        /// <summary>邮箱已被占用。</summary>
        public const int EmailAlreadyUsed = 46002;

        /// <summary>验证码发送过快。</summary>
        public const int EmailSendTooFast = 46003;

        /// <summary>每小时发送次数超限。</summary>
        public const int EmailHourlyLimit = 46004;

        /// <summary>每日发送次数超限。</summary>
        public const int EmailDailyLimit = 46005;

        /// <summary>验证码错误。</summary>
        public const int EmailCodeInvalid = 46006;

        /// <summary>验证码已过期。</summary>
        public const int EmailCodeExpired = 46007;

        /// <summary>验证码已使用。</summary>
        public const int EmailCodeUsed = 46008;

        /// <summary>验证码尝试次数过多。</summary>
        public const int EmailCodeTooManyAttempts = 46009;

        /// <summary>未先发起验证码流程。</summary>
        public const int EmailFlowNotStarted = 46010;
    }

    /// <summary>
    /// 语义化接口访问策略。
    /// </summary>
    public enum ApiAccessPolicy
    {
        /// <summary>
        /// 普通已登录用户。
        /// </summary>
        Authenticated = 0,

        /// <summary>
        /// 管理员（System / Console / Admin）。
        /// </summary>
        Admin = 1,

        /// <summary>
        /// 系统管理员（仅 System）。
        /// </summary>
        System = 2
    }

    /// <summary>
    /// KaxSocket 业务层 API 快捷封装。
    /// 将 JWT 认证、封禁检查、用户查询、管理员验证等重复逻辑整合为单行调用。
    /// <para>
    /// 使用示例：
    /// <code>
    /// // 认证 + 封禁检查 + 获取用户对象（原来需要 8 行，现在只需 2 行）：
    /// var (user, error) = await Api.GetUserAsync(request);
    /// if (error != null) return error;
    /// 
    /// // 管理员接口（认证 + 封禁 + 权限检查）：
    /// var (user, error) = await Api.RequireAdminAsync(request);
    /// if (error != null) return error;
    /// 
    /// // 仅认证（不查数据库）：
    /// var (userName, error) = Api.Authenticate(request);
    /// if (error != null) return error;
    /// </code>
    /// </para>
    /// </summary>
    public static class Api
    {
        private const string VerificationCodeCharset = "0123456789"; // 纯数字验证码，避免大小写混淆
        private const string TokenUseClaim = "token_use";
        private const string TraceIdHeader = "x-trace-id";
        private const string RequestIdHeader = "x-request-id";

        #region 统一响应协议（Envelope）

        /// <summary>
        /// 构建统一成功响应。
        /// </summary>
        /// <param name="request">当前请求，用于提取或生成 traceId。</param>
        /// <param name="data">响应数据；可为空。</param>
        /// <param name="message">响应消息；为空时使用默认值。</param>
        /// <param name="code">业务状态码，成功建议为 0。</param>
        /// <param name="httpStatus">HTTP 状态码，默认 200。</param>
        /// <returns>包含 code/message/data/traceId 的统一响应。</returns>
        public static IActionResult EnvelopeOk(HttpRequest request, object? data = null, string? message = null, int code = 0, int httpStatus = 200)
        {
            var envelope = new ApiEnvelope
            {
                Code = code,
                Message = string.IsNullOrWhiteSpace(message) ? "ok" : message,
                Data = data,
                TraceId = ResolveTraceId(request)
            };
            return new JsonResult(new
            {
                code = envelope.Code,
                message = envelope.Message,
                data = envelope.Data,
                traceId = envelope.TraceId
            }, httpStatus);
        }

        /// <summary>
        /// 构建统一失败响应。
        /// </summary>
        /// <param name="request">当前请求，用于提取或生成 traceId。</param>
        /// <param name="httpStatus">HTTP 状态码。</param>
        /// <param name="code">业务错误码。</param>
        /// <param name="message">错误消息；为空时使用默认文案。</param>
        /// <param name="data">可选错误上下文数据。</param>
        /// <returns>包含 code/message/data/traceId 的统一错误响应。</returns>
        public static IActionResult EnvelopeFail(HttpRequest request, int httpStatus, int code, string? message, object? data = null)
        {
            var envelope = new ApiEnvelope
            {
                Code = code,
                Message = string.IsNullOrWhiteSpace(message) ? "error" : message,
                Data = data,
                TraceId = ResolveTraceId(request)
            };
            return new JsonResult(new
            {
                code = envelope.Code,
                message = envelope.Message,
                data = envelope.Data,
                traceId = envelope.TraceId
            }, httpStatus);
        }

        /// <summary>
        /// 构建统一分页成功响应，分页对象固定落在 data 下。
        /// </summary>
        /// <typeparam name="T">分页项类型。</typeparam>
        /// <param name="request">当前请求，用于提取或生成 traceId。</param>
        /// <param name="items">分页数据项集合；为空时返回空集合。</param>
        /// <param name="page">页码（小于 1 时按 1 处理）。</param>
        /// <param name="pageSize">每页大小（小于 1 时按 1 处理）。</param>
        /// <param name="total">总条数（小于 0 时按 0 处理）。</param>
        /// <param name="message">成功消息；为空时使用默认值。</param>
        /// <returns>统一分页 Envelope 响应。</returns>
        public static IActionResult EnvelopePaged<T>(HttpRequest request, IEnumerable<T>? items, int page, int pageSize, int total, string? message = null)
        {
            var payload = new ApiPagedData<T>
            {
                Items = (items ?? Enumerable.Empty<T>()).ToList(),
                Page = Math.Max(1, page),
                PageSize = Math.Max(1, pageSize),
                Total = Math.Max(0, total)
            };

            return EnvelopeOk(request, payload, message ?? "ok");
        }

        #endregion

        #region 认证（不涉及数据库查询）

        /// <summary>
        /// 从请求中认证用户，提取用户名。
        /// 验证步骤：Bearer Token → JWT 验证 → 提取用户名
        /// </summary>
        /// <param name="request">HTTP 请求</param>
        /// <returns>(userName, error) — 成功时 error 为 null</returns>
        public static (string? userName, IActionResult? error) Authenticate(HttpRequest request)
        {
            if (!ApiGuard.TryGetUserName(request, out var userName, out var error))
                return (null, error);
            return (userName, null);
        }

        #endregion

        #region 安全工具（验证码与邮箱）

        /// <summary>
        /// 生成指定长度的验证码（默认 8 位，字母数字，排除易混淆字符）。
        /// </summary>
        public static string GenerateVerificationCode(int length = 8)
        {
            if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));

            var buffer = new byte[length];
            RandomNumberGenerator.Fill(buffer);
            var chars = new char[length];

            for (var i = 0; i < length; i++)
            {
                chars[i] = VerificationCodeCharset[buffer[i] % VerificationCodeCharset.Length];
            }

            return new string(chars);
        }

        /// <summary>
        /// 生成随机盐（Base64 URL 友好）。
        /// </summary>
        public static string GenerateVerificationSalt(int byteLength = 16)
        {
            if (byteLength <= 0) throw new ArgumentOutOfRangeException(nameof(byteLength));
            var saltBytes = new byte[byteLength];
            RandomNumberGenerator.Fill(saltBytes);
            return Convert.ToBase64String(saltBytes);
        }

        /// <summary>
        /// 计算验证码哈希（SHA-256，包含盐），返回小写十六进制串。
        /// </summary>
        public static string HashVerificationCode(string code, string salt)
        {
            var input = $"{code ?? string.Empty}:{salt ?? string.Empty}";
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        /// <summary>
        /// 校验验证码明文是否与目标哈希一致（固定时序比较）。
        /// </summary>
        public static bool VerifyVerificationCode(string inputCode, string salt, string expectedHash)
        {
            var actual = HashVerificationCode(inputCode, salt);
            var actualBytes = Encoding.UTF8.GetBytes(actual);
            var expectedBytes = Encoding.UTF8.GetBytes(expectedHash ?? string.Empty);
            return CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
        }

        /// <summary>
        /// 邮箱脱敏：保留首字符与域名，其余替换为 *。
        /// </summary>
        public static string MaskEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return string.Empty;
            var at = email.IndexOf('@');
            if (at <= 0 || at == email.Length - 1) return "***";

            var local = email[..at];
            var domain = email[(at + 1)..];
            var head = local[0];
            var maskedLocal = local.Length <= 1 ? "*" : head + new string('*', Math.Max(2, local.Length - 1));
            return $"{maskedLocal}@{domain}";
        }

        /// <summary>
        /// 统一邮箱格式校验入口。
        /// </summary>
        public static bool IsValidEmailFormat(string? email)
        {
            return !string.IsNullOrWhiteSpace(email) && CommonUtility.IsValidEmail(email);
        }

        #endregion

        #region 认证 + 封禁检查

        /// <summary>
        /// 认证用户并检查封禁状态（不查询完整用户对象）。
        /// 验证步骤：Bearer Token → JWT 验证 → 提取用户名 → 封禁检查
        /// </summary>
        /// <param name="request">HTTP 请求</param>
        /// <returns>(userName, error) — 成功时 error 为 null</returns>
        public static async Task<(string? userName, IActionResult? error)> AuthenticateAndCheckBanAsync(HttpRequest request)
        {
            if (!ApiGuard.TryGetUserName(request, out var userName, out var error))
                return (null, error);

            if (await KaxGlobal.IsUserBanned(userName))
                return (null, ApiResult.Banned());

            return (userName, null);
        }

        #endregion

        #region 认证 + 封禁 + 获取用户对象

        /// <summary>
        /// 认证用户、检查封禁状态、查询完整用户对象。
        /// 这是最常用的方法，替代 Handler 中重复的 8 行样板代码。
        /// <para>
        /// 验证步骤：Bearer Token → JWT 验证 → 提取用户名 → 封禁检查 → 查询 UserData
        /// </para>
        /// </summary>
        /// <param name="request">HTTP 请求</param>
        /// <returns>(user, error) — 成功时 error 为 null，user 保证非 null</returns>
        public static async Task<(UserData? user, IActionResult? error)> GetUserAsync(HttpRequest request)
        {
            if (!ApiGuard.TryAuthenticate(request, out var principal, out var error))
                return (null, error);

            var rawToken = ApiGuard.ExtractBearerToken(request) ?? string.Empty;

            var userName = principal.Identity?.Name;
            if (string.IsNullOrWhiteSpace(userName))
                return (null, ApiResult.Unauthorized("令牌缺少用户信息"));

            if (await KaxGlobal.IsUserBanned(userName))
                return (null, ApiResult.Banned());

            var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
            if (user == null)
                return (null, ApiResult.NotFound("用户不存在"));

            var tokenUse = principal.FindFirst(TokenUseClaim)?.Value;
            var isBrowser = IsBrowserRequest(request);
            if (string.Equals(tokenUse, "client", StringComparison.OrdinalIgnoreCase))
            {
                if (isBrowser)
                    return (null, ApiResult.Unauthorized("当前令牌仅允许客户端使用"));

                var hid = (request.Headers["hid"] ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(hid))
                    return (null, ApiResult.Unauthorized("客户端请求缺少 hid"));

                if (!string.Equals(user.ClientHid ?? string.Empty, hid, StringComparison.Ordinal))
                    return (null, ApiResult.Unauthorized("客户端 hid 与登录绑定不一致"));

                var tokenHid = principal.FindFirst("hid")?.Value ?? string.Empty;
                if (!string.Equals(tokenHid, hid, StringComparison.Ordinal))
                    return (null, ApiResult.Unauthorized("令牌 hid 校验失败"));

                var tokenSeed = principal.FindFirst("client_seed")?.Value ?? string.Empty;
                var expectedSeed = ComputeClientSeed(user.UserName ?? string.Empty, hid);
                if (!string.Equals(tokenSeed, expectedSeed, StringComparison.Ordinal))
                    return (null, ApiResult.Unauthorized("令牌设备绑定校验失败"));

                if (!string.IsNullOrWhiteSpace(user.ClientToken)
                    && !string.Equals(rawToken, user.ClientToken, StringComparison.Ordinal))
                    return (null, ApiResult.Unauthorized("客户端登录状态已更新，请重新登录"));
            }
            else if (string.Equals(tokenUse, "web", StringComparison.OrdinalIgnoreCase))
            {
                if (!isBrowser)
                    return (null, ApiResult.Unauthorized("当前令牌仅允许 Web 浏览器使用"));

                // Web 多端并存：不再精确匹配 user.WebToken，只校验签名与有效期（由 JwtHelper.ValidateToken 完成）。
            }

            // 密码重置后强制所有旧会话失效：比较 JWT iat（秒）与 TokenInvalidBefore（毫秒基准）
            if (user.TokenInvalidBefore > 0)
            {
                var iatClaim = principal.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Iat)?.Value;
                if (long.TryParse(iatClaim, out var iatSeconds))
                {
                    var iatMs = iatSeconds * 1000L; // 转毫秒比较
                    if (iatMs < user.TokenInvalidBefore)
                        return (null, ApiResult.Unauthorized("登录状态已过期，请重新登录"));
                }
            }

            return (user, null);
        }

        #endregion

        #region 请求来源判断

        /// <summary>
        /// 判断当前请求是否来自浏览器环境（用于区分 web/client token 使用场景）。
        /// </summary>
        private static bool IsBrowserRequest(HttpRequest request)
        {
            var userAgent = request.Headers["User-Agent"] ?? string.Empty;
            if (userAgent.IndexOf("mozilla", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            if (!string.IsNullOrWhiteSpace(request.Headers["Sec-Fetch-Mode"]))
                return true;

            if (!string.IsNullOrWhiteSpace(request.Headers["Origin"]))
                return true;

            var accept = request.Headers["Accept"] ?? string.Empty;
            return accept.IndexOf("text/html", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 根据用户名与 HID 计算客户端绑定摘要。
        /// </summary>
        private static string ComputeClientSeed(string userName, string hid)
        {
            var value = $"{hid}:{userName}";
            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        /// <summary>
        /// 解析请求 traceId：优先读取请求头，其次生成新的 GUID。
        /// </summary>
        /// <param name="request">HTTP 请求对象。</param>
        /// <returns>非空 traceId 字符串。</returns>
        private static string ResolveTraceId(HttpRequest request)
        {
            var traceId = (request.Headers[TraceIdHeader] ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(traceId))
                traceId = (request.Headers[RequestIdHeader] ?? string.Empty).Trim();

            return string.IsNullOrWhiteSpace(traceId)
                ? Guid.NewGuid().ToString("N")
                : traceId;
        }

        #endregion

        #region 权限策略入口

        /// <summary>
        /// 根据语义化策略完成认证与权限校验。
        /// </summary>
        /// <param name="request">HTTP 请求。</param>
        /// <param name="policy">访问策略（登录用户/管理员/系统管理员）。</param>
        /// <returns>(user, error) — 成功时 error 为 null，user 保证非 null。</returns>
        public static async Task<(UserData? user, IActionResult? error)> RequirePolicyAsync(HttpRequest request, ApiAccessPolicy policy)
        {
            var (user, error) = await GetUserAsync(request);
            if (error != null) return (null, error);

            switch (policy)
            {
                case ApiAccessPolicy.Admin:
                    if (!IsAdmin(user!.PermissionGroup))
                        return (null, ApiResult.Forbidden("权限不足"));
                    break;

                case ApiAccessPolicy.System:
                    if (!IsSystem(user!.PermissionGroup))
                        return (null, ApiResult.Forbidden("权限不足：需要 System 权限"));
                    break;
            }

            return (user, null);
        }

        /// <summary>
        /// 根据语义化策略完成认证与权限校验，并仅返回用户名。
        /// </summary>
        /// <param name="request">HTTP 请求。</param>
        /// <param name="policy">访问策略（登录用户/管理员/系统管理员）。</param>
        /// <returns>(userName, error) — 成功时 error 为 null。</returns>
        public static async Task<(string? userName, IActionResult? error)> RequirePolicyNameAsync(HttpRequest request, ApiAccessPolicy policy)
        {
            var (user, error) = await RequirePolicyAsync(request, policy);
            if (error != null) return (null, error);
            return (user!.UserName, null);
        }

        #endregion

        #region 管理员验证

        /// <summary>
        /// 检查用户是否拥有管理员权限（System / Console / Admin）。
        /// </summary>
        private static bool IsAdmin(UserPermissionGroup group)
        {
            return group == UserPermissionGroup.System
                || group == UserPermissionGroup.Console
                || group == UserPermissionGroup.Admin;
        }

        /// <summary>
        /// 检查用户是否为系统管理员（仅 System/权限组0）。
        /// 用于需要最高权限控制的功能（如资产管理）。
        /// </summary>
        private static bool IsSystem(UserPermissionGroup group)
        {
            return group == UserPermissionGroup.System;
        }

        /// <summary>
        /// 认证用户并验证管理员权限。
        /// 验证步骤：Bearer Token → JWT 验证 → 提取用户名 → 封禁检查 → 查询 UserData → 权限验证
        /// </summary>
        /// <param name="request">HTTP 请求</param>
        /// <returns>(user, error) — 成功时 error 为 null，user 保证非 null 且是管理员</returns>
        public static async Task<(UserData? user, IActionResult? error)> RequireAdminAsync(HttpRequest request)
        {
            return await RequirePolicyAsync(request, ApiAccessPolicy.Admin);
        }

        /// <summary>
        /// 仅做认证 + 封禁 + 管理员检查，不返回用户对象（用于不需要 user 的管理接口）。
        /// </summary>
        /// <param name="request">HTTP 请求</param>
        /// <returns>(userName, error) — 成功时 error 为 null</returns>
        public static async Task<(string? userName, IActionResult? error)> RequireAdminNameAsync(HttpRequest request)
        {
            return await RequirePolicyNameAsync(request, ApiAccessPolicy.Admin);
        }

        #endregion

        #region 邮件发送辅助（Unified Email）

        /// <summary>
        /// 统一邮件发送入口。
        /// 集中处理参数校验、SMTP配置、异常处理，避免 Handler 中重复代码。
        /// </summary>
        /// <param name="to">收件人邮箱。</param>
        /// <param name="subject">邮件主题。</param>
        /// <param name="body">邮件内容（支持纯文本或 HTML）。</param>
        /// <param name="isHtml">是否为 HTML 邮件；默认 false（纯文本）。</param>
        /// <param name="senderAddress">发件人地址（可选，默认读取全局配置）。</param>
        /// <param name="authCode">发件人授权码（可选，默认使用内置授权码）。</param>
        /// <returns>发送成功返回 true；异常已捕获，返回 false。</returns>
        public static bool SendEmailUnified(
            string to,
            string subject,
            string body,
            bool isHtml = false,
            string? senderAddress = null,
            string? authCode = null)
        {
            if (string.IsNullOrWhiteSpace(to)) return false;
            if (string.IsNullOrWhiteSpace(subject)) return false;
            if (string.IsNullOrWhiteSpace(body)) return false;

            try
            {
                senderAddress ??= "xxx@qq.com";
                authCode ??= "umrroeavogwsdjci"; // 内置默认授权码

                var options = new Drx.Sdk.Network.Email.EmailSenderOptions
                {
                    SenderAddress = senderAddress,
                    Password = authCode,
                    SmtpHost = "smtp.qq.com",
                    SmtpPort = 587,
                    EnableSsl = true
                };

                var contentType = isHtml
                    ? Drx.Sdk.Network.Email.EmailContentType.Html
                    : Drx.Sdk.Network.Email.EmailContentType.PlainText;

                var sender = new Drx.Sdk.Network.Email.SmtpEmailSender(options);
                var message = Drx.Sdk.Network.Email.EmailMessage.Create(to, subject, body, contentType);
                return sender.TrySendAsync(message).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                try { Logger.Error($"unified email send failed: {ex}"); } catch { }
                return false;
            }
        }

        /// <summary>
        /// 异步邮件发送入口。
        /// </summary>
        /// <param name="to">收件人邮箱。</param>
        /// <param name="subject">邮件主题。</param>
        /// <param name="body">邮件内容。</param>
        /// <param name="isHtml">是否为 HTML 邮件；默认 false。</param>
        /// <param name="senderAddress">发件人地址（可选）。</param>
        /// <param name="authCode">发件人授权码（可选）。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>发送成功返回 true；异常已捕获，返回 false。</returns>
        public static async Task<bool> SendEmailUnifiedAsync(
            string to,
            string subject,
            string body,
            bool isHtml = false,
            string? senderAddress = null,
            string? authCode = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(to)) return false;
            if (string.IsNullOrWhiteSpace(subject)) return false;
            if (string.IsNullOrWhiteSpace(body)) return false;

            try
            {
                senderAddress ??= "xxx@qq.com";
                authCode ??= "umrroeavogwsdjci";

                var options = new Drx.Sdk.Network.Email.EmailSenderOptions
                {
                    SenderAddress = senderAddress,
                    Password = authCode,
                    SmtpHost = "smtp.qq.com",
                    SmtpPort = 587,
                    EnableSsl = true
                };

                var contentType = isHtml
                    ? Drx.Sdk.Network.Email.EmailContentType.Html
                    : Drx.Sdk.Network.Email.EmailContentType.PlainText;

                var sender = new Drx.Sdk.Network.Email.SmtpEmailSender(options);
                var message = Drx.Sdk.Network.Email.EmailMessage.Create(to, subject, body, contentType);
                return await sender.TrySendAsync(message, cancellationToken);
            }
            catch (Exception ex)
            {
                try { Logger.Error($"async unified email send failed: {ex}"); } catch { }
                return false;
            }
        }

        /// <summary>
        /// 发送验证码邮件（特化方法）。
        /// 验证码邮件采用统一格式，方便跟踪和管理。
        /// </summary>
        /// <param name="to">收件人邮箱。</param>
        /// <param name="verificationCode">验证码内容。</param>
        /// <param name="purpose">验证码用途（如 "注册验证" / "重置密码" / "修改邮箱"）。</param>
        /// <param name="expirationMinutes">验证码有效期（分钟）；用于邮件提示。</param>
        /// <returns>发送成功返回 true。</returns>
        public static bool SendVerificationCodeEmail(
            string to,
            string verificationCode,
            string purpose = "验证码",
            int expirationMinutes = 10)
        {
            if (string.IsNullOrWhiteSpace(to)) return false;
            if (string.IsNullOrWhiteSpace(verificationCode)) return false;

            var subject = $"您的{purpose}验证码";
            var body = $"""
                验证码：{verificationCode}
                
                有效期：{expirationMinutes} 分钟
                如非本人操作，请忽略此邮件。
                """;

            return SendEmailUnified(to, subject, body, isHtml: false);
        }

        /// <summary>
        /// 异步发送验证码邮件。
        /// </summary>
        public static async Task<bool> SendVerificationCodeEmailAsync(
            string to,
            string verificationCode,
            string purpose = "验证码",
            int expirationMinutes = 10,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(to)) return false;
            if (string.IsNullOrWhiteSpace(verificationCode)) return false;

            var subject = $"您的{purpose}验证码";
            var body = $"""
                验证码：{verificationCode}
                
                有效期：{expirationMinutes} 分钟
                如非本人操作，请忽略此邮件。
                """;

            return await SendEmailUnifiedAsync(to, subject, body, isHtml: false, cancellationToken: cancellationToken);
        }

        #endregion

        #region 系统管理员验证（仅权限组0）

        /// <summary>
        /// 认证用户并验证系统管理员权限（仅 permissionGroup=0）。
        /// 用于需要最高权限控制的功能，如资产管理。
        /// 验证步骤：Bearer Token → JWT 验证 → 提取用户名 → 封禁检查 → 查询 UserData → System权限验证
        /// </summary>
        /// <param name="request">HTTP 请求</param>
        /// <returns>(user, error) — 成功时 error 为 null，user 保证非 null 且是系统管理员</returns>
        public static async Task<(UserData? user, IActionResult? error)> RequireSystemAsync(HttpRequest request)
        {
            return await RequirePolicyAsync(request, ApiAccessPolicy.System);
        }

        /// <summary>
        /// 仅做认证 + 封禁 + 系统管理员检查，不返回用户对象（用于不需要 user 的系统管理接口）。
        /// </summary>
        /// <param name="request">HTTP 请求</param>
        /// <returns>(userName, error) — 成功时 error 为 null</returns>
        public static async Task<(string? userName, IActionResult? error)> RequireSystemNameAsync(HttpRequest request)
        {
            return await RequirePolicyNameAsync(request, ApiAccessPolicy.System);
        }

        #endregion
    }
}

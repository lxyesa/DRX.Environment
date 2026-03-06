using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Drx.Sdk.Network.Http.Configs;
using Drx.Sdk.Network.Email;
using Drx.Sdk.Network.Http;
using Drx.Sdk.Network.Http.Api;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.Results;
using Drx.Sdk.Shared;
using Drx.Sdk.Shared.Utility;
using KaxSocket;
using KaxSocket.Handlers.Helpers;

namespace KaxSocket.Handlers;

/// <summary>
/// 用户资料管理模块 - 处理用户资料查询、更新、头像管理等功能
/// </summary>
public partial class KaxHttp
{
    #region 用户资料管理 (User Profile Management)

    // GET  /api/user/profile   -> 返回当前登录用户的资料
    // POST /api/user/profile   -> 更新当前登录用户的资料（displayName, email, bio）
    [HttpHandle("/api/user/profile", "GET", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Get_UserProfile(HttpRequest request)
    {
        var (user, error) = await Api.GetUserAsync(request);
        if (error != null) return error;

        var respDisplayName = string.IsNullOrEmpty(user!.DisplayName) ? user.UserName : user.DisplayName;
        var respEmail = user.Email ?? string.Empty;
        var respBio = string.Empty;
        var bioProp = user.GetType().GetProperty("Bio");
        if (bioProp != null) respBio = (bioProp.GetValue(user) as string) ?? string.Empty;
        var respSignature = string.Empty;
        var signatureProp = user.GetType().GetProperty("Signature");
        if (signatureProp != null) respSignature = (signatureProp.GetValue(user) as string) ?? string.Empty;
        var respRegisteredAt = user.RegisteredAt;
        var respLastLoginAt = user.LastLoginAt;

        // 若存在服务器头像文件，则提供可访问的头像 URL（前端将直接使用该 URL）
        string avatarUrl = string.Empty;
        try
        {
            var avatarPath = KaxGlobal.GetUserAvatarPathById(user.Id);
            if (!string.IsNullOrEmpty(avatarPath))
            {
                var stamp = respLastLoginAt > 0 ? respLastLoginAt : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                avatarUrl = $"/api/user/avatar/{user.Id}?v={stamp}";
            }
        }
        catch { }

        // 额外动态字段：resourceCount / gold / recentActivity / cdkCount
        var resourceCount = 0; var gold = 0; var recentActivity = 0; var cdkCount = 0;
        try { resourceCount = user.ResourceCount; gold = user.Gold; recentActivity = user.RecentActivity; } catch { }
        try { cdkCount = await KaxGlobal.GetUserCdkCountAsync(user.UserName); } catch { }

        return new JsonResult(new
        {
            id = user.Id,
            user = user.UserName,
            displayName = respDisplayName,
            email = respEmail,
            bio = respBio,
            signature = respSignature,
            badges = BadgeHelper.ParseBadges(user.Badges),
            registeredAt = respRegisteredAt,
            lastLoginAt = respLastLoginAt,
            permissionGroup = (int)user.PermissionGroup,
            // 保留旧字段以兼容前端
            isBanned = user.Status != null && user.Status.IsBanned,
            bannedAt = user.Status != null ? user.Status.BannedAt : 0,
            banExpiresAt = user.Status != null ? user.Status.BanExpiresAt : 0,
            banReason = user.Status != null ? (user.Status.BanReason ?? string.Empty) : string.Empty,
            avatarUrl = avatarUrl,
            resourceCount = resourceCount,
            gold = gold,
            recentActivity = recentActivity,
            cdkCount = cdkCount
        });
    }

    // GET /api/user/profile/{uid} -> 返回指定用户的资料（公开信息）
    [HttpHandle("/api/user/profile/{uid}", "GET", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Get_UserProfileByUid(HttpRequest request)
    {
        var (_, authError) = await Api.AuthenticateAndCheckBanAsync(request);
        if (authError != null) return authError;

        // 从路径中提取 uid
        var uidStr = request.Path.Split('/').LastOrDefault();
        if (!long.TryParse(uidStr, out var uid))
            return new JsonResult(new { message = "无效的用户 ID" }, 400);

        try
        {
            var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("Id", uid)).FirstOrDefault();
            if (user == null) return new JsonResult(new { message = "用户不存在" }, 404);

            // 检查目标用户是否被封禁
            if (user.Status != null && user.Status.IsBanned)
                return new JsonResult(new { message = "该用户已被封禁" }, 403);

            var respDisplayName = string.IsNullOrEmpty(user.DisplayName) ? user.UserName : user.DisplayName;
            var respEmail = user.Email ?? string.Empty;
            var respBio = string.Empty;
            var bioProp = user.GetType().GetProperty("Bio");
            if (bioProp != null) respBio = (bioProp.GetValue(user) as string) ?? string.Empty;
            var respSignature = string.Empty;
            var signatureProp = user.GetType().GetProperty("Signature");
            if (signatureProp != null) respSignature = (signatureProp.GetValue(user) as string) ?? string.Empty;
            var respRegisteredAt = user.RegisteredAt;
            var respLastLoginAt = user.LastLoginAt;

            // 若存在服务器头像文件，则提供可访问的头像 URL
            string avatarUrl = string.Empty;
            try
            {
                var avatarPath = KaxGlobal.GetUserAvatarPathById(user.Id);
                if (!string.IsNullOrEmpty(avatarPath))
                {
                    var stamp = respLastLoginAt > 0 ? respLastLoginAt : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    avatarUrl = $"/api/user/avatar/{user.Id}?v={stamp}";
                }
            }
            catch { }

            // 额外动态字段
            var resourceCount = 0; var gold = 0; var recentActivity = 0; var cdkCount = 0;
            try { resourceCount = user.ResourceCount; gold = user.Gold; recentActivity = user.RecentActivity; } catch { }
            try { cdkCount = await KaxGlobal.GetUserCdkCountAsync(user.UserName); } catch { }

            return new JsonResult(new
            {
                id = user.Id,
                user = user.UserName,
                displayName = respDisplayName,
                email = respEmail,
                bio = respBio,
                signature = respSignature,
                badges = BadgeHelper.ParseBadges(user.Badges),
                registeredAt = respRegisteredAt,
                lastLoginAt = respLastLoginAt,
                permissionGroup = (int)user.PermissionGroup,
                isBanned = user.Status != null && user.Status.IsBanned,
                bannedAt = user.Status != null ? user.Status.BannedAt : 0,
                banExpiresAt = user.Status != null ? user.Status.BanExpiresAt : 0,
                banReason = user.Status != null ? (user.Status.BanReason ?? string.Empty) : string.Empty,
                avatarUrl = avatarUrl,
                resourceCount = resourceCount,
                gold = gold,
                recentActivity = recentActivity,
                cdkCount = cdkCount
            });
        }
        catch (Exception ex)
        {
            Logger.Error("获取用户资料时出错: " + ex.Message);
            return new JsonResult(new { message = "服务器错误" }, 500);
        }
    }

    // POST /api/user/email-change/send-code -> 发送邮箱变更验证码（channel: old/new）
    [HttpHandle("/api/user/email-change/send-code", "POST", RateLimitMaxRequests = 20, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_SendEmailChangeCode(HttpRequest request, DrxHttpServer server)
    {
        var (userName, authError) = await Api.AuthenticateAndCheckBanAsync(request);
        if (authError != null) return authError;

        if (!ApiBody.TryParse(request, out var body, out var parseError))
            return parseError!;

        var channel = body["channel"]?.ToString()?.Trim().ToLowerInvariant() ?? string.Empty;
        var targetUidStr = body["targetUid"]?.ToString() ?? string.Empty;
        var newEmail = body["newEmail"]?.ToString()?.Trim() ?? string.Empty;

        if (channel != "old" && channel != "new")
            return new JsonResult(new { code = 46001, message = "channel 参数无效，仅支持 old/new" }, 400);

        if (string.IsNullOrEmpty(targetUidStr) || !long.TryParse(targetUidStr, out var targetUid) || targetUid <= 0)
            return new JsonResult(new { code = 46001, message = "targetUid 参数无效" }, 400);

        try
        {
            var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName!)).FirstOrDefault();
            if (user == null) return new JsonResult(new { code = 404, message = "用户不存在" }, 404);

            // 权限校验：只能给自己发验证码
            if (user.Id != targetUid)
            {
                Logger.Warn($"用户 {userName}(ID:{user.Id}) 尝试为他人发送邮箱验证码（目标 UID: {targetUid}），请求被拒绝");
                return new JsonResult(new { code = 403, message = "无权操作他人资料" }, 403);
            }

            string targetEmail;
            if (channel == "old")
            {
                targetEmail = (user.Email ?? string.Empty).Trim();
                if (!Api.IsValidEmailFormat(targetEmail))
                    return new JsonResult(new { code = 46001, message = "当前邮箱不可用，无法发送验证码" }, 400);
            }
            else
            {
                if (!Api.IsValidEmailFormat(newEmail))
                    return new JsonResult(new { code = 46001, message = "新邮箱格式无效" }, 400);

                if (string.Equals(user.Email ?? string.Empty, newEmail, StringComparison.OrdinalIgnoreCase))
                    return new JsonResult(new { code = 46001, message = "新邮箱不能与当前邮箱相同" }, 400);

                var byEmail = (await KaxGlobal.UserDatabase.SelectWhereAsync("Email", newEmail)).FirstOrDefault();
                if (byEmail != null && !string.Equals(byEmail.UserName, user.UserName, StringComparison.OrdinalIgnoreCase))
                    return new JsonResult(new { code = 46002, message = "该邮箱已被占用" }, 409);

                targetEmail = newEmail;
            }

            KaxGlobal.EnsureEmailChangeVerificationStore(user);

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var relatedRecords = user.EmailChangeVerifications
                .Where(x => string.Equals(x.Channel, channel, StringComparison.OrdinalIgnoreCase)
                            && (channel != "new" || string.Equals(x.NewEmail ?? string.Empty, newEmail, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(x => x.CreatedAt)
                .ToList();

            var latestAt = relatedRecords.FirstOrDefault()?.CreatedAt ?? 0;
            if (latestAt > 0 && now - latestAt < 30_000)
            {
                Logger.Warn($"邮箱验证码发送频率过高: user={userName}, channel={channel}, target={Api.MaskEmail(targetEmail)}");
                return new JsonResult(new { code = 46003, message = "发送过快，请 30 秒后重试" }, 429);
            }

            var hourlyCount = relatedRecords.Count(x => x.CreatedAt >= now - 3_600_000);
            if (hourlyCount >= 10)
            {
                Logger.Warn($"邮箱验证码发送命中小时上限: user={userName}, channel={channel}, count={hourlyCount}");
                return new JsonResult(new { code = 46004, message = "发送次数已达每小时上限" }, 429);
            }

            var dailyCount = relatedRecords.Count(x => x.CreatedAt >= now - 86_400_000);
            if (dailyCount >= 50)
            {
                Logger.Warn($"邮箱验证码发送命中日上限: user={userName}, channel={channel}, count={dailyCount}");
                return new JsonResult(new { code = 46005, message = "发送次数已达每日上限" }, 429);
            }

            var smtpEmail = "157335596@qq.com";
            var smtpAuthCode = "eymlrhwykskccbdb";
            var smtpHost = Environment.GetEnvironmentVariable("KAX_SMTP_HOST") ?? "smtp.qq.com";
            var smtpPort = int.TryParse(Environment.GetEnvironmentVariable("KAX_SMTP_PORT"), out var p) ? p : 587;
            var smtpEnableSsl = !bool.TryParse(Environment.GetEnvironmentVariable("KAX_SMTP_ENABLE_SSL"), out var ssl) || ssl;

            if (string.IsNullOrWhiteSpace(smtpEmail) || string.IsNullOrWhiteSpace(smtpAuthCode))
            {
                Logger.Error($"SMTP 配置缺失，无法发送邮箱验证码: user={userName}, channel={channel}");
                return new JsonResult(new { code = 500, message = "SMTP 配置缺失，请先配置 KAX_SMTP_EMAIL 与 KAX_SMTP_AUTH_CODE" }, 500);
            }

            var code = Api.GenerateVerificationCode(8);
            var salt = Api.GenerateVerificationSalt();
            var hash = Api.HashVerificationCode(code, salt);

            var subject = channel == "old" ? "KaxHub · 旧邮箱验证码" : "KaxHub · 新邮箱验证码";
            var channelLabel = channel == "old" ? "旧邮箱验证" : "新邮箱验证";
            var displayName = user.DisplayName ?? user.UserName;
            var bodyText = $"""
<!DOCTYPE html>
<html lang="zh-CN">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>KaxHub 邮箱验证</title>
</head>
<body style="margin:0;padding:0;background:#0d1117;font-family:'Segoe UI',Arial,sans-serif;">
  <table width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#0d1117;padding:40px 0;">
    <tr>
      <td align="center">
        <table width="560" cellpadding="0" cellspacing="0" border="0"
               style="background:#161b22;border-radius:12px;border:1px solid #30363d;overflow:hidden;max-width:560px;width:100%;">

          <!-- Header -->
          <tr>
            <td style="background:linear-gradient(135deg,#1a2332 0%,#0d2137 100%);padding:32px 40px;border-bottom:1px solid #21262d;">
              <table width="100%" cellpadding="0" cellspacing="0" border="0">
                <tr>
                  <td>
                    <span style="font-size:22px;font-weight:700;color:#58a6ff;letter-spacing:1px;">Kax</span><span style="font-size:22px;font-weight:700;color:#e6edf3;letter-spacing:1px;">Hub</span>
                  </td>
                  <td align="right">
                    <span style="display:inline-block;padding:4px 12px;background:rgba(88,166,255,0.12);border:1px solid rgba(88,166,255,0.3);border-radius:20px;font-size:11px;color:#58a6ff;letter-spacing:0.5px;">{channelLabel}</span>
                  </td>
                </tr>
              </table>
            </td>
          </tr>

          <!-- Body -->
          <tr>
            <td style="padding:40px 40px 32px;">
              <p style="margin:0 0 8px;font-size:15px;color:#8b949e;">您好，</p>
              <p style="margin:0 0 28px;font-size:20px;font-weight:600;color:#e6edf3;">{displayName}</p>

              <p style="margin:0 0 28px;font-size:14px;color:#8b949e;line-height:1.7;">
                您正在对绑定的<strong style="color:#c9d1d9;">邮箱</strong>进行变更操作（{channelLabel}），请使用以下验证码完成验证：
              </p>

              <!-- Code Card -->
              <table width="100%" cellpadding="0" cellspacing="0" border="0" style="margin-bottom:28px;">
                <tr>
                  <td align="center" style="background:#0d1117;border:1px solid #30363d;border-radius:10px;padding:28px 20px;">
                    <p style="margin:0 0 10px;font-size:11px;text-transform:uppercase;letter-spacing:2px;color:#6e7681;">验证码</p>
                    <p style="margin:0;font-size:38px;font-weight:700;letter-spacing:10px;color:#58a6ff;font-family:'Courier New',Courier,monospace;">{code}</p>
                  </td>
                </tr>
              </table>

              <!-- Expiry Notice -->
              <table width="100%" cellpadding="0" cellspacing="0" border="0" style="margin-bottom:28px;">
                <tr>
                  <td style="background:rgba(248,166,0,0.08);border:1px solid rgba(248,166,0,0.2);border-left:3px solid #f8a600;border-radius:0 6px 6px 0;padding:12px 16px;">
                    <p style="margin:0;font-size:13px;color:#f8a600;">⏱ 验证码有效期为 <strong>10 分钟</strong>，请尽快使用。</p>
                  </td>
                </tr>
              </table>

              <p style="margin:0;font-size:13px;color:#6e7681;line-height:1.7;">
                若非本人操作，请忽略此邮件。您的账号安全不会受到影响。
              </p>
            </td>
          </tr>

          <!-- Divider -->
          <tr>
            <td style="padding:0 40px;"><div style="border-top:1px solid #21262d;"></div></td>
          </tr>

          <!-- Footer -->
          <tr>
            <td style="padding:24px 40px;">
              <p style="margin:0;font-size:12px;color:#484f58;line-height:1.7;">
                此邮件由 <span style="color:#58a6ff;">KaxHub</span> 系统自动发送，请勿直接回复。<br />
                &copy; {DateTime.UtcNow.Year} KaxHub. All rights reserved.
              </p>
            </td>
          </tr>

        </table>
      </td>
    </tr>
  </table>
</body>
</html>
""";

            var cfg = new EmailConfig
            {
                SenderAddress = smtpEmail,
                Password = smtpAuthCode,
                To = targetEmail,
                Subject = subject,
                Body = bodyText,
                SmtpHost = smtpHost,
                SmtpPort = smtpPort,
                EnableSsl = smtpEnableSsl,
                ContentType = EmailContentType.Html
            };

            var sent = await server.SendEmailAsync(cfg);
            if (!sent)
            {
                Logger.Error($"邮箱验证码发送失败: user={userName}, channel={channel}, target={Api.MaskEmail(targetEmail)}");
                return new JsonResult(new { code = 500, message = "验证码发送失败，请稍后重试" }, 500);
            }

            user.EmailChangeVerifications.Add(new EmailChangeVerification
            {
                ParentId = user.Id,
                Channel = channel,
                NewEmail = channel == "new" ? newEmail : string.Empty,
                CodeHash = hash,
                CodeSalt = salt,
                Status = "pending",
                Attempts = 0,
                MaxAttempts = 5,
                ExpiresAt = now + 10 * 60 * 1000,
                UsedAt = 0,
                RequestIp = request.ClientAddress.Ip ?? string.Empty,
                UserAgent = request.Headers["User-Agent"] ?? request.Headers["user-agent"] ?? string.Empty,
                LastSendAt = now,
                HourlyCount = hourlyCount + 1,
                DailyCount = dailyCount + 1,
                WindowHourStartAt = now,
                WindowDayStartAt = now
            });

            await KaxGlobal.UserDatabase.UpdateAsync(user);

            Logger.Info($"用户 {userName} 发送邮箱验证码成功，通道={channel}，目标={Api.MaskEmail(targetEmail)}");
            return new JsonResult(new
            {
                code = 0,
                message = "验证码已发送",
                data = new
                {
                    channel,
                    expireAt = now + 10 * 60 * 1000,
                    cooldownSeconds = 30
                }
            }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"发送邮箱验证码失败: {ex.Message}");
            return new JsonResult(new { code = 500, message = "服务器错误" }, 500);
        }
    }

    // POST /api/user/email-change/confirm -> 校验旧邮箱验证码并更新邮箱
    [HttpHandle("/api/user/email-change/confirm", "POST", RateLimitMaxRequests = 15, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_ConfirmEmailChange(HttpRequest request)
    {
        var (userName, authError) = await Api.AuthenticateAndCheckBanAsync(request);
        if (authError != null) return authError;

        if (!ApiBody.TryParse(request, out var body, out var parseError))
            return parseError!;

        var targetUidStr = body["targetUid"]?.ToString() ?? string.Empty;
        var newEmail = body["newEmail"]?.ToString()?.Trim() ?? string.Empty;
        var oldEmailCode = body["oldEmailCode"]?.ToString()?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(targetUidStr) || !long.TryParse(targetUidStr, out var targetUid) || targetUid <= 0)
            return new JsonResult(new { code = 46001, message = "targetUid 参数无效" }, 400);

        if (!Api.IsValidEmailFormat(newEmail))
            return new JsonResult(new { code = 46001, message = "新邮箱格式无效" }, 400);

        if (string.IsNullOrWhiteSpace(oldEmailCode))
            return new JsonResult(new { code = 46001, message = "旧邮箱验证码为必填" }, 400);

        try
        {
            var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName!)).FirstOrDefault();
            if (user == null) return new JsonResult(new { code = 404, message = "用户不存在" }, 404);

            if (user.Id != targetUid)
            {
                Logger.Warn($"用户 {userName}(ID:{user.Id}) 尝试确认他人邮箱变更（目标 UID: {targetUid}），请求被拒绝");
                return new JsonResult(new { code = 403, message = "无权操作他人资料" }, 403);
            }

            if (string.Equals(user.Email ?? string.Empty, newEmail, StringComparison.OrdinalIgnoreCase))
                return new JsonResult(new { code = 46001, message = "新邮箱不能与当前邮箱相同" }, 400);

            var byEmail = (await KaxGlobal.UserDatabase.SelectWhereAsync("Email", newEmail)).FirstOrDefault();
            if (byEmail != null && !string.Equals(byEmail.UserName, user.UserName, StringComparison.OrdinalIgnoreCase))
                return new JsonResult(new { code = 46002, message = "该邮箱已被占用" }, 409);

            KaxGlobal.EnsureEmailChangeVerificationStore(user);

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var oldRecord = user.EmailChangeVerifications
                .Where(x => string.Equals(x.Channel, "old", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefault();

            if (oldRecord == null)
            {
                Logger.Warn($"邮箱变更确认失败：验证码记录缺失 user={userName}, new={Api.MaskEmail(newEmail)}");
                return new JsonResult(new { code = 46010, message = "请先完成旧邮箱验证码发送" }, 400);
            }

            var oldEmailBefore = user.Email ?? string.Empty;

            IActionResult? validateError = ValidateAndConsumeAttempt(user, oldRecord, oldEmailCode, now, isOldChannel: true);
            if (validateError != null)
            {
                await KaxGlobal.UserDatabase.UpdateAsync(user);
                return validateError;
            }

            oldRecord.Status = "used";
            oldRecord.UsedAt = now;
            oldRecord.UpdatedAt = now;

            // 邮箱更新属于敏感操作：清理用户会话标记并要求重新登录
            user.LoginToken = string.Empty;

            var committed = await KaxGlobal.CommitEmailChangeAsync(user, newEmail);
            if (!committed)
                return new JsonResult(new { code = 500, message = "邮箱更新失败" }, 500);

            RevokeTokenFromRequest(request);

            Logger.Info($"用户 {userName} 完成邮箱变更: {Api.MaskEmail(oldEmailBefore)} -> {Api.MaskEmail(newEmail)}");
            return new JsonResult(new
            {
                code = 0,
                message = "邮箱已更新，请重新登录",
                data = new
                {
                    emailMasked = Api.MaskEmail(newEmail),
                    requireRelogin = true
                }
            }, 200);
        }
        catch (Exception ex)
        {
            Logger.Error($"确认邮箱变更失败: {ex.Message}");
            return new JsonResult(new { code = 500, message = "服务器错误" }, 500);
        }
    }

    private static IActionResult? ValidateAndConsumeAttempt(UserData user, EmailChangeVerification record, string inputCode, long now, bool isOldChannel)
    {
        var channelLabel = isOldChannel ? "旧邮箱" : "新邮箱";

        if (string.Equals(record.Status, "used", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Warn($"{channelLabel}验证码重复使用: userId={user.Id}");
            return new JsonResult(new { code = 46008, message = $"{channelLabel}验证码已使用" }, 400);
        }

        if (now > record.ExpiresAt)
        {
            record.Status = "expired";
            record.UpdatedAt = now;
            Logger.Warn($"{channelLabel}验证码过期: userId={user.Id}, recordId={record.Id}");
            return new JsonResult(new { code = 46007, message = $"{channelLabel}验证码已过期" }, 400);
        }

        if (record.Attempts >= record.MaxAttempts)
        {
            record.Status = "locked";
            record.UpdatedAt = now;
            Logger.Warn($"{channelLabel}验证码已锁定: userId={user.Id}, recordId={record.Id}");
            return new JsonResult(new { code = 46009, message = $"{channelLabel}验证码尝试次数过多" }, 400);
        }

        if (!Api.VerifyVerificationCode(inputCode, record.CodeSalt, record.CodeHash))
        {
            record.Attempts += 1;
            record.UpdatedAt = now;
            if (record.Attempts >= record.MaxAttempts)
                record.Status = "locked";

            Logger.Warn($"{channelLabel}验证码校验失败: userId={user.Id}, attempts={record.Attempts}/{record.MaxAttempts}");

            return new JsonResult(new { code = 46006, message = $"{channelLabel}验证码错误" }, 400);
        }

        return null;
    }

    [HttpHandle("/api/user/profile", "POST", RateLimitMaxRequests = 10, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_UpdateUserProfile(HttpRequest request)
    {
        var (userName, authError) = await Api.AuthenticateAndCheckBanAsync(request);
        if (authError != null) return authError;

        if (!ApiBody.TryParse(request, out var body, out var parseError))
            return parseError!;

        var displayName = body["displayName"]?.ToString()?.Trim() ?? string.Empty;
        var email = body["email"]?.ToString()?.Trim() ?? string.Empty;
        var bio = body["bio"]?.ToString() ?? string.Empty;
        var signature = body["signature"]?.ToString() ?? string.Empty;
        var targetUidStr = body["targetUid"]?.ToString() ?? string.Empty;

        // 基础校验
        if (!string.IsNullOrEmpty(email) && !Api.IsValidEmailFormat(email))
            return new JsonResult(new { message = "无效的电子邮箱地址" }, 400);

        // 验证 targetUid 参数
        if (string.IsNullOrEmpty(targetUidStr))
            return new JsonResult(new { message = "targetUid 参数缺失" }, 400);

        if (!long.TryParse(targetUidStr, out var targetUid) || targetUid <= 0)
            return new JsonResult(new { message = "targetUid 参数无效" }, 400);

        try
        {
            var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName!)).FirstOrDefault();
            if (user == null) return new JsonResult(new { message = "用户不存在" }, 404);

            // 权限验证：确保只能更新自己的资料（当前用户 ID 必须与 targetUid 一致）
            if (user.Id != targetUid)
            {
                Logger.Warn($"用户 {userName}(ID:{user.Id}) 尝试修改他人资料（目标 UID: {targetUid}），请求被拒绝");
                return new JsonResult(new { message = "无权修改他人资料" }, 403);
            }

            // 如果邮箱发生变化，检查唯一性
            if (!string.IsNullOrEmpty(email) && !string.Equals(user.Email ?? string.Empty, email, StringComparison.OrdinalIgnoreCase))
            {
                var oldEmail = user.Email ?? string.Empty;
                var byEmail = (await KaxGlobal.UserDatabase.SelectWhereAsync("Email", email)).FirstOrDefault();
                if (byEmail != null && !string.Equals(byEmail.UserName, user.UserName, StringComparison.OrdinalIgnoreCase))
                {
                    return new JsonResult(new { message = "该邮箱已被占用" }, 409);
                }
                user.Email = email;
                Logger.Info($"用户 {userName} 更新邮箱: {Api.MaskEmail(oldEmail)} -> {Api.MaskEmail(email)}");
            }

            if (!string.IsNullOrEmpty(displayName)) user.DisplayName = displayName;

            // 如果模型支持 Bio 字段则保存（向后兼容）
            var prop = user.GetType().GetProperty("Bio");
            if (prop != null) prop.SetValue(user, bio ?? string.Empty);

            // 如果模型支持 Signature 字段则保存
            var signatureProp = user.GetType().GetProperty("Signature");
            if (signatureProp != null) signatureProp.SetValue(user, signature ?? string.Empty);

            await KaxGlobal.UserDatabase.UpdateAsync(user);

            Logger.Info($"用户 {userName} 成功更新了自己的资料");
            return new JsonResult(new { message = "资料已更新" });
        }
        catch (Exception ex)
        {
            Logger.Error("更新用户资料时出错: " + ex.Message);
            return new JsonResult(new { message = "服务器错误" }, 500);
        }
    }

    // POST /api/user/profile/update-field -> 更新当前登录用户的单个资料字段（保留旧全量接口用于兼容）
    [HttpHandle("/api/user/profile/update-field", "POST", RateLimitMaxRequests = 20, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_UpdateUserProfileField(HttpRequest request)
    {
        var (userName, authError) = await Api.AuthenticateAndCheckBanAsync(request);
        if (authError != null) return authError;

        if (!ApiBody.TryParse(request, out var body, out var parseError))
            return parseError!;

        var field = body["field"]?.ToString()?.Trim() ?? string.Empty;
        var targetUidStr = body["targetUid"]?.ToString() ?? string.Empty;
        var valueNode = body["value"];

        if (string.IsNullOrEmpty(field))
            return new JsonResult(new { message = "field 参数缺失" }, 400);

        if (string.IsNullOrEmpty(targetUidStr))
            return new JsonResult(new { message = "targetUid 参数缺失" }, 400);

        if (!long.TryParse(targetUidStr, out var targetUid) || targetUid <= 0)
            return new JsonResult(new { message = "targetUid 参数无效" }, 400);

        field = field.ToLowerInvariant();
        if (field != "displayname" && field != "email" && field != "bio" && field != "signature")
            return new JsonResult(new { message = "不支持的字段，只允许 displayName/email/bio/signature" }, 400);

        try
        {
            var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName!)).FirstOrDefault();
            if (user == null) return new JsonResult(new { message = "用户不存在" }, 404);

            // 权限验证：只能改自己的资料
            if (user.Id != targetUid)
            {
                Logger.Warn($"用户 {userName}(ID:{user.Id}) 尝试单字段修改他人资料（目标 UID: {targetUid}），请求被拒绝");
                return new JsonResult(new { message = "无权修改他人资料" }, 403);
            }

            var value = valueNode?.ToString() ?? string.Empty;
            switch (field)
            {
                case "displayname":
                    value = value.Trim();
                    if (string.IsNullOrEmpty(value))
                        return new JsonResult(new { message = "显示名称不能为空" }, 400);
                    if (value.Length > 100)
                        return new JsonResult(new { message = "显示名称过长（最多100字符）" }, 400);
                    user.DisplayName = value;
                    break;

                case "email":
                    return new JsonResult(new
                    {
                        code = 46011,
                        message = "邮箱字段已切换到验证码流程，请调用 /api/user/email-change/send-code 与 /api/user/email-change/confirm"
                    }, 400);

                case "bio":
                    if (value.Length > 500)
                        return new JsonResult(new { message = "个人简介过长（最多500字符）" }, 400);
                    user.Bio = value;
                    break;

                case "signature":
                    if (value.Length > 500)
                        return new JsonResult(new { message = "个性签名过长（最多500字符）" }, 400);
                    user.Signature = value;
                    break;
            }

            await KaxGlobal.UserDatabase.UpdateAsync(user);

            Logger.Info($"用户 {userName} 单字段更新成功: {field}");
            return new JsonResult(new
            {
                message = "字段已更新",
                field,
                value
            });
        }
        catch (Exception ex)
        {
            Logger.Error("单字段更新用户资料时出错: " + ex.Message);
            return new JsonResult(new { message = "服务器错误" }, 500);
        }
    }

    // POST /api/user/password -> 修改当前登录用户的密码（需提供旧密码、新密码、确认新密码）
    [HttpHandle("/api/user/password", "POST", RateLimitMaxRequests = 6, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_ChangePassword(HttpRequest request)
    {
        var (userName, authError) = await Api.AuthenticateAndCheckBanAsync(request);
        if (authError != null) return authError;

        if (!ApiBody.TryParse(request, out var body, out var parseError))
            return parseError!;

        var oldPassword = body["oldPassword"]?.ToString() ?? string.Empty;
        var newPassword = body["newPassword"]?.ToString() ?? string.Empty;
        var confirmPassword = body["confirmPassword"]?.ToString() ?? string.Empty;

        if (string.IsNullOrEmpty(oldPassword) || string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmPassword))
            return new JsonResult(new { message = "旧密码/新密码/确认密码均为必填" }, 400);

        if (newPassword.Length < 8) return new JsonResult(new { message = "新密码长度至少 8 位" }, 400);
        if (newPassword != confirmPassword) return new JsonResult(new { message = "两次新密码不匹配" }, 400);

        try
        {
            var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName!)).FirstOrDefault();
            if (user == null) return new JsonResult(new { message = "用户不存在" }, 404);

            // 安全验证：确保只能修改自己的密码
            // 这是一个防御性检查，防止通过 API 直接修改他人密码
            if (!string.Equals(user.UserName, userName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Warn($"用户 {userName} 尝试修改他人密码（目标用户: {user.UserName}），请求被拒绝");
                return new JsonResult(new { message = "无权修改他人密码" }, 403);
            }

            var oldHash = CommonUtility.ComputeSHA256Hash(oldPassword);
            if (!string.Equals(user.PasswordHash ?? string.Empty, oldHash, StringComparison.Ordinal))
            {
                Logger.Warn($"用户 {userName} 修改密码失败：旧密码不正确。");
                return new JsonResult(new { message = "旧密码不正确" }, 401);
            }

            user.PasswordHash = CommonUtility.ComputeSHA256Hash(newPassword);
            await KaxGlobal.UserDatabase.UpdateAsync(user);

            Logger.Info($"用户 {userName} 已更新密码。");
            return new JsonResult(new { message = "密码已更新" });
        }
        catch (Exception ex)
        {
            Logger.Error("修改密码时出错: " + ex.Message);
            return new JsonResult(new { message = "服务器错误" }, 500);
        }
    }

    // GET /api/user/avatar/{userId} -> 返回指定用户的头像文件（若存在）
    [HttpHandle("/api/user/avatar/{userId}", "GET", RateLimitMaxRequests = 120, RateLimitWindowSeconds = 60)]
    public static async Task<IActionResult> Get_UserAvatar(HttpRequest request)
    {
        if (!request.PathParameters.TryGetValue("userId", out var idStr) || !int.TryParse(idStr, out var userId) || userId <= 0)
            return new JsonResult(new { message = "userId 参数无效" }, 400);

        // 尝试从缓存获取头像
        if (_avatarCache.TryGetAvatar(userId, out var cachedImageData, out var cachedContentType))
        {
            return new BytesResult(cachedImageData, cachedContentType ?? "image/png");
        }

        var path = KaxGlobal.GetUserAvatarPathById(userId);
        if (string.IsNullOrEmpty(path)) return new JsonResult(new { message = "未找到头像" }, 404);

        try
        {
            var imageData = await File.ReadAllBytesAsync(path);
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var contentType = ext == ".png" ? "image/png" : "image/jpeg";

            // 将读取的数据存入缓存
            _avatarCache.SetAvatar(userId, imageData, contentType);

            return new BytesResult(imageData, contentType);
        }
        catch (Exception ex)
        {
            Logger.Error($"读取用户头像失败 (userId: {userId}): {ex.Message}");
            return new JsonResult(new { message = "读取头像失败" }, 500);
        }
    }

    // GET /api/user/stats -> 返回当前登录用户的统计信息（resourceCount / cdkCount / recentActivity / gold）
    [HttpHandle("/api/user/stats", "GET", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Get_UserStats(HttpRequest request)
    {
        var (user, error) = await Api.GetUserAsync(request);
        if (error != null) return error;

        var resourceCount = user!.ResourceCount;
        var gold = user.Gold;
        var recentActivity = user.RecentActivity;
        var cdkCount = await KaxGlobal.GetUserCdkCountAsync(user.UserName);

        return new JsonResult(new
        {
            user = user.UserName,
            resourceCount = resourceCount,
            cdkCount = cdkCount,
            recentActivity = recentActivity,
            gold = gold
        });
    }

    // POST /api/user/avatar -> 上传当前登录用户的头像（multipart/form-data, field name 可任意，使用第一个文件）
    [HttpHandle("/api/user/avatar", "POST", RateLimitMaxRequests = 10, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_UploadUserAvatar(HttpRequest request, DrxHttpServer server)
    {
        var (user, authError) = await Api.GetUserAsync(request);
        if (authError != null) return authError;

        if (request.UploadFile == null || request.UploadFile.Stream == null)
            return new JsonResult(new { message = "缺少上传的文件（multipart/form-data）" }, 400);

        var upload = request.UploadFile;
        var fileExt = Path.GetExtension(upload.FileName ?? string.Empty).ToLowerInvariant();
        if (fileExt == ".jpeg") fileExt = ".jpg";
        if (fileExt != ".png" && fileExt != ".jpg")
        {
            return new JsonResult(new { message = "仅支持 PNG / JPG 格式（文件扩展名应为 .png/.jpg）" }, 400);
        }

        if (upload.Stream.Length > 2 * 1024 * 1024) return new JsonResult(new { message = "文件过大，最大 2MB" }, 413);

        var iconsDir = Path.Combine(AppContext.BaseDirectory, "resources", "user", "icon");
        Directory.CreateDirectory(iconsDir);
        var finalPngPath = Path.Combine(iconsDir, $"{user!.Id}.png");

        // 统一将上传的图片转为 PNG 并保存为 {uid}.png；若存在旧的 jpg 文件则删除
        upload.Stream.Position = 0;
        try
        {
            using var img = Image.FromStream(upload.Stream, useEmbeddedColorManagement: true, validateImageData: true);
            // 使用高质量重采样以保证输出稳定
            using var bmp = new Bitmap(img.Width, img.Height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.DrawImage(img, 0, 0, img.Width, img.Height);
            }

            // 覆盖保存为 PNG
            bmp.Save(finalPngPath, ImageFormat.Png);

            // 删除可能残留的 JPG（避免同一用户存在多种扩展名）
            var legacyJpg = Path.Combine(iconsDir, $"{user!.Id}.jpg");
            if (File.Exists(legacyJpg))
            {
                try { File.Delete(legacyJpg); } catch { /* 忽略删除失败 */ }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"保存用户头像并转换为 PNG 失败: {ex.Message}");
            return new JsonResult(new { message = "保存头像失败（无效的图片或服务器错误）" }, 500);
        }

        // 清除该用户的头像缓存，使下次请求重新加载
        _avatarCache.InvalidateAvatar(user.Id);

        var url = $"/api/user/avatar/{user.Id}?v={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        Logger.Info($"用户 {user!.UserName} 成功上传了头像");
        return new JsonResult(new { message = "头像已上传", url = url });
    }

    #endregion
}

using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Drx.Sdk.Network.Email;
using Drx.Sdk.Network.Http;
using Drx.Sdk.Network.Http.Api;
using Drx.Sdk.Network.Http.Configs;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.Results;
using Drx.Sdk.Shared;
using Drx.Sdk.Shared.Utility;
using KaxSocket;
using KaxSocket.Handlers.Helpers;

namespace KaxSocket.Handlers;

/// <summary>
/// 忘记密码模块 — 实现邮件重置密码全链路（申请→验证→提交）。
/// </summary>
public partial class KaxHttp
{
    // ------------------------------------------------------------------
    // POST /api/user/password-reset/request
    // 申请密码重置，防枚举：外部统一返回受理文案
    // ------------------------------------------------------------------
    [HttpHandle("/api/user/password-reset/request", "POST",
        RateLimitMaxRequests = 5, RateLimitWindowSeconds = 60,
        RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_PasswordResetRequest(HttpRequest request, DrxHttpServer server)
    {
        string identifierMasked = "(unknown)";
        try
        {
            var bodyJson = request.Body != null ? JsonNode.Parse(request.Body) : null;
            if (bodyJson == null)
                return new JsonResult(new { code = 47001, message = "参数错误" }, 400);

            var identifier = bodyJson["identifier"]?.ToString()?.Trim();
            if (string.IsNullOrWhiteSpace(identifier))
                return new JsonResult(new { code = 47001, message = "请输入用户名或邮箱" }, 400);

            identifierMasked = identifier.Length > 3
                ? identifier[..2] + "***"
                : "***";

            var ip = request.ClientAddress.Ip ?? "unknown";
            var ua = request.Headers["User-Agent"] ?? request.Headers["user-agent"] ?? "unknown";

            // 查找用户（不对外暴露结果差异）
            UserData? targetUser = null;
            var allUsers = await KaxGlobal.UserDatabase.SelectAllAsync();

            if (Api.IsValidEmailFormat(identifier))
            {
                targetUser = allUsers.FirstOrDefault(u =>
                    !string.IsNullOrWhiteSpace(u.Email) &&
                    string.Equals(u.Email, identifier, StringComparison.OrdinalIgnoreCase));
            }

            if (targetUser == null)
            {
                targetUser = allUsers.FirstOrDefault(u =>
                    string.Equals(u.UserName, identifier, StringComparison.OrdinalIgnoreCase));
            }

            // 无论用户是否存在，外部统一返回受理文案
            if (targetUser == null || string.IsNullOrWhiteSpace(targetUser.Email))
            {
                Logger.Info($"[PasswordReset] 找回申请：账号不存在或无邮箱 identifier={identifierMasked} ip={ip}");
                // 统一受理文案，不泄露账号存在性
                return new JsonResult(new
                {
                    code = 0,
                    message = "如果账号存在且邮箱可用，我们已发送重置邮件，请在 10 分钟内完成重置",
                    data = new { cooldownSeconds = 30 }
                });
            }

            // 签发令牌
            var (rawToken, record, tokenError) = await KaxGlobal.IssuePasswordResetTokenAsync(targetUser.Id, ip, ua);
            if (tokenError != null)
            {
                if (tokenError.StartsWith("cooldown:"))
                {
                    var remaining = int.TryParse(tokenError.Split(':')[1], out var s) ? s : 30;
                    Logger.Info($"[PasswordReset] 冷却中: user={targetUser.UserName} remaining={remaining}s ip={ip}");
                    return new JsonResult(new { code = 47002, message = $"操作频繁，请 {remaining} 秒后再试" }, 429);
                }
                if (tokenError == "rate_limit_hour")
                {
                    Logger.Warn($"[PasswordReset] 小时上限: user={targetUser.UserName} ip={ip}");
                    return new JsonResult(new { code = 47002, message = "操作频繁，请稍后再试" }, 429);
                }
                if (tokenError == "rate_limit_day")
                {
                    Logger.Warn($"[PasswordReset] 日上限: user={targetUser.UserName} ip={ip}");
                    return new JsonResult(new { code = 47002, message = "操作频繁，请稍后再试" }, 429);
                }
                Logger.Error($"[PasswordReset] 签发令牌失败: user={targetUser.UserName} error={tokenError}");
                return new JsonResult(new { code = 0, message = "如果账号存在且邮箱可用，我们已发送重置邮件，请在 10 分钟内完成重置", data = new { cooldownSeconds = 30 } });
            }

            // 发送邮件
            var sent = await SendPasswordResetEmailAsync(request, server, targetUser, rawToken!);
            if (!sent)
            {
                Logger.Error($"[PasswordReset] 邮件发送失败: user={targetUser.UserName} email={Api.MaskEmail(targetUser.Email)}");
                // 邮件失败时仍返回统一文案（避免泄露信息），但内部记录错误
                return new JsonResult(new
                {
                    code = 0,
                    message = "如果账号存在且邮箱可用，我们已发送重置邮件，请在 10 分钟内完成重置",
                    data = new { cooldownSeconds = 30 }
                });
            }

            Logger.Info($"[PasswordReset] 邮件已发送: user={targetUser.UserName} email={Api.MaskEmail(targetUser.Email)} ip={ip}");
            return new JsonResult(new
            {
                code = 0,
                message = "如果账号存在且邮箱可用，我们已发送重置邮件，请在 10 分钟内完成重置",
                data = new { cooldownSeconds = 30 }
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"[PasswordReset] 申请异常: identifier={identifierMasked} error={ex.Message}");
            return new JsonResult(new { code = 0, message = "如果账号存在且邮箱可用，我们已发送重置邮件，请在 10 分钟内完成重置", data = new { cooldownSeconds = 30 } });
        }
    }

    // ------------------------------------------------------------------
    // POST /api/user/password-reset/validate
    // 预检令牌有效性（可选，重置页加载时调用）
    // ------------------------------------------------------------------
    [HttpHandle("/api/user/password-reset/validate", "POST",
        RateLimitMaxRequests = 20, RateLimitWindowSeconds = 60,
        RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_PasswordResetValidate(HttpRequest request)
    {
        try
        {
            var bodyJson = request.Body != null ? JsonNode.Parse(request.Body) : null;
            var token = bodyJson?["token"]?.ToString()?.Trim();

            if (string.IsNullOrWhiteSpace(token))
                return new JsonResult(new { code = 47003, message = "重置链接无效" }, 400);

            var (user, record, error) = await KaxGlobal.ValidatePasswordResetTokenAsync(token);
            if (error != null)
            {
                return error switch
                {
                    "token_expired" => new JsonResult(new { code = 47004, message = "重置链接已过期，请重新发起找回申请" }, 400),
                    "token_locked" => new JsonResult(new { code = 47006, message = "尝试次数过多，重置链接已锁定" }, 400),
                    _ => new JsonResult(new { code = 47003, message = "重置链接无效或已使用" }, 400)
                };
            }

            return new JsonResult(new
            {
                code = 0,
                message = "链接有效",
                data = new
                {
                    maskedEmail = Api.MaskEmail(user!.Email),
                    expiresAt = record!.ExpiresAt
                }
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"[PasswordReset] 预检异常: {ex.Message}");
            return new JsonResult(new { code = 47003, message = "重置链接无效" }, 400);
        }
    }

    // ------------------------------------------------------------------
    // POST /api/user/password-reset/confirm
    // 提交新密码并消费令牌
    // ------------------------------------------------------------------
    [HttpHandle("/api/user/password-reset/confirm", "POST",
        RateLimitMaxRequests = 10, RateLimitWindowSeconds = 60,
        RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_PasswordResetConfirm(HttpRequest request)
    {
        var ip = request.ClientAddress.Ip ?? "unknown";
        try
        {
            var bodyJson = request.Body != null ? JsonNode.Parse(request.Body) : null;
            if (bodyJson == null)
                return new JsonResult(new { code = 47001, message = "参数错误" }, 400);

            var token = bodyJson["token"]?.ToString()?.Trim();
            var newPassword = bodyJson["newPassword"]?.ToString();
            var confirmPassword = bodyJson["confirmPassword"]?.ToString();

            if (string.IsNullOrWhiteSpace(token))
                return new JsonResult(new { code = 47003, message = "重置链接无效" }, 400);

            if (string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
                return new JsonResult(new { code = 47001, message = "请输入新密码" }, 400);

            if (newPassword != confirmPassword)
                return new JsonResult(new { code = 47007, message = "两次输入的密码不一致" }, 400);

            if (newPassword.Length < 8)
                return new JsonResult(new { code = 47008, message = "密码长度不能少于 8 位" }, 400);

            // 计算密码哈希（保持与注册/登录一致的 SHA256）
            var newPasswordHash = CommonUtility.ComputeSHA256Hash(newPassword);

            var (success, error) = await KaxGlobal.ConsumePasswordResetTokenAsync(token, newPasswordHash);
            if (!success)
            {
                Logger.Warn($"[PasswordReset] 确认失败: error={error} ip={ip}");
                return error switch
                {
                    "token_expired" => new JsonResult(new { code = 47004, message = "重置链接已过期，请重新发起找回申请" }, 400),
                    "token_locked"  => new JsonResult(new { code = 47006, message = "尝试次数过多，重置链接已锁定" }, 400),
                    _               => new JsonResult(new { code = 47003, message = "重置链接无效或已使用" }, 400)
                };
            }

            Logger.Info($"[PasswordReset] 密码重置成功, ip={ip}");
            return new JsonResult(new
            {
                code = 0,
                message = "密码重置成功，请用新密码重新登录",
                data = new { requireRelogin = true }
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"[PasswordReset] 确认异常: {ex.Message} ip={ip}");
            return new JsonResult(new { code = 500, message = "服务器异常，请稍后重试" }, 500);
        }
    }

    // ------------------------------------------------------------------
    // 私有辅助：发送密码重置邮件
    // ------------------------------------------------------------------
    private static async Task<bool> SendPasswordResetEmailAsync(HttpRequest request, DrxHttpServer server, UserData user, string rawToken)
    {
        try
        {
          var smtpConfig = ResolveSmtpRuntimeConfig("PasswordReset");
          if (smtpConfig == null)
            {
                Logger.Error("[PasswordReset] SMTP 配置缺失，无法发送邮件");
                return false;
            }

            // 构建重置链接（取 Host 头，优先 HTTPS）
            var host = request.Headers["Host"] ?? "localhost:8462";
            var scheme = request.Headers["X-Forwarded-Proto"] ?? "http";
            var resetUrl = $"{scheme}://{host}/reset-password?token={Uri.EscapeDataString(rawToken)}";

            var displayName = user.DisplayName ?? user.UserName;
            var bodyHtml = $"""
<!DOCTYPE html>
<html lang="zh-CN">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>KaxHub 密码重置</title>
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
                    <span style="display:inline-block;padding:4px 12px;background:rgba(248,87,0,0.12);border:1px solid rgba(248,87,0,0.3);border-radius:20px;font-size:11px;color:#f85700;letter-spacing:0.5px;">密码重置</span>
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
                我们收到了您的密码重置申请，请点击下方按钮完成密码重置。如非本人操作，请忽略此邮件。
              </p>

              <!-- Reset Button -->
              <table width="100%" cellpadding="0" cellspacing="0" border="0" style="margin-bottom:28px;">
                <tr>
                  <td align="center">
                    <a href="{resetUrl}"
                       style="display:inline-block;padding:14px 36px;background:linear-gradient(135deg,#1f6feb,#388bfd);border-radius:8px;font-size:15px;font-weight:600;color:#ffffff;text-decoration:none;letter-spacing:0.3px;">
                      重置我的密码
                    </a>
                  </td>
                </tr>
              </table>

              <!-- Link Fallback -->
              <table width="100%" cellpadding="0" cellspacing="0" border="0" style="margin-bottom:24px;">
                <tr>
                  <td style="background:#0d1117;border:1px solid #30363d;border-radius:8px;padding:14px 16px;">
                    <p style="margin:0 0 6px;font-size:11px;color:#6e7681;text-transform:uppercase;letter-spacing:1px;">如按钮无法点击，请复制以下链接</p>
                    <p style="margin:0;font-size:12px;color:#58a6ff;word-break:break-all;">{resetUrl}</p>
                  </td>
                </tr>
              </table>

              <!-- Expiry Notice -->
              <table width="100%" cellpadding="0" cellspacing="0" border="0" style="margin-bottom:28px;">
                <tr>
                  <td style="background:rgba(248,166,0,0.08);border:1px solid rgba(248,166,0,0.2);border-left:3px solid #f8a600;border-radius:0 6px 6px 0;padding:12px 16px;">
                    <p style="margin:0;font-size:13px;color:#f8a600;">⏱ 重置链接有效期为 <strong>10 分钟</strong>，且只能使用一次，请尽快完成操作。</p>
                  </td>
                </tr>
              </table>

              <p style="margin:0;font-size:13px;color:#6e7681;line-height:1.7;">
                若非本人操作，您的账号仍然安全，请无视此邮件。
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
              SenderAddress = smtpConfig.SenderAddress,
              Password = smtpConfig.AuthCode,
                To = user.Email,
                Subject = "KaxHub · 密码重置",
                Body = bodyHtml,
              SmtpHost = smtpConfig.Host,
              SmtpPort = smtpConfig.Port,
              EnableSsl = smtpConfig.EnableSsl,
                ContentType = EmailContentType.Html
            };

            return await server.SendEmailAsync(cfg);
        }
        catch (Exception ex)
        {
            Logger.Error($"[PasswordReset] SendPasswordResetEmailAsync 异常: {ex.Message}");
            return false;
        }
    }
}

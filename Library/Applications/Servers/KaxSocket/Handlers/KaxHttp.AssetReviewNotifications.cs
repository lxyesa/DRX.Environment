using System;
using System.Net;
using System.Threading.Tasks;
using Drx.Sdk.Network.Email;
using Drx.Sdk.Shared;
using KaxSocket;
using KaxSocket.Handlers.Helpers;
using KaxSocket.Model;

namespace KaxSocket.Handlers;

/// <summary>
/// 资产审核/状态流转邮件通知。
/// </summary>
public partial class KaxHttp
{
    private static async Task NotifyAssetActionEmailAsync(AssetModel asset, string actionName, string? reason, string? operatorName)
    {
        try
        {
            var user = await KaxGlobal.UserDatabase.SelectByIdAsync(asset.AuthorId);
            if (user == null || string.IsNullOrWhiteSpace(user.Email))
            {
                Logger.Warn($"[AssetNotify] 跳过邮件：作者不存在或邮箱为空, assetId={asset.Id}, action={actionName}");
                return;
            }

            var smtpEmail = Environment.GetEnvironmentVariable("KAX_SMTP_EMAIL") ?? "157335596@qq.com";
            var smtpAuthCode = Environment.GetEnvironmentVariable("KAX_SMTP_AUTH_CODE") ?? "eymlrhwykskccbdb";
            var smtpHost = Environment.GetEnvironmentVariable("KAX_SMTP_HOST") ?? "smtp.qq.com";
            var smtpPort = int.TryParse(Environment.GetEnvironmentVariable("KAX_SMTP_PORT"), out var p) ? p : 587;
            var smtpEnableSsl = !bool.TryParse(Environment.GetEnvironmentVariable("KAX_SMTP_ENABLE_SSL"), out var ssl) || ssl;

            if (string.IsNullOrWhiteSpace(smtpEmail) || string.IsNullOrWhiteSpace(smtpAuthCode))
            {
                Logger.Warn($"[AssetNotify] SMTP 未配置，跳过邮件发送, assetId={asset.Id}, action={actionName}");
                return;
            }

            var displayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName;
            var reasonText = string.IsNullOrWhiteSpace(reason) ? "无" : reason.Trim();
            var normalizedOperatorName = string.IsNullOrWhiteSpace(operatorName) ? "系统" : operatorName.Trim();
            var subject = $"KaxHub · 资产{actionName}通知";
                        var displayNameSafe = WebUtility.HtmlEncode(displayName ?? string.Empty);
                        var assetNameSafe = WebUtility.HtmlEncode(asset.Name ?? string.Empty);
                        var actionNameSafe = WebUtility.HtmlEncode(actionName);
                        var reasonSafe = WebUtility.HtmlEncode(reasonText);
                        var operatorNameSafe = WebUtility.HtmlEncode(normalizedOperatorName);
                        var statusSafe = WebUtility.HtmlEncode(asset.Status.ToString());
                        var actionTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

                        var bodyHtml = $"""
<!DOCTYPE html>
<html lang="zh-CN">
<head>
    <meta charset="UTF-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>KaxHub 资产状态通知</title>
</head>
<body style="margin:0;padding:0;background:#0d1117;font-family:'Segoe UI',Arial,sans-serif;">
    <table width="100%" cellpadding="0" cellspacing="0" border="0" style="background:#0d1117;padding:40px 0;">
        <tr>
            <td align="center">
                <table width="560" cellpadding="0" cellspacing="0" border="0"
                             style="background:#161b22;border-radius:12px;border:1px solid #30363d;overflow:hidden;max-width:560px;width:100%;">

                    <tr>
                        <td style="background:linear-gradient(135deg,#1a2332 0%,#0d2137 100%);padding:32px 40px;border-bottom:1px solid #21262d;">
                            <table width="100%" cellpadding="0" cellspacing="0" border="0">
                                <tr>
                                    <td>
                                        <span style="font-size:22px;font-weight:700;color:#58a6ff;letter-spacing:1px;">Kax</span><span style="font-size:22px;font-weight:700;color:#e6edf3;letter-spacing:1px;">Hub</span>
                                    </td>
                                    <td align="right">
                                        <span style="display:inline-block;padding:4px 12px;background:rgba(248,87,0,0.12);border:1px solid rgba(248,87,0,0.3);border-radius:20px;font-size:11px;color:#f85700;letter-spacing:0.5px;">{actionNameSafe}</span>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <tr>
                        <td style="padding:40px 40px 32px;">
                            <p style="margin:0 0 8px;font-size:15px;color:#8b949e;">您好，</p>
                            <p style="margin:0 0 24px;font-size:20px;font-weight:600;color:#e6edf3;">{displayNameSafe}</p>

                            <p style="margin:0 0 22px;font-size:14px;color:#8b949e;line-height:1.7;">
                                您的资源状态发生了变更，请查看以下详情：
                            </p>

                            <table width="100%" cellpadding="0" cellspacing="0" border="0" style="margin-bottom:24px;background:#0d1117;border:1px solid #30363d;border-radius:8px;overflow:hidden;">
                                <tr>
                                    <td style="padding:12px 16px;border-bottom:1px solid #21262d;color:#6e7681;font-size:12px;width:110px;">资源名称</td>
                                    <td style="padding:12px 16px;border-bottom:1px solid #21262d;color:#e6edf3;font-size:13px;">{assetNameSafe}</td>
                                </tr>
                                <tr>
                                    <td style="padding:12px 16px;border-bottom:1px solid #21262d;color:#6e7681;font-size:12px;">资源ID</td>
                                    <td style="padding:12px 16px;border-bottom:1px solid #21262d;color:#e6edf3;font-size:13px;">{asset.Id}</td>
                                </tr>
                                <tr>
                                    <td style="padding:12px 16px;border-bottom:1px solid #21262d;color:#6e7681;font-size:12px;">当前状态</td>
                                    <td style="padding:12px 16px;border-bottom:1px solid #21262d;color:#e6edf3;font-size:13px;">{statusSafe}</td>
                                </tr>
                                <tr>
                                    <td style="padding:12px 16px;border-bottom:1px solid #21262d;color:#6e7681;font-size:12px;">操作类型</td>
                                    <td style="padding:12px 16px;border-bottom:1px solid #21262d;color:#58a6ff;font-size:13px;font-weight:600;">{actionNameSafe}</td>
                                </tr>
                                <tr>
                                    <td style="padding:12px 16px;border-bottom:1px solid #21262d;color:#6e7681;font-size:12px;">处理原因</td>
                                    <td style="padding:12px 16px;border-bottom:1px solid #21262d;color:#e6edf3;font-size:13px;line-height:1.6;">{reasonSafe}</td>
                                </tr>
                                <tr>
                                    <td style="padding:12px 16px;border-bottom:1px solid #21262d;color:#6e7681;font-size:12px;">处理人</td>
                                    <td style="padding:12px 16px;border-bottom:1px solid #21262d;color:#e6edf3;font-size:13px;">{operatorNameSafe}</td>
                                </tr>
                                <tr>
                                    <td style="padding:12px 16px;color:#6e7681;font-size:12px;">处理时间(UTC)</td>
                                    <td style="padding:12px 16px;color:#e6edf3;font-size:13px;">{actionTime}</td>
                                </tr>
                            </table>

                            <p style="margin:0;font-size:13px;color:#6e7681;line-height:1.7;">
                                如有疑问，请前往开发者中心查看资源详情。
                            </p>
                        </td>
                    </tr>

                    <tr>
                        <td style="padding:0 40px;"><div style="border-top:1px solid #21262d;"></div></td>
                    </tr>

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

            var sender = new SmtpEmailSender(new EmailSenderOptions
            {
                SenderAddress = smtpEmail,
                Password = smtpAuthCode,
                SmtpHost = smtpHost,
                SmtpPort = smtpPort,
                EnableSsl = smtpEnableSsl
            });

            var ok = await sender.TrySendAsync(EmailMessage.Create(user.Email, subject, bodyHtml, EmailContentType.Html));
            if (!ok)
            {
                Logger.Warn($"[AssetNotify] 邮件发送失败, assetId={asset.Id}, action={actionName}, to={Api.MaskEmail(user.Email)}");
                return;
            }

            Logger.Info($"[AssetNotify] 邮件发送成功, assetId={asset.Id}, action={actionName}, to={Api.MaskEmail(user.Email)}");
        }
        catch (Exception ex)
        {
            Logger.Error($"[AssetNotify] 邮件通知异常, assetId={asset.Id}, action={actionName}, error={ex.Message}");
        }
    }
}

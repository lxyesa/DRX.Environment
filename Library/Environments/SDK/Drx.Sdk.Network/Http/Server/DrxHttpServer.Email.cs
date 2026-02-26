using System;
using Drx.Sdk.Shared;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.Entry;
using Drx.Sdk.Network.Email;

namespace Drx.Sdk.Network.Http
{
    /// <summary>
    /// DrxHttpServer 邮件发送部分
    /// </summary>
    public partial class DrxHttpServer
    {
        /// <summary>
        /// 使用默认 QQ SMTP 发送简单文本邮件。
        /// </summary>
        public bool SendEmail(string to, string body, string senderAddress = "xxx@qq.com", string? authCode = null, string subject = "DRX Notification")
        {
            if (string.IsNullOrWhiteSpace(to)) throw new ArgumentNullException(nameof(to));
            if (string.IsNullOrWhiteSpace(senderAddress)) throw new ArgumentNullException(nameof(senderAddress));

            DRXEmail email = authCode == null ? new DRXEmail(senderAddress) : new DRXEmail(senderAddress, authCode);
            try
            {
                email.SendEmail(subject, body, to);
                return true;
            }
            catch (Exception ex)
            {
                try { Logger.Error($"SendEmail error: {ex}"); } catch { }
                return false;
            }
        }

        /// <summary>
        /// 使用完整的 EmailConfig 配置发送邮件。
        /// </summary>
        public bool SendEmail(EmailConfig cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (string.IsNullOrWhiteSpace(cfg.SenderAddress)) throw new ArgumentException("SenderAddress must be provided in EmailConfig", nameof(cfg));
            if (string.IsNullOrWhiteSpace(cfg.To)) throw new ArgumentException("To (recipient) must be provided in EmailConfig", nameof(cfg));

            var drx = new DRXEmail(cfg.SenderAddress, cfg.SmtpHost, cfg.Password, cfg.SmtpPort, cfg.EnableSsl, cfg.DisplayName);
            try
            {
                if (!string.IsNullOrEmpty(cfg.Body) && (cfg.Body.IndexOf("<html", StringComparison.OrdinalIgnoreCase) >= 0 || cfg.Body.IndexOf("<body", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    drx.SendHtmlEmail(cfg.Subject ?? string.Empty, cfg.Body, cfg.To);
                }
                else
                {
                    drx.SendEmail(cfg.Subject ?? string.Empty, cfg.Body ?? string.Empty, cfg.To);
                }
                return true;
            }
            catch (Exception ex)
            {
                try { Logger.Error($"SendEmail(config) error: {ex}"); } catch { }
                return false;
            }
        }
    }
}

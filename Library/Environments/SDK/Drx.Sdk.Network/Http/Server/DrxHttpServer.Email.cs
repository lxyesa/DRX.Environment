using System;
using Drx.Sdk.Shared;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.Entry;
using Drx.Sdk.Network.Email;
using System.Threading;
using System.Threading.Tasks;

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

            try
            {
                var sender = new SmtpEmailSender(new EmailSenderOptions
                {
                    SenderAddress = senderAddress,
                    Password = authCode ?? "umrroeavogwsdjci",
                    SmtpHost = "smtp.qq.com",
                    SmtpPort = 587,
                    EnableSsl = true
                });

                return sender.TrySendAsync(EmailMessage.Create(to, subject, body, EmailContentType.PlainText)).GetAwaiter().GetResult();
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

            try
            {
                var sender = new SmtpEmailSender(cfg.ToSenderOptions());
                return sender.TrySendAsync(cfg.ToMessage()).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                try { Logger.Error($"SendEmail(config) error: {ex}"); } catch { }
                return false;
            }
        }

        /// <summary>
        /// 使用默认 QQ SMTP 异步发送简单文本邮件。
        /// </summary>
        public Task<bool> SendEmailAsync(string to, string body, string senderAddress, string authCode, string subject = "DRX Notification", CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(to)) throw new ArgumentNullException(nameof(to));
            if (string.IsNullOrWhiteSpace(senderAddress)) throw new ArgumentNullException(nameof(senderAddress));
            if (string.IsNullOrWhiteSpace(authCode)) throw new ArgumentNullException(nameof(authCode));

            var sender = new SmtpEmailSender(new EmailSenderOptions
            {
                SenderAddress = senderAddress,
                Password = authCode,
                SmtpHost = "smtp.qq.com",
                SmtpPort = 587,
                EnableSsl = true
            });

            return sender.TrySendAsync(EmailMessage.Create(to, subject, body, EmailContentType.PlainText), cancellationToken);
        }

        /// <summary>
        /// 使用完整配置异步发送邮件。
        /// </summary>
        public Task<bool> SendEmailAsync(EmailConfig cfg, CancellationToken cancellationToken = default)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (string.IsNullOrWhiteSpace(cfg.SenderAddress)) throw new ArgumentException("SenderAddress must be provided in EmailConfig", nameof(cfg));
            if (string.IsNullOrWhiteSpace(cfg.To)) throw new ArgumentException("To (recipient) must be provided in EmailConfig", nameof(cfg));

            var sender = new SmtpEmailSender(cfg.ToSenderOptions());
            return sender.TrySendAsync(cfg.ToMessage(), cancellationToken);
        }
    }
}

using Drx.Sdk.Shared;
using Markdig;
using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.Email
{
    /// <summary>
    /// 基于 SMTP 的邮件发送器实现。
    /// </summary>
    public sealed class SmtpEmailSender : IEmailSender
    {
        private readonly EmailSenderOptions _options;

        public SmtpEmailSender(EmailSenderOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            ValidateOptions(_options);
        }

        public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (message.To.Count == 0) throw new ArgumentException("At least one recipient is required.", nameof(message));

            using var mail = BuildMailMessage(message);
            using var smtp = CreateSmtpClient();
            await smtp.SendMailAsync(mail, cancellationToken);
        }

        public async Task<bool> TrySendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            try
            {
                await SendAsync(message, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                try { Logger.Error($"Email send failed: {ex}"); } catch { }
                return false;
            }
        }

        private MailMessage BuildMailMessage(EmailMessage message)
        {
            var mail = new MailMessage
            {
                From = string.IsNullOrWhiteSpace(_options.DisplayName)
                    ? new MailAddress(_options.SenderAddress)
                    : new MailAddress(_options.SenderAddress, _options.DisplayName),
                Subject = message.Subject ?? string.Empty
            };

            foreach (var to in message.To.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                mail.To.Add(new MailAddress(to));
            }

            foreach (var cc in message.Cc.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                mail.CC.Add(new MailAddress(cc));
            }

            foreach (var bcc in message.Bcc.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                mail.Bcc.Add(new MailAddress(bcc));
            }

            var resolvedType = ResolveContentType(message.ContentType, message.Body);
            if (resolvedType == EmailContentType.Markdown)
            {
                mail.IsBodyHtml = true;
                mail.Body = BuildStyledMarkdownHtml(message.Body ?? string.Empty);
            }
            else
            {
                mail.IsBodyHtml = resolvedType == EmailContentType.Html;
                mail.Body = message.Body ?? string.Empty;
            }

            return mail;
        }

        private SmtpClient CreateSmtpClient()
        {
            return new SmtpClient(_options.SmtpHost)
            {
                Port = _options.SmtpPort,
                Credentials = new NetworkCredential(_options.SenderAddress, _options.Password),
                EnableSsl = _options.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };
        }

        private static EmailContentType ResolveContentType(EmailContentType contentType, string? body)
        {
            if (contentType != EmailContentType.Auto)
            {
                return contentType;
            }

            if (!string.IsNullOrWhiteSpace(body) &&
                (body.IndexOf("<html", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 body.IndexOf("<body", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 body.IndexOf("</", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return EmailContentType.Html;
            }

            return EmailContentType.PlainText;
        }

        private static string BuildStyledMarkdownHtml(string markdownBody)
        {
            var htmlBody = Markdown.ToHtml(markdownBody, new MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
            return $@"
            <html>
                <head>
                    <style>
                        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif;
                               line-height: 1.6;
                               padding: 20px;
                               max-width: 800px;
                               margin: auto; }}
                        code {{ background-color: #f6f8fa;
                               padding: 2px 4px;
                               border-radius: 3px; }}
                        pre {{ background-color: #f6f8fa;
                              padding: 16px;
                              border-radius: 6px; }}
                        img {{ max-width: 100%; }}
                    </style>
                </head>
                <body>
                    {htmlBody}
                </body>
            </html>";
        }

        private static void ValidateOptions(EmailSenderOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.SenderAddress)) throw new ArgumentException("SenderAddress is required.", nameof(options));
            if (string.IsNullOrWhiteSpace(options.Password)) throw new ArgumentException("Password is required.", nameof(options));
            if (string.IsNullOrWhiteSpace(options.SmtpHost)) throw new ArgumentException("SmtpHost is required.", nameof(options));
            if (options.SmtpPort <= 0) throw new ArgumentOutOfRangeException(nameof(options.SmtpPort), "SmtpPort must be greater than 0.");
        }
    }
}

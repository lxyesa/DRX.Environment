using Drx.Sdk.Shared;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.Email
{
    public class DRXEmail
    {
        private readonly EmailSenderOptions _options;
        private SmtpEmailSender _sender;

        /// <summary>
        /// 初始化 QQ 邮件客户端
        /// </summary>
        /// <param name="qqEmail">QQ邮箱地址 (xxx@qq.com)</param>
        /// <param name="authCode">QQ邮箱授权码</param>
        /// <param name="displayName">发件人显示名称</param>
        public DRXEmail(string qqEmail, string authCode = "umrroeavogwsdjci", string displayName = "")
        {
            _options = new EmailSenderOptions
            {
                SenderAddress = qqEmail,
                Password = authCode,
                SmtpHost = "smtp.qq.com",
                SmtpPort = 587,
                EnableSsl = true,
                DisplayName = displayName
            };
            _sender = new SmtpEmailSender(_options);
        }

        /// <summary>
        /// 自定义 SMTP 设置的构造函数
        /// </summary>
        public DRXEmail(string address, string stmpHost, string password, int stmpPort, bool enableSSL, string displayName = "")
        {
            _options = new EmailSenderOptions
            {
                SenderAddress = address,
                Password = password,
                SmtpHost = stmpHost,
                SmtpPort = stmpPort,
                EnableSsl = enableSSL,
                DisplayName = displayName
            };
            _sender = new SmtpEmailSender(_options);
        }

        /// <summary>
        /// 使用新的 SMTP 配置创建邮件客户端。
        /// </summary>
        public DRXEmail(EmailSenderOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _sender = new SmtpEmailSender(_options);
        }

        /// <summary>
        /// 设置发件人显示名称
        /// </summary>
        public void SetDisplayName(string displayName)
        {
            _options.DisplayName = displayName;
            _sender = new SmtpEmailSender(_options);
        }

        /// <summary>
        /// 向指定邮箱发送邮件
        /// </summary>
        public void SendEmail(string subject, string body, string to)
        {
            SendAsync(EmailMessage.Create(to, subject, body, EmailContentType.PlainText)).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 尝试发送邮件，并处理可能的异常
        /// </summary>
        public bool TrySendEmail(string subject, string body, string to)
        {
            try
            {
                SendEmail(subject, body, to);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                return false;
            }
        }


        /// <summary>
        /// 发送HTML格式的邮件
        /// </summary>
        /// <param name="subject">邮件主题</param>
        /// <param name="htmlBody">HTML格式的邮件内容</param>
        /// <param name="to">收件人地址</param>
        public void SendHtmlEmail(string subject, string htmlBody, string to)
        {
            SendAsync(EmailMessage.Create(to, subject, htmlBody, EmailContentType.Html)).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 尝试发送HTML格式的邮件，并处理可能的异常
        /// </summary>
        public bool TrySendHtmlEmail(string subject, string htmlBody, string to)
        {
            try
            {
                SendHtmlEmail(subject, htmlBody, to);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 发送Markdown格式的邮件（会自动转换为HTML）
        /// </summary>
        /// <param name="subject">邮件主题</param>
        /// <param name="markdownBody">Markdown格式的邮件内容</param>
        /// <param name="to">收件人地址</param>
        public void SendMarkdownEmail(string subject, string markdownBody, string to)
        {
            SendAsync(EmailMessage.Create(to, subject, markdownBody, EmailContentType.Markdown)).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 尝试发送Markdown格式的邮件，并处理可能的异常
        /// </summary>
        public bool TrySendMarkdownEmail(string subject, string markdownBody, string to)
        {
            try
            {
                SendMarkdownEmail(subject, markdownBody, to);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 现代化异步发送入口。
        /// </summary>
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            return _sender.SendAsync(message, cancellationToken);
        }

        /// <summary>
        /// 现代化异步尝试发送入口。
        /// </summary>
        public Task<bool> TrySendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            return _sender.TrySendAsync(message, cancellationToken);
        }
    }
} 
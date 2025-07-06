using DRX.Framework;
using Markdig;
using System.Net.Mail;

namespace Drx.Sdk.Network.Email
{
    public class DRXEmail
    {
        private string _address;
        private string _password;
        private string _stmpHost;
        private int _stmpPort = 587;
        private bool _enableSsl = true;
        private string _displayName;

        /// <summary>
        /// 初始化 QQ 邮件客户端
        /// </summary>
        /// <param name="qqEmail">QQ邮箱地址 (xxx@qq.com)</param>
        /// <param name="authCode">QQ邮箱授权码</param>
        /// <param name="displayName">发件人显示名称</param>
        public DRXEmail(string qqEmail, string authCode = "umrroeavogwsdjci", string displayName = "")
        {
            _address = qqEmail;
            _password = authCode; // QQ邮箱使用授权码而不是密码
            _stmpHost = "smtp.qq.com";
            _stmpPort = 587;
            _enableSsl = true;
            _displayName = displayName;
        }

        /// <summary>
        /// 自定义 SMTP 设置的构造函数
        /// </summary>
        public DRXEmail(string address, string stmpHost, string password, int stmpPort, bool enableSSL, string displayName = "")
        {
            _address = address;
            _password = password;
            _stmpHost = stmpHost;
            _stmpPort = stmpPort;
            _enableSsl = enableSSL;
            _displayName = displayName;
        }

        /// <summary>
        /// 设置发件人显示名称
        /// </summary>
        public void SetDisplayName(string displayName)
        {
            _displayName = displayName;
        }

        /// <summary>
        /// 向指定邮箱发送邮件
        /// </summary>
        public void SendEmail(string subject, string body, string to)
        {
            using var mail = new MailMessage();
            using var smtpServer = new SmtpClient(_stmpHost);

            // 使用显示名称创建发件人地址
            mail.From = string.IsNullOrEmpty(_displayName)
                ? new MailAddress(_address)
                : new MailAddress(_address, _displayName);

            mail.To.Add(new MailAddress(to));
            mail.Subject = subject;
            mail.Body = body;
            mail.IsBodyHtml = false;

            smtpServer.Port = _stmpPort;
            smtpServer.Credentials = new System.Net.NetworkCredential(_address, _password);
            smtpServer.EnableSsl = _enableSsl;
            smtpServer.DeliveryMethod = SmtpDeliveryMethod.Network;

            smtpServer.Send(mail);
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
            using var mail = new MailMessage();
            using var smtpServer = new SmtpClient(_stmpHost);

            mail.From = string.IsNullOrEmpty(_displayName)
                ? new MailAddress(_address)
                : new MailAddress(_address, _displayName);

            mail.To.Add(new MailAddress(to));
            mail.Subject = subject;
            mail.Body = htmlBody;
            mail.IsBodyHtml = true;  // 启用HTML格式

            smtpServer.Port = _stmpPort;
            smtpServer.Credentials = new System.Net.NetworkCredential(_address, _password);
            smtpServer.EnableSsl = _enableSsl;
            smtpServer.DeliveryMethod = SmtpDeliveryMethod.Network;

            smtpServer.Send(mail);
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
            // 将Markdown转换为HTML
            var htmlBody = Markdown.ToHtml(markdownBody, new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build());

            // 添加基本的HTML样式
            var styledHtml = $@"
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

            SendHtmlEmail(subject, styledHtml, to);
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
    }
} 
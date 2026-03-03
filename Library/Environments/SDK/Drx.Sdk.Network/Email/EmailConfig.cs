using System;

namespace Drx.Sdk.Network.Email
{
    /// <summary>
    /// 邮件发送配置类，用于自定义 SMTP 设置
    /// </summary>
    public class EmailConfig
    {
        /// <summary>
        /// 发件人地址（例如：xxx@qq.com）。如果为空，调用方应提供一个非空值。
        /// </summary>
        public string SenderAddress { get; set; } = string.Empty;

        /// <summary>
        /// 发件人授权码或密码
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// 收件人地址
        /// </summary>
        public string To { get; set; } = string.Empty;

        /// <summary>
        /// 邮件主题
        /// </summary>
        public string Subject { get; set; } = "DRX Notification";

        /// <summary>
        /// 邮件正文（支持 HTML）
        /// </summary>
        public string Body { get; set; } = string.Empty;

        /// <summary>
        /// SMTP 主机，例如 "smtp.qq.com"
        /// </summary>
        public string SmtpHost { get; set; } = "smtp.qq.com";

        /// <summary>
        /// SMTP 端口，例如 587
        /// </summary>
        public int SmtpPort { get; set; } = 587;

        /// <summary>
        /// 是否启用 SSL/TLS
        /// </summary>
        public bool EnableSsl { get; set; } = true;

        /// <summary>
        /// 发件人显示名称（可选）
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 正文类型，默认自动识别（兼容旧逻辑）。
        /// </summary>
        public EmailContentType ContentType { get; set; } = EmailContentType.Auto;

        /// <summary>
        /// 转换为 SMTP 发送配置。
        /// </summary>
        public EmailSenderOptions ToSenderOptions()
        {
            return new EmailSenderOptions
            {
                SenderAddress = SenderAddress,
                Password = Password,
                SmtpHost = SmtpHost,
                SmtpPort = SmtpPort,
                EnableSsl = EnableSsl,
                DisplayName = DisplayName
            };
        }

        /// <summary>
        /// 转换为邮件消息模型。
        /// </summary>
        public EmailMessage ToMessage()
        {
            var message = new EmailMessage
            {
                Subject = Subject ?? string.Empty,
                Body = Body ?? string.Empty,
                ContentType = ContentType
            };

            if (!string.IsNullOrWhiteSpace(To))
            {
                message.To.Add(To);
            }

            return message;
        }
    }
}

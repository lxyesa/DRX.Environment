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
    }
}

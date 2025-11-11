using System;
using Drx.Sdk.Network.V2.Socket;

namespace Drx.Sdk.Network.Email
{
    /// <summary>
    /// 为 NetworkServer 提供的邮件发送扩展方法
    /// </summary>
    public static class NetworkServerEmailExtensions
    {
        /// <summary>
        /// 使用默认 QQ SMTP（smtp.qq.com）发送简单文本邮件。
        /// 参数说明：to - 收件人地址；body - 邮件正文；senderAddress - 发件人地址（默认为 "xxx@qq.com"，请替换为真实地址）；authCode - 发件人授权码（默认为 DRXEmail 类中的默认授权码）。
        /// </summary>
        /// <param name="server">调用实例（仅作扩展，不使用内部状态）</param>
        /// <param name="to">收件人地址</param>
        /// <param name="body">邮件正文（作为 plain/text 发送）</param>
        /// <param name="senderAddress">发件人地址（可选，默认 xxx@qq.com）</param>
        /// <param name="authCode">发件人授权码（可选，默认 DRXEmail 的内置值）</param>
        /// <param name="subject">邮件主题（可选）</param>
        public static bool SendEmail(this NetworkServer server, string to, string body, string senderAddress = "xxx@qq.com", string authCode = null, string subject = "DRX Notification")
        {
            // 保守的参数校验
            if (string.IsNullOrWhiteSpace(to)) throw new ArgumentNullException(nameof(to));
            if (string.IsNullOrWhiteSpace(senderAddress)) throw new ArgumentNullException(nameof(senderAddress));

            // 使用 DRXEmail 发送
            var email = authCode == null ? new DRXEmail(senderAddress) : new DRXEmail(senderAddress, authCode);
            try
            {
                email.SendEmail(subject, body, to);
                return true;
            }
            catch (Exception ex)
            {
                // 将错误记录到框架日志（如果 Logger 可用），这里简单写入 Debug
                try { System.Diagnostics.Debug.WriteLine($"SendEmail error: {ex}"); } catch { }
                return false;
            }
        }

        /// <summary>
        /// 使用完整的 EmailConfig 配置发送邮件（支持自定义 SMTP、HTML 内容等）。
        /// </summary>
        /// <param name="server">调用实例（仅作扩展）</param>
        /// <param name="cfg">完整的邮件配置</param>
        public static bool SendEmail(this NetworkServer server, EmailConfig cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (string.IsNullOrWhiteSpace(cfg.SenderAddress)) throw new ArgumentException("SenderAddress must be provided in EmailConfig", nameof(cfg));
            if (string.IsNullOrWhiteSpace(cfg.To)) throw new ArgumentException("To (recipient) must be provided in EmailConfig", nameof(cfg));

            // 根据配置构造 DRXEmail（自定义 SMTP）
            var drx = new DRXEmail(cfg.SenderAddress, cfg.SmtpHost, cfg.Password, cfg.SmtpPort, cfg.EnableSsl, cfg.DisplayName);
            try
            {
                // 如果传入的是 HTML 内容（例如包含 <html>），则使用 SendHtmlEmail，否则当作普通文本
                if (!string.IsNullOrEmpty(cfg.Body) && (cfg.Body.Contains("<html", StringComparison.OrdinalIgnoreCase) || cfg.Body.Contains("<body", StringComparison.OrdinalIgnoreCase)))
                {
                    drx.SendHtmlEmail(cfg.Subject ?? "", cfg.Body, cfg.To);
                }
                else
                {
                    // 对于纯文本，调用 SendEmail
                    drx.SendEmail(cfg.Subject ?? "", cfg.Body ?? string.Empty, cfg.To);
                }

                return true;
            }
            catch (Exception ex)
            {
                try { System.Diagnostics.Debug.WriteLine($"SendEmail(config) error: {ex}"); } catch { }
                return false;
            }
        }
    }
}

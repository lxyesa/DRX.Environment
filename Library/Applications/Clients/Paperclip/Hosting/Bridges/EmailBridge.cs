// Copyright (c) DRX SDK — Paperclip Email 脚本桥接层
// 职责：将 SMTP 邮件发送能力导出到 JS/TS 脚本
// 关键依赖：Drx.Sdk.Network.Email.*

using System;
using System.Threading.Tasks;
using Drx.Sdk.Network.Email;

namespace DrxPaperclip.Hosting;

/// <summary>
/// 邮件发送脚本桥接层。脚本可通过 <c>Email.createSender(...)</c> 创建发送器，
/// 再通过 <c>Email.send(...)</c> 发送邮件。
/// </summary>
public static class EmailBridge
{
    /// <summary>
    /// 创建 SMTP 邮件发送器。
    /// </summary>
    /// <param name="senderAddress">发件人邮箱。</param>
    /// <param name="password">SMTP 密码/授权码。</param>
    /// <param name="smtpHost">SMTP 主机，默认 smtp.qq.com。</param>
    /// <param name="smtpPort">SMTP 端口，默认 587。</param>
    /// <param name="enableSsl">是否启用 SSL，默认 true。</param>
    /// <param name="displayName">发件人显示名称。</param>
    public static SmtpEmailSender createSender(
        string senderAddress,
        string password,
        string smtpHost = "smtp.qq.com",
        int smtpPort = 587,
        bool enableSsl = true,
        string displayName = "")
    {
        return new SmtpEmailSender(new EmailSenderOptions
        {
            SenderAddress = senderAddress,
            Password = password,
            SmtpHost = smtpHost,
            SmtpPort = smtpPort,
            EnableSsl = enableSsl,
            DisplayName = displayName
        });
    }

    /// <summary>
    /// 发送纯文本邮件。
    /// </summary>
    public static Task send(SmtpEmailSender sender, string to, string subject, string body)
    {
        ArgumentNullException.ThrowIfNull(sender);
        var msg = EmailMessage.Create(to, subject, body, EmailContentType.PlainText);
        return sender.SendAsync(msg);
    }

    /// <summary>
    /// 发送 HTML 邮件。
    /// </summary>
    public static Task sendHtml(SmtpEmailSender sender, string to, string subject, string htmlBody)
    {
        ArgumentNullException.ThrowIfNull(sender);
        var msg = EmailMessage.Create(to, subject, htmlBody, EmailContentType.Html);
        return sender.SendAsync(msg);
    }

    /// <summary>
    /// 发送 Markdown 邮件（服务端自动转 HTML）。
    /// </summary>
    public static Task sendMarkdown(SmtpEmailSender sender, string to, string subject, string markdownBody)
    {
        ArgumentNullException.ThrowIfNull(sender);
        var msg = EmailMessage.Create(to, subject, markdownBody, EmailContentType.Markdown);
        return sender.SendAsync(msg);
    }

    /// <summary>
    /// 安全发送（失败不抛异常，返回 bool）。
    /// </summary>
    public static Task<bool> trySend(SmtpEmailSender sender, string to, string subject, string body)
    {
        ArgumentNullException.ThrowIfNull(sender);
        var msg = EmailMessage.Create(to, subject, body, EmailContentType.PlainText);
        return sender.TrySendAsync(msg);
    }
}

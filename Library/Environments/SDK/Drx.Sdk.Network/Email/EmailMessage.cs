using System.Collections.Generic;

namespace Drx.Sdk.Network.Email
{
    /// <summary>
    /// 邮件消息模型。
    /// </summary>
    public sealed class EmailMessage
    {
        public List<string> To { get; } = new();

        public List<string> Cc { get; } = new();

        public List<string> Bcc { get; } = new();

        public string Subject { get; set; } = string.Empty;

        public string Body { get; set; } = string.Empty;

        public EmailContentType ContentType { get; set; } = EmailContentType.PlainText;

        public static EmailMessage Create(string to, string subject, string body, EmailContentType contentType = EmailContentType.PlainText)
        {
            var message = new EmailMessage
            {
                Subject = subject,
                Body = body,
                ContentType = contentType
            };
            message.To.Add(to);
            return message;
        }
    }
}

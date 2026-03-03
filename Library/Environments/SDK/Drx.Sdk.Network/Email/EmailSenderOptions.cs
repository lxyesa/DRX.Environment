namespace Drx.Sdk.Network.Email
{
    /// <summary>
    /// SMTP 邮件发送配置。
    /// </summary>
    public sealed class EmailSenderOptions
    {
        public string SenderAddress { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string SmtpHost { get; set; } = "smtp.qq.com";

        public int SmtpPort { get; set; } = 587;

        public bool EnableSsl { get; set; } = true;

        public string DisplayName { get; set; } = string.Empty;
    }
}

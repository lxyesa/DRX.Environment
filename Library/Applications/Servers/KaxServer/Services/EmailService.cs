using Drx.Sdk.Network.Email;

namespace KaxServer.Services
{
    public class EmailService
    {
        private readonly DRXEmail _emailClient;
        private readonly EmailVerificationCode _verificationCode;

        public EmailService(string senderEmail, string authCode, EmailVerificationCode verificationCode)
        {
            _emailClient = new DRXEmail(senderEmail, authCode, "KaxServer");
            _verificationCode = verificationCode;
        }

        public bool SendVerificationEmail(string toEmail)
        {
            if (!_verificationCode.CanSendCode(toEmail))
            {
                return false;
            }

            string code = _verificationCode.GenerateCode();
            _verificationCode.SaveCode(toEmail, code);

            string subject = "KaxServer 邮箱验证码";
            string markdownBody = $@"
# KaxServer 邮箱验证

您好！

您的验证码是：**{code}**

请在 3 分钟内完成验证。

注意事项：
- 验证码有效期为 3 分钟
- 如果不是您本人操作，请忽略此邮件
- 请勿将验证码泄露给他人
- 1分钟内只能请求一次验证码

此致，
KaxServer 团队
";

            return _emailClient.TrySendMarkdownEmail(subject, markdownBody, toEmail);
        }
    }
}
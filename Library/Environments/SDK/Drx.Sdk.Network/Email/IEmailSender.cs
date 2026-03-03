using System.Threading;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.Email
{
    /// <summary>
    /// 邮件发送器抽象。
    /// </summary>
    public interface IEmailSender
    {
        Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);

        Task<bool> TrySendAsync(EmailMessage message, CancellationToken cancellationToken = default);
    }
}

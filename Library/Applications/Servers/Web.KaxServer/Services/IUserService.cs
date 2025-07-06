using Drx.Sdk.Network.Services;
using Web.KaxServer.Models;

namespace Web.KaxServer.Services
{
    public interface IUserService : IDrxService
    {
        /// <summary>
        /// 通过用户ID获取用户信息
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns>用户会话信息，如果不存在则返回null</returns>
        UserSession? GetUserById(int userId);

        /// <summary>
        /// 通过用户ID获取用户名
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <returns>用户名，如果不存在则返回"未知用户"</returns>
        string GetUsernameById(int userId);

        /// <summary>
        /// 获取系统中的用户总数
        /// </summary>
        /// <returns>用户总数</returns>
        int GetTotalUsersCount();

        /// <summary>
        /// 对用户进行身份验证。
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码</param>
        /// <returns>如果凭据有效，则返回用户会话；否则返回null。</returns>
        UserSession? AuthenticateUser(string username, string password);
    }
} 
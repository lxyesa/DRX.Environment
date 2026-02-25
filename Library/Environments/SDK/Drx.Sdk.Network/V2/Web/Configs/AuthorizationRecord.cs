using System;

namespace Drx.Sdk.Network.V2.Web.Configs
{
    /// <summary>
    /// 授权记录类，用于存储OAuth授权信息
    /// </summary>
    public class AuthorizationRecord
    {
        /// <summary>
        /// 授权码，唯一标识
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// 授权用户名
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 应用名称（用于展示）
        /// </summary>
        public string ApplicationName { get; set; }

        /// <summary>
        /// 应用描述
        /// </summary>
        public string ApplicationDescription { get; set; }

        /// <summary>
        /// 授权请求时间
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 授权超时时间（分钟）
        /// </summary>
        public int ExpirationMinutes { get; set; }

        /// <summary>
        /// 是否已授权完成
        /// </summary>
        public bool IsAuthorized { get; set; }

        /// <summary>
        /// 授权完成时间
        /// </summary>
        public DateTime? AuthorizedAt { get; set; }

        /// <summary>
        /// 授权范围/权限列表（逗号分隔）
        /// </summary>
        public string Scopes { get; set; }

        public AuthorizationRecord(string code, string userName, string applicationName, string applicationDescription = "", int expirationMinutes = 5, string scopes = "")
        {
            Code = code;
            UserName = userName;
            ApplicationName = applicationName;
            ApplicationDescription = applicationDescription;
            CreatedAt = DateTime.UtcNow;
            ExpirationMinutes = expirationMinutes;
            IsAuthorized = false;
            AuthorizedAt = null;
            Scopes = scopes;
        }

        /// <summary>
        /// 检查授权码是否已过期
        /// </summary>
        public bool IsExpired => (DateTime.UtcNow - CreatedAt).TotalMinutes > ExpirationMinutes;
    }
}

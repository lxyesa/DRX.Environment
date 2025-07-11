using Drx.Sdk.Network.DataBase;
using Drx.Sdk.Network.Email;
using Drx.Sdk.Network.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Web.KaxServer.Models
{

    public class UserOwnedAsset : IDataBase
    {
        public int UserId { get; set; }
        public int AssetId { get; set; }
        public DateTime PurchaseDate { get; set; }
        public bool IsActive { get; set; } = true;
        public int Id { get; set; }
    }

    public class McaCode : IDataBase
    {
        public int UserId { get; set; }
        public int AssetId { get; set; }       // 机械码关联的资产id
        public string CodeValue { get; set; }  // 机械码
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int Id { get; set; }
    }

    public class ClientToken : IDataBase
    {
        public int UserId { get; set; }
        public int AssetId { get; set; }    // 客户端token关联的资产id
        public string Token { get; set; }    // 客户端token
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int Id { get; set; }
    }

    public class ClientOnline : IDataBase
    {
        public int UserId { get; set; }
        public int AssetId { get; set; }    // 客户端在线关联的资产id
        public bool IsOnline { get; set; } = false;     // 客户端是否在线
        public DateTime LastOnlineTime { get; set; } = DateTime.MinValue;
        public int Id { get; set; }
    }

    /// <summary>
    /// Represents the complete persisted data for a user.
    /// This class is designed to be serialized and deserialized by the XmlDatabase system.
    /// </summary>
    public class UserData : IDataBase
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public UserPermissionType UserPermission { get; set; }
        public decimal Coins { get; set; }

        [SqliteRelation("UserAssets", "UserId")]
        public List<UserOwnedAsset> OwnedAssets { get; set; } = new();

        [SqliteRelation("McaCodes", "UserId")]
        public List<McaCode> McaCodes { get; set; } = new();

        [SqliteRelation("ClientTokens", "UserId")]
        public List<ClientToken> ClientTokens { get; set; } = new();

        [SqliteRelation("PublishedAssets", "UserId")]
        public List<int> PublishedAssetIds { get; set; } = new List<int>();

        public string AvatarUrl { get; set; }

        [SqliteIgnore]
        public string SessionId { get; set; }
        public DateTime CreatedAt { get; set; }

        [SqliteRelation("ClientOnline", "UserId")]
        public List<ClientOnline> ClientOnline { get; set; } = new();

        public bool Banned { get; set; } = false;
        public DateTime BanEndTime { get; set; } = DateTime.MinValue;


        int IDataBase.Id { get => UserId; set => UserId = value; }


        public bool IsBanned()
        {
            if (Banned)
            {
                if (BanEndTime > DateTime.Now)
                {
                    return true;
                }
                else
                {
                    Banned = false;
                    BanEndTime = DateTime.MinValue;
                }
            }
            return false;
        }

        public void Ban(int minutes)
        {
            if (minutes == -1)
            {
                // 永久封禁
                minutes = 60 * 24 * 365 * 1000;
            }

            var emailSender = new DRXEmail("drxhelp@qq.com", "kyzernkjwlsicifb", "Kax辅助平台");
            string emailBody = GenerateBanNotificationEmail(Username, BanEndTime);
            emailSender.TrySendHtmlEmail("KAX游戏辅助平台 - 封禁通知", emailBody, Email);

            Banned = true;
            BanEndTime = DateTime.Now.AddMinutes(minutes);
        }

        private string GenerateBanNotificationEmail(string username, DateTime banEndTime, string? reason = null)
        {
            bool isPermanent = banEndTime > DateTime.Now.AddYears(10);
            string banDuration = isPermanent ? "永久封禁" : $"封禁至 {banEndTime:yyyy-MM-dd HH:mm}";
            string reasonText = string.IsNullOrEmpty(reason) ? "违反用户协议或社区规则" : reason;

            string emailBody = $@"
<!DOCTYPE html>
<html lang='zh-CN'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>账号封禁通知</title>
    <style>
        :root {{
            --bg-color: #fafafa;
            --text-color: #1a1a1a;
            --text-color-light: #ffffff;
            --muted-color: #6b7280;
            --border-color: #e2e8f0;
            --accent-color: #111827;
            --accent-hover: #374151;
            --primary-color: #3b82f6;
            --card-shadow: 0 10px 25px rgba(0, 0, 0, 0.05);
        }}

        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}

        body {{
            background-color: #f3f4f6;
            font-family: 'Segoe UI', system-ui, -apple-system, sans-serif;
            line-height: 1.6;
            color: var(--text-color);
        }}

        .email-container {{
            max-width: 600px;
            margin: 0 auto;
            background-color: var(--bg-color);
            border-radius: 12px;
            overflow: hidden;
            box-shadow: var(--card-shadow);
            border: 1px solid var(--border-color);
        }}

        .email-header {{
            background-color: var(--accent-color);
            padding: 2rem;
            text-align: center;
            border-bottom: 1px solid var(--border-color);
        }}

        .logo {{
            font-size: 2rem;
            font-weight: 800;
            color: var(--text-color-light);
            text-decoration: none;
            letter-spacing: -0.5px;
            position: relative;
            display: inline-block;
        }}

        .logo::after {{
            content: '';
            position: absolute;
            width: 6px;
            height: 6px;
            border-radius: 50%;
            background-color: var(--text-color-light);
            bottom: 5px;
            right: -8px;
        }}

        .email-body {{
            padding: 2.5rem;
        }}

        .email-title {{
            font-size: 1.5rem;
            font-weight: 700;
            margin-bottom: 1.5rem;
            color: #dc2626;
        }}

        .email-content {{
            margin-bottom: 2rem;
        }}

        .email-content p {{
            margin-bottom: 1rem;
        }}

        .ban-info {{
            background-color: #fee2e2;
            border: 1px solid #fecaca;
            border-radius: 8px;
            padding: 1.5rem;
            margin-bottom: 2rem;
        }}

        .ban-info-item {{
            display: flex;
            margin-bottom: 0.75rem;
        }}

        .ban-info-label {{
            font-weight: 600;
            width: 100px;
            flex-shrink: 0;
        }}

        .ban-info-value {{
            color: var(--text-color);
        }}

        .ban-reason {{
            font-style: italic;
            color: #dc2626;
        }}

        .contact-section {{
            background-color: #f3f4f6;
            padding: 1.5rem;
            border-radius: 8px;
            margin-bottom: 2rem;
        }}

        .email-footer {{
            background-color: #f3f4f6;
            padding: 1.5rem;
            text-align: center;
            color: var(--muted-color);
            font-size: 0.9rem;
            border-top: 1px solid var(--border-color);
        }}

        .footer-links {{
            display: flex;
            justify-content: center;
            gap: 1rem;
            margin-bottom: 1rem;
        }}

        .footer-link {{
            color: var(--accent-color);
            text-decoration: none;
        }}

        .footer-link:hover {{
            text-decoration: underline;
        }}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='email-header'>
            <div class='logo'>KAX</div>
        </div>
        
        <div class='email-body'>
            <h1 class='email-title'>账号封禁通知</h1>
            
            <div class='email-content'>
                <p>尊敬的 <strong>{username}</strong>：</p>
                <p>我们遗憾地通知您，您的KAX账号已被封禁。根据我们的系统记录和社区规则审核，您的账号行为违反了我们的用户协议。</p>
            </div>
            
            <div class='ban-info'>
                <div class='ban-info-item'>
                    <div class='ban-info-label'>用户名：</div>
                    <div class='ban-info-value'>{username}</div>
                </div>
                <div class='ban-info-item'>
                    <div class='ban-info-label'>封禁状态：</div>
                    <div class='ban-info-value'>{banDuration}</div>
                </div>
                <div class='ban-info-item'>
                    <div class='ban-info-label'>封禁原因：</div>
                    <div class='ban-info-value ban-reason'>{reasonText}</div>
                </div>
            </div>
            
            <div class='email-content'>
                <p>在封禁期间，您将无法登录KAX平台或使用任何相关服务。如果您认为这是一个错误，或者需要进一步解释，请通过以下方式联系我们的支持团队。</p>
            </div>
            
            <div class='contact-section'>
                <p><strong>联系我们的客服团队：</strong></p>
                <p>邮箱：support@kax-game.com</p>
                <p>工作时间：周一至周五 9:00-18:00</p>
            </div>
            
            <div class='email-content'>
                <p>感谢您的理解与配合。</p>
                <p>KAX游戏辅助平台团队</p>
            </div>
        </div>
        
        <div class='email-footer'>
            <div class='footer-links'>
                <a href='#' class='footer-link'>使用条款</a>
                <a href='#' class='footer-link'>隐私政策</a>
                <a href='#' class='footer-link'>帮助中心</a>
            </div>
            <p>© 2023 KAX 游戏辅助平台 版权所有</p>
            <p>此邮件为系统自动发送，请勿直接回复</p>
        </div>
    </div>
</body>
</html>
";
            return emailBody;
        }
    }
}
using System;
using Drx.Sdk.Network.Session;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Web.KaxServer.Models
{
    public class UserSession : BaseSession
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string UserToken { get; set; }
        public UserPermissionType UserPermission { get; set; }
        public decimal Coins { get; set; } // 用户金币
        public List<UserOwnedAsset> OwnedAssets { get; set; } // 用户拥有的资产列表，键为资产ID，值为过期时间
        public List<McaCode> McaCodes { get; set; } // 用户的MCA码列表，键为资产ID，值为MCA码
        public List<int> PublishedAssetIds { get; set; } = new List<int>(); // 用户发布的资产ID列表
        public string AvatarUrl { get; set; }   /* 用户头像URL */

        public UserData UserData { get; set; }

        public UserSession()
            : base("UserAuth", 604800)
        {
            OwnedAssets = new List<UserOwnedAsset>();
            McaCodes = new List<McaCode>();
        }

        /// <summary>
        /// Creates a user session from a UserData object.
        /// </summary>
        /// <param name="userData">The user's persisted data.</param>
        public UserSession(UserData userData)
            : base("UserAuth", 604800)
        {
            UserId = userData.UserId;
            Username = userData.Username;
            Email = userData.Email;
            UserPermission = userData.UserPermission;
            Coins = userData.Coins;
            OwnedAssets = userData.OwnedAssets ?? new Dictionary<int, DateTime>();
            McaCodes = userData.McaCodes ?? new Dictionary<int, string>();
            PublishedAssetIds = userData.PublishedAssetIds ?? new List<int>();
            AvatarUrl = userData.AvatarUrl;

            // Generate a new user token for this session
            UserToken = GenerateUserToken(Username, Email);
            UserData = userData;
        }

        public UserSession(string username, string email) 
            : base("UserAuth", 604800) // 7天过期 (7 * 24 * 60 * 60 = 604800秒)
        {
            Username = username;
            Email = email;
            // 默认权限和等级
            UserPermission = UserPermissionType.Normal;
            Coins = 1.0m;
            // 生成用户token，基于用户名和邮箱
            UserToken = GenerateUserToken(username, email);
            OwnedAssets = new Dictionary<int, DateTime>(); // 初始化为空字典
            McaCodes = new Dictionary<int, string>(); // 初始化为空字典
        }

        public UserSession(string username, string email, UserPermissionType permission, decimal level) 
            : base("UserAuth", 604800) // 7天过期 (7 * 24 * 60 * 60 = 604800秒)
        {
            Username = username;
            Email = email;
            UserPermission = permission;
            Coins = level;
            // 生成用户token，基于用户名和邮箱
            UserToken = GenerateUserToken(username, email);
            OwnedAssets = new Dictionary<int, DateTime>(); // 初始化为空字典
            McaCodes = new Dictionary<int, string>(); // 初始化为空字典
        }
        

        /// <summary>
        /// 检查用户是否拥有有效的（未过期的）资产
        /// </summary>
        /// <param name="assetId">资产ID</param>
        /// <returns>如果拥有且未过期，返回true</returns>
        public bool HasValidAsset(int assetId)
        {
            if (OwnedAssets.TryGetValue(assetId, out DateTime expirationDate))
            {
                return DateTime.Now < expirationDate;
            }
            return false;
        }

        /// <summary>
        /// 为用户添加或续期资产
        /// </summary>
        /// <param name="assetId">资产ID</param>
        /// <param name="monthsValid">有效月数</param>
        public void AddOrUpdateAsset(int assetId, int monthsValid = 1)
        {
            // 如果用户已拥有该资产，则视为续期，在当前过期时间上累加
            // 否则，从现在开始计算新的过期时间
            DateTime currentExpiration = OwnedAssets.ContainsKey(assetId) ? OwnedAssets[assetId] : DateTime.Now;
            
            // 确保过期的订阅从现在开始续订，而不是从过去的日期
            if (currentExpiration < DateTime.Now)
            {
                currentExpiration = DateTime.Now;
            }

            OwnedAssets[assetId] = currentExpiration.AddMonths(monthsValid);
        }

        /// <summary>
        /// 为用户添加或续期资产，使用灵活的时间单位
        /// </summary>
        /// <param name="assetId">资产ID</param>
        /// <param name="value">时长</param>
        /// <param name="unit">时间单位</param>
        public void AddOrUpdateAsset(int assetId, int value, DurationUnit unit)
        {
            DateTime currentExpiration = OwnedAssets.TryGetValue(assetId, out var existingExpiry) && existingExpiry > DateTime.Now 
                ? existingExpiry 
                : DateTime.Now;

            DateTime newExpiration = unit switch
            {
                DurationUnit.Minute => currentExpiration.AddMinutes(value),
                DurationUnit.Hour => currentExpiration.AddHours(value),
                DurationUnit.Day => currentExpiration.AddDays(value),
                DurationUnit.Week => currentExpiration.AddDays(value * 7),
                DurationUnit.Month => currentExpiration.AddMonths(value),
                DurationUnit.Year => currentExpiration.AddYears(value),
                _ => currentExpiration
            };

            OwnedAssets[assetId] = newExpiration;
        }

        private string GenerateUserToken(string username, string email)
        {
            // 创建一个基于用户名、邮箱和当前时间的唯一token
            string baseString = $"{username}:{email}:{DateTime.Now.Ticks}";
            // 使用SHA256哈希生成token
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(baseString));
                return Convert.ToBase64String(hashBytes);
            }
        }
    }
} 
using Drx.Sdk.Network.DataBase.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KaxSocket
{
    public abstract class DataModel : IDataBase
    {
        public int Id { get; set; }
    }

    public class ModInfo : DataModel
    {
        public string ModId { get; set; }
        public string ModName { get; set; }
        public string ModVersion { get; set; }
        public string ModAuthor { get; set; }
        public string ModDescription { get; set; }
        public long LastUpdatedAt { get; set; }
    }

    /// <summary>
    /// 用户数据模型
    /// </summary>
    public class UserData : DataModel
    {
        public string UserName { get; set; }
        public string PasswordHash { get; set; }
        public string Email { get; set; }
        public long RegisteredAt { get; set; }
        public long LastLoginAt { get; set; }
        public string LoginToken { get; set; }
        public string DisplayName { get; set; }
        public UserPermissionGroup PermissionGroup { get; set; } = UserPermissionGroup.User;

        public UserStatus Status { get; set; } = new UserStatus();
        public List<ActiveAssets> ActiveAssets { get; set; } = new List<ActiveAssets>();
    }

    public class ActiveAssets : IDataTable
    {
        public int ParentId { get; set; }
        public int AssetId { get; set; } // 激活的资源 ID，例如正在使用中的 Mod 资源 ID
        public long ActivatedAt { get; set; } // 资源被激活的时间，Unix 时间戳
        public long ExpiresAt { get; set; } // 资源的过期时间，Unix 时间戳，0 表示永不过期
        public string TableName => nameof(ActiveAssets);
        public int Id { get; set; }
    }

    public class UserStatus : IDataTable
    {
        public int ParentId { get; set; }
        public bool IsBanned { get; set; }
        public long BannedAt { get; set; }
        public long BanExpiresAt { get; set; }
        public string BanReason { get; set; }   // 封禁原因
        public string TableName => nameof(UserStatus);
        public int Id { get; set; }
    }

    /// <summary>
    /// 用户权限组枚举（存储为整数）
    /// </summary>
    public enum UserPermissionGroup
    {
        /// <summary>控制台（系统控制台）</summary>
        Console = 0,
        /// <summary>最高权限</summary>
        Root = 1,
        /// <summary>管理员</summary>
        Admin = 2,
        /// <summary>普通用户（默认）</summary>
        User = 100
    }
}

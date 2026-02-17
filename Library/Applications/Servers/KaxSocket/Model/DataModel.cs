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
        public string Bio { get; set; } = string.Empty;
        public UserPermissionGroup PermissionGroup { get; set; } = UserPermissionGroup.User;

        public UserStatus Status { get; set; } = new UserStatus();
        public TableList<ActiveAssets> ActiveAssets { get; set; }

        // 新增字段：最近活动计数（后续由业务逻辑累加）
        public int RecentActivity { get; set; } = 0;

        // 新增字段：资源数（例如用户拥有的资源/作品数量）
        public int ResourceCount { get; set; } = 0;

        // 新增字段：贡献值（平台贡献积分/分数）
        public int Contribution { get; set; } = 0;
    }

    /// <summary>
    /// 用户激活的资源/Mod - 子表实例
    /// 实现 IDataTableV2 接口，使用 String 类型的 ID、毫秒时间戳和追踪字段
    /// 所有时间戳使用 Unix Milliseconds 确保精度和一致性
    /// </summary>
    public class ActiveAssets : IDataTableV2
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int ParentId { get; set; }
        public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        /// <summary>激活的资源 ID，例如正在使用中的 Mod 资源 ID</summary>
        public int AssetId { get; set; }
        
        /// <summary>资源被激活的时间（Unix Milliseconds）</summary>
        public long ActivatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        /// <summary>资源的过期时间（Unix Milliseconds），0 表示永不过期</summary>
        public long ExpiresAt { get; set; }
        
        public string TableName => nameof(ActiveAssets);
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

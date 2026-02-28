using Drx.Sdk.Network.DataBase;
using System;

namespace KaxSocket
{
    /// <summary>
    /// 数据模型基类，提供数据库主键 Id
    /// </summary>
    public abstract class DataModel : IDataBase
    {
        /// <summary>数据库主键</summary>
        public int Id { get; set; }
    }

    /// <summary>
    /// 用户数据模型，包含用户基本信息与若干子表引用
    /// </summary>
    public class UserData : DataModel
    {
        /// <summary>登录用户名</summary>
        public string UserName { get; set; }

        /// <summary>密码哈希</summary>
        public string PasswordHash { get; set; }

        /// <summary>邮箱</summary>
        public string Email { get; set; }

        /// <summary>注册时间（Unix 毫秒）</summary>
        public long RegisteredAt { get; set; }

        /// <summary>最后一次登录时间（Unix 毫秒）</summary>
        public long LastLoginAt { get; set; }

        /// <summary>登录令牌（可用于会话）</summary>
        public string LoginToken { get; set; }

        /// <summary>显示名称</summary>
        public string DisplayName { get; set; }

        /// <summary>签名</summary>
        public string Signature { get; set; } = string.Empty;

        /// <summary>个人简介</summary>
        public string Bio { get; set; } = string.Empty;

        /// <summary>权限分组，默认普通用户</summary>
        public UserPermissionGroup PermissionGroup { get; set; } = UserPermissionGroup.User;

        /// <summary>用户状态（封禁等信息）</summary>
        public UserStatus Status { get; set; } = new UserStatus();

        /// <summary>激活的资源集合（子表）</summary>
        public TableList<ActiveAssets> ActiveAssets { get; set; }

        /// <summary>收藏的资源（子表，仅保存 AssetId）</summary>
        public TableList<UserFavoriteAsset> FavoriteAssets { get; set; }

        /// <summary>购物车条目（子表，仅保存 AssetId，后续可扩展数量等字段）</summary>
        public TableList<UserCartItem> CartItems { get; set; }

        /// <summary>订单详细（子表，记录每次购买/兑换 CDK 的详细信息）</summary>
        public TableList<UserOrderRecord> OrderRecords { get; set; }
        
        /// <summary>邮箱是否已验证</summary>
        public bool EmailVerified { get; set; } = false;

        /// <summary>最近活动计数（业务逻辑累计）</summary>
        public int RecentActivity { get; set; } = 0;

        /// <summary>资源数（例如用户拥有的作品数量）</summary>
        public int ResourceCount { get; set; } = 0;

        /// <summary>金币（平台积分/分数）</summary>
        public int Gold { get; set; } = 0;
    }

    /// <summary>
    /// 表示用户激活的资源（子表项）
    /// 使用 IDataTableV2 约定：Id 为字符串、包含 ParentId 与时间追踪字段，时间使用 Unix 毫秒。
    /// </summary>
    public class ActiveAssets : IDataTableV2
    {
        /// <summary>子表项唯一 Id（字符串）</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>父表主键（所属 UserData.Id）</summary>
        public int ParentId { get; set; }

        /// <summary>创建时间（Unix 毫秒）</summary>
        public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>更新时间（Unix 毫秒）</summary>
        public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>激活的资源 Id</summary>
        public int AssetId { get; set; }

        /// <summary>激活时间（Unix 毫秒）</summary>
        public long ActivatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>过期时间（Unix 毫秒），0 表示永不过期</summary>
        public long ExpiresAt { get; set; }

        /// <summary>表名</summary>
        public string TableName => nameof(ActiveAssets);
    }

    /// <summary>
    /// 用户收藏资源（子表项），仅保存 AssetId
    /// </summary>
    public class UserFavoriteAsset : IDataTableV2
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int ParentId { get; set; }
        public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>收藏的资源 Id</summary>
        public int AssetId { get; set; }

        public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public string TableName => nameof(UserFavoriteAsset);
    }

    /// <summary>
    /// 购物车条目（子表项），仅存 AssetId（可扩展数量/选项）
    /// </summary>
    public class UserCartItem : IDataTableV2
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int ParentId { get; set; }
        public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>资源 Id</summary>
        public int AssetId { get; set; }

        public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public string TableName => nameof(UserCartItem);
    }

    /// <summary>
    /// 用户订单详细记录（子表项），记录每次购买或兑换 CDK 的单笔详情
    /// </summary>
    public class UserOrderRecord : IDataTableV2
    {
        /// <summary>子表项唯一 Id（字符串）</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>父表主键（所属 UserData.Id）</summary>
        public int ParentId { get; set; }

        /// <summary>创建时间（Unix 毫秒）</summary>
        public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>更新时间（Unix 毫秒）</summary>
        public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>订单类型："purchase"（金币购买）或 "cdk"（兑换 CDK）</summary>
        public string OrderType { get; set; } = string.Empty;

        /// <summary>关联资产 ID（购买时填充；CDK 兑换时为 0）</summary>
        public int AssetId { get; set; } = 0;

        /// <summary>资产名称快照（便于展示，不再查资产表）</summary>
        public string AssetName { get; set; } = string.Empty;

        /// <summary>使用的 CDK 码（CDK 兑换时填充；购买时为空）</summary>
        public string CdkCode { get; set; } = string.Empty;

        /// <summary>消耗金币数量（负数表示扣减，正数表示充值）</summary>
        public int GoldChange { get; set; } = 0;

        /// <summary>订单备注/描述</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>表名</summary>
        public string TableName => nameof(UserOrderRecord);
    }

    /// <summary>
    /// 用户状态信息（如封禁、封禁时间等）
    /// </summary>
    public class UserStatus : IDataTable
    {
        public int Id { get; set; }
        public int ParentId { get; set; }

        /// <summary>是否被封禁</summary>
        public bool IsBanned { get; set; }

        /// <summary>封禁时间（Unix 毫秒）</summary>
        public long BannedAt { get; set; }

        /// <summary>封禁到期时间（Unix 毫秒）</summary>
        public long BanExpiresAt { get; set; }

        /// <summary>封禁原因</summary>
        public string BanReason { get; set; }

        public string TableName => nameof(UserStatus);
    }

    /// <summary>
    /// 用户权限组枚举（用于持久化为整数）
    /// </summary>
    public enum UserPermissionGroup
    {
        /// <summary>系统最高权限</summary>
        System = 0,

        /// <summary>控制台权限</summary>
        Console = 2,

        /// <summary>管理员</summary>
        Admin = 3,

        /// <summary>普通用户（默认）</summary>
        User = 999
    }
}

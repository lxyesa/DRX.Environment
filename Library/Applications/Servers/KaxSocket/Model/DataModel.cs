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

        /// <summary>邮箱变更验证码记录（子表，用于双通道邮箱验证）</summary>
        public TableList<EmailChangeVerification> EmailChangeVerifications { get; set; }

        /// <summary>密码重置令牌记录（子表，用于忘记密码邮件重置流程）</summary>
        public TableList<PasswordResetToken> PasswordResetTokens { get; set; }

        /// <summary>会话失效基线时间戳（Unix 毫秒），该时刻之前签发的所有 Token 均视为失效</summary>
        public long TokenInvalidBefore { get; set; } = 0;
        
        /// <summary>邮箱是否已验证</summary>
        public bool EmailVerified { get; set; } = false;

        /// <summary>最近活动计数（业务逻辑累计）</summary>
        public int RecentActivity { get; set; } = 0;

        /// <summary>资源数（例如用户拥有的作品数量）</summary>
        public int ResourceCount { get; set; } = 0;

        /// <summary>金币（平台积分/分数）</summary>
        public int Gold { get; set; } = 0;

        /// <summary>
        /// 徽章（JSON 数组字符串），格式：[{"text":"MVP","color":[59,130,246]}]
        /// 兼容旧格式 badge1[r,g,b];badge2[r,g,b]，读取时自动迁移。
        /// </summary>
        public string Badges { get; set; } = string.Empty;
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
    /// 用户订单详细记录（子表项），记录每次购买、兑换 CDK 、取消订阅、更变计划、金币加减的单笔详情
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

        /// <summary>订单类型："purchase"（金币购买）、"cdk"（兑换 CDK）、"cancel_subscription"（取消订阅）、"change_plan"（更变计划）、"gold_adjust"（金币调整）</summary>
        public string OrderType { get; set; } = string.Empty;

        /// <summary>关联资产 ID（购买/取消/更变时填充；其他类型为 0）</summary>
        public int AssetId { get; set; } = 0;

        /// <summary>资产名称快照（便于展示，不再查资产表）</summary>
        public string AssetName { get; set; } = string.Empty;

        /// <summary>使用的 CDK 码（CDK 兑换时填充；其他类型为空）</summary>
        public string CdkCode { get; set; } = string.Empty;

        /// <summary>消耗金币数量（负数表示扣减，正数表示充值；金币调整时记录实际变化）</summary>
        public int GoldChange { get; set; } = 0;

        /// <summary>金币加减方式：可选值为 "cdk_redeem"（CDK兑换）、"purchase"（购买）、"admin"（管理员）、"refund"（退款）、"bonus"（奖励）等</summary>
        public string GoldChangeReason { get; set; } = string.Empty;

        /// <summary>从旧计划/到新计划（仅在 change_plan 时使用，格式："priceId1->priceId2"）</summary>
        public string PlanTransition { get; set; } = string.Empty;

        /// <summary>订单备注/描述</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>表名</summary>
        public string TableName => nameof(UserOrderRecord);
    }

    /// <summary>
    /// 用户邮箱变更验证码记录（子表项），用于旧邮箱/新邮箱双通道验证码状态管理。
    /// </summary>
    public class EmailChangeVerification : IDataTableV2
    {
        /// <summary>子表项唯一 Id（字符串）</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>父表主键（所属 UserData.Id）</summary>
        public int ParentId { get; set; }

        /// <summary>创建时间（Unix 毫秒）</summary>
        public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>更新时间（Unix 毫秒）</summary>
        public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>验证码通道：old 或 new</summary>
        public string Channel { get; set; } = "new";

        /// <summary>目标新邮箱（old 通道可为空）</summary>
        public string NewEmail { get; set; } = string.Empty;

        /// <summary>验证码哈希值（不存明文）</summary>
        public string CodeHash { get; set; } = string.Empty;

        /// <summary>验证码哈希盐（可选）</summary>
        public string CodeSalt { get; set; } = string.Empty;

        /// <summary>状态：pending / used / expired / cancelled / locked</summary>
        public string Status { get; set; } = "pending";

        /// <summary>已尝试次数</summary>
        public int Attempts { get; set; } = 0;

        /// <summary>最大允许尝试次数</summary>
        public int MaxAttempts { get; set; } = 5;

        /// <summary>过期时间（Unix 毫秒）</summary>
        public long ExpiresAt { get; set; }

        /// <summary>实际消费时间（Unix 毫秒，0 表示未消费）</summary>
        public long UsedAt { get; set; } = 0;

        /// <summary>请求来源 IP</summary>
        public string RequestIp { get; set; } = string.Empty;

        /// <summary>请求来源 User-Agent</summary>
        public string UserAgent { get; set; } = string.Empty;

        /// <summary>最近一次发送时间（Unix 毫秒）</summary>
        public long LastSendAt { get; set; } = 0;

        /// <summary>当前小时窗口发送次数</summary>
        public int HourlyCount { get; set; } = 0;

        /// <summary>当前日窗口发送次数</summary>
        public int DailyCount { get; set; } = 0;

        /// <summary>小时窗口起点时间（Unix 毫秒）</summary>
        public long WindowHourStartAt { get; set; } = 0;

        /// <summary>日窗口起点时间（Unix 毫秒）</summary>
        public long WindowDayStartAt { get; set; } = 0;

        /// <summary>子表名称</summary>
        public string TableName => nameof(EmailChangeVerification);
    }

    /// <summary>
    /// 密码重置令牌记录（子表项），用于忘记密码邮件重置流程。
    /// 明文 Token 仅在邮件中出现，数据库仅存储 hash/salt。
    /// </summary>
    public class PasswordResetToken : IDataTableV2
    {
        /// <summary>子表项唯一 Id（字符串）</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>父表主键（所属 UserData.Id）</summary>
        public int ParentId { get; set; }

        /// <summary>创建时间（Unix 毫秒）</summary>
        public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>更新时间（Unix 毫秒）</summary>
        public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>令牌哈希值（不存明文令牌）</summary>
        public string TokenHash { get; set; } = string.Empty;

        /// <summary>令牌哈希盐</summary>
        public string TokenSalt { get; set; } = string.Empty;

        /// <summary>状态：pending / used / expired / locked</summary>
        public string Status { get; set; } = "pending";

        /// <summary>已尝试次数（用于防暴力枚举）</summary>
        public int Attempts { get; set; } = 0;

        /// <summary>最大允许尝试次数</summary>
        public int MaxAttempts { get; set; } = 5;

        /// <summary>过期时间（Unix 毫秒）</summary>
        public long ExpiresAt { get; set; }

        /// <summary>实际消费时间（Unix 毫秒，0 表示未消费）</summary>
        public long UsedAt { get; set; } = 0;

        /// <summary>申请来源 IP</summary>
        public string RequestIp { get; set; } = string.Empty;

        /// <summary>申请来源 User-Agent</summary>
        public string UserAgent { get; set; } = string.Empty;

        /// <summary>最近一次发送时间（Unix 毫秒，用于冷却判断）</summary>
        public long LastSendAt { get; set; } = 0;

        /// <summary>当前小时窗口发送次数</summary>
        public int HourlyCount { get; set; } = 0;

        /// <summary>当前日窗口发送次数</summary>
        public int DailyCount { get; set; } = 0;

        /// <summary>小时窗口起点时间（Unix 毫秒）</summary>
        public long WindowHourStartAt { get; set; } = 0;

        /// <summary>日窗口起点时间（Unix 毫秒）</summary>
        public long WindowDayStartAt { get; set; } = 0;

        /// <summary>子表名称</summary>
        public string TableName => nameof(PasswordResetToken);
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

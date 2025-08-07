using System;
using System.Collections.Generic;
using Drx.Sdk.Network.DataBase.Sqlite;
using Drx.Sdk.Shared.JavaScript;
using KaxServer.Services;

namespace KaxServer.Models;

/// <summary>
/// 用户数据模型，包含基础信息、设置与状态。用于数据库持久化与业务逻辑处理。
/// </summary>
[ScriptExport("UserData", ScriptExportType.Class)]
public class UserData : IDataBase
{
    /// <summary>
    /// 用户唯一ID（主键，自动生成）
    /// </summary>
    [ScriptExport]
    public int Id { get; set; }

    /// <summary>
    /// Username属性，与UserName同步，兼容性用途
    /// </summary>
    [ScriptExport]
    public string Username { get; set; }

    /// <summary>
    /// 密码哈希（存储加密后的密码，严禁明文）
    /// </summary>
    [ScriptExport]
    public string PasswordHash { get; set; }

    /// <summary>
    /// 邮箱（唯一，用户找回密码等场景使用）
    /// </summary>
    [ScriptExport]
    public string Email { get; set; }

    /// <summary>
    /// 金币（虚拟货币，参与商城、奖励等）
    /// </summary>
    [ScriptExport]
    public int Coins { get; set; } = 0;

    /// <summary>
    /// 等级（成长体系，影响权限与奖励）
    /// </summary>
    [ScriptExport]
    public int Level { get; set; } = 0;

    /// <summary>
    /// 当前经验值（升级所需）
    /// </summary>
    [ScriptExport]
    public int Exp { get; set; } = 0;

    /// <summary>
    /// 下一级所需经验（升级阈值）
    /// </summary>
    public int NextLevelExp { get; set; } = 100;

    /// <summary>
    /// 用户Token
    /// </summary>
    public string AppToken { get; set; } = string.Empty;

    /// <summary>
    /// 用户设置数据（包含订阅、改名等）
    /// </summary>
    public UserSettingData UserSettingData { get; set; } = new UserSettingData();

    /// <summary>
    /// 用户状态数据（包含登录、权限等）
    /// </summary>
    public UserStatusData UserStatusData { get; set; } = new UserStatusData();

    /// <summary>
    /// 用户已发布的商品ID列表
    /// </summary>
    public List<int> PublishedStoreItemIds { get; set; } = new List<int>();

    public List<UserBuyedStoreItem> BuyedStoreItems { get; set; } = new List<UserBuyedStoreItem>();
}

/// <summary>
/// 用户设置数据
/// </summary>
/// <summary>
/// 用户设置数据表，包含订阅、改名等个性化设置。
/// </summary>
public class UserSettingData : IDataTable
{
    /// <summary>
    /// 主键ID（自动生成）
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 父表ID（关联UserData的ID）
    /// </summary>
    public int ParentId { get; set; }

    /// <summary>
    /// 数据表名（固定为UserSettingData）
    /// </summary>
    public string TableName => "UserSettingData";

    /// <summary>
    /// 是否开启邮件通知（系统消息推送）
    /// </summary>
    public bool EmailNotifications { get; set; } = true;

    /// <summary>
    /// 是否订阅新闻（平台资讯）
    /// </summary>
    public bool NewsSubscription { get; set; } = false;

    /// <summary>
    /// 是否订阅营销信息（活动、广告等）
    /// </summary>
    public bool MarketingSubscription { get; set; } = false;

    /// <summary>
    /// 上次改名时间（用于限制改名频率）
    /// </summary>
    public DateTime LastChangeNameTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// 下次可改名时间（冷却结束时间）
    /// </summary>
    public DateTime NextChangeNameTime { get; set; } = DateTime.MinValue;
}

/// <summary>
/// 用户状态数据
/// </summary>
public class UserStatusData : IDataTable
{
    public int Id { get; set; }
    /// <summary>父表ID（UserData的ID）</summary>
    public int ParentId { get; set; }
    public string TableName => "UserStatusData";
    /// <summary>是否为管理员</summary>
    public bool IsAdmin { get; set; } = true;
    /// <summary>是否被封禁</summary>
    public bool IsBanned { get; set; } = false;
    /// <summary>是否为应用登录状态</summary>
    public bool IsAppLogin { get; set; } = false;
    /// <summary>是否为网页登录状态</summary>
    public bool IsWebLogin { get; set; } = false;
    /// <summary>应用登录令牌</summary>
    [Publish]
    public string AppToken { get; set; } = string.Empty;
}

public class UserBuyedStoreItem : IDataTable
{
    public int Id { get; set; }
    public int ParentId { get; set; } // UserData的ID
    public string TableName => "UserBuyedStoreItem";
    public int StoreItemId { get; set; } // 商品ID
    public DateTime BuyTime { get; set; } = DateTime.Now; // 购买时间
    public DateTime ExpireTime { get; set; } = DateTime.MaxValue; // 过期时间，默认永不过期
    public bool IsExpired => ExpireTime < DateTime.Now; // 是否过期
}
using System;
using Drx.Sdk.Network.DataBase;

namespace KaxSocket.Model;

/// <summary>
/// 资产审核状态枚举
/// 
/// 状态说明：
/// - PendingReview (0): 审核中，新提交或重新提交审核的资产
/// - Rejected (1): 审核拒绝，需开发者修改后重新提交
/// - ApprovedPendingPublish (2): 审核通过，等待开发者发布到商店
/// - Active (3): 已上线，正常运行在商店中
/// - OffShelf (4): 已下架，由管理员主动下架（独立于软删除）
/// 
/// 重要：OffShelf 与 IsDeleted 的职责边界
/// - OffShelf: 表示资产被管理员下架，可恢复上架，状态流转：Active(3) -> OffShelf(4) -> Active(3)
/// - IsDeleted: 表示资产被软删除，通常不可恢复，与状态枚举无关
/// 下架操作不应修改 IsDeleted 字段，两者语义独立
/// </summary>
public enum AssetStatus
{
    /// <summary>审核中（新提交或重新提交）</summary>
    PendingReview = 0,

    /// <summary>审核拒绝</summary>
    Rejected = 1,

    /// <summary>审核通过，待发布</summary>
    ApprovedPendingPublish = 2,

    /// <summary>正常运行（已发布到商店）</summary>
    Active = 3,

    /// <summary>
    /// 已下架（管理员主动下架）
    /// 
    /// 注意：此状态独立于 IsDeleted 软删除语义
    /// - 下架后可通过管理员操作恢复上架
    /// - 不会触发 IsDeleted = true
    /// 状态流转：通常从 Active(3) 进入，可恢复到 Active(3)
    /// </summary>
    OffShelf = 4
}

/// <summary>
/// 资产/商品数据模型
/// 
/// 结构说明：
/// - 基本信息：名称、版本、作者、分类等通用字段
/// - 媒体资源：封面、图标、截图等图像 URL
/// - 价格方案：多种购买选项（子表 <see cref="AssetPrice"/>），每个方案独立库存
/// - 规格信息：文件信息、统计数据、时间戳等元数据（子表 <see cref="AssetSpecs"/>）
/// - 审核状态：资产需经审核后方可在商店展示
/// - 软删除：逻辑删除支持
/// </summary>
public class AssetModel : IDataBase
{
    public int Id { get; set; }

    #region 基本信息

    /// <summary>资源名称</summary>
    public string Name { get; set; }

    /// <summary>资源版本号</summary>
    public string Version { get; set; }

    /// <summary>资源作者用户 ID（关联 UserData.Id）</summary>
    public int AuthorId { get; set; }

    /// <summary>资源描述</summary>
    public string Description { get; set; }

    /// <summary>资源分类（如 "工具", "素材", "插件"）</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>标签列表（逗号分隔，如 "高性能,安全,稳定"）</summary>
    public string Tags { get; set; } = string.Empty;

    #endregion

    #region 媒体资源

    /// <summary>封面 URL（用于英雄区背景和商品卡片展示）</summary>
    public string CoverImage { get; set; } = string.Empty;

    /// <summary>图标 URL（用于列表页和推荐卡片小图）</summary>
    public string IconImage { get; set; } = string.Empty;

    /// <summary>截图 URL 列表（分号分隔，用于详情页画廊）</summary>
    public string Screenshots { get; set; } = string.Empty;

    #endregion

    #region 价格方案

    /// <summary>价格方案子表，支持多种购买选项，每个方案独立维护库存</summary>
    public TableList<AssetPrice> Prices { get; set; }

    #endregion

    #region 规格信息

    /// <summary>规格信息子表，统一管理文件、统计、时间戳、许可等元数据</summary>
    public AssetSpecs Specs { get; set; }

    #endregion

    #region 语言支持

    /// <summary>
    /// 语言支持表（JSON 字符串）
    /// 
    /// 结构示例：
    /// [
    ///   { "name": "中文", "isSupported": true },
    ///   { "name": "English", "isSupported": false }
    /// ]
    /// </summary>
    public string LanguageSupportsJson { get; set; } = string.Empty;

    #endregion

    #region 软删除

    /// <summary>是否已删除</summary>
    public bool IsDeleted { get; set; }

    /// <summary>删除时间戳（Unix 毫秒）</summary>
    public long DeletedAt { get; set; }

    #endregion

    #region 审核状态

    /// <summary>资产当前状态（审核流程）</summary>
    public AssetStatus Status { get; set; } = AssetStatus.PendingReview;

    /// <summary>最后一次提交审核时间戳（Unix 秒），用于4小时冷却检测</summary>
    public long LastSubmittedAt { get; set; }

    /// <summary>首次提交审核时间戳（Unix 秒），用于前端展示“提交日期”</summary>
    public long FirstSubmittedAt { get; set; }

    /// <summary>审核拒绝原因（仅在 Status == Rejected 时有值）</summary>
    public string RejectReason { get; set; } = string.Empty;

    #endregion

    #region 嵌套类型

    /// <summary>
    /// 资产价格方案（子表，一对多）
    /// 
    /// 说明：每个资产可拥有多个价格方案（购买选项），支持：
    /// - 不同时长/单位组合（如 1个月、3个月、1年等）
    /// - 独立的原价、折扣、最终价格
    /// - 方案级别的库存管理（-1 表示无限库存）
    /// </summary>
    public class AssetPrice : IDataTableV2
    {
        /// <summary>价格方案 ID（GUID）</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>关联的父资产 ID</summary>
        public int ParentId { get; set; }

        /// <summary>创建时间戳（Unix 毫秒）</summary>
        public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>更新时间戳（Unix 毫秒）</summary>
        public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>方案名称（如"月度授权"、"专业版"）</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>最终价格（最小货币单位，如分）</summary>
        public int Price { get; set; }

        /// <summary>时间单位（"year", "month", "day", "hour", "once" 等）</summary>
        public string Unit { get; set; } = "once";

        /// <summary>时间数量（如 Duration=1 + Unit="year" 表示 1 年）</summary>
        public int Duration { get; set; } = 1;

        /// <summary>授权时长（天数，0 表示永久授权）</summary>
        public int DurationDays { get; set; } = 0;

        /// <summary>原始价格（未折扣，最小货币单位）</summary>
        public int OriginalPrice { get; set; }

        /// <summary>折扣率（0.0-1.0，如 0.15 表示 15% 折扣）</summary>
        public double DiscountRate { get; set; } = 0.0;

        /// <summary>库存数量（-1 表示无限）</summary>
        public int Stock { get; set; } = -1;

        public string TableName => nameof(AssetPrice);
    }

    /// <summary>
    /// 资产规格信息（子表，一对一）
    /// 
    /// 说明：统一管理资产的元数据，包括：
    /// - 文件信息：大小、下载地址等
    /// - 许可与兼容：证书类型、平台支持等
    /// - 统计数据：评分、下载量、购买数等
    /// - 时间戳：上传、更新时间等
    /// </summary>
    public class AssetSpecs : IDataTable
    {
        /// <summary>规格记录 ID</summary>
        public int Id { get; set; }

        /// <summary>关联的父资产 ID</summary>
        public int ParentId { get; set; }

        #region 文件信息

        /// <summary>文件大小（字节）</summary>
        public long FileSize { get; set; } = 0;

        /// <summary>下载地址（可选）</summary>
        public string DownloadUrl { get; set; } = string.Empty;

        #endregion

        #region 许可与兼容

        /// <summary>许可证信息（如 MIT、Apache、GPL）</summary>
        public string License { get; set; } = string.Empty;

        /// <summary>兼容性描述（支持的版本、平台等）</summary>
        public string Compatibility { get; set; } = string.Empty;

        #endregion

        #region 统计数据

        /// <summary>平均评分（0.0-5.0）</summary>
        public double Rating { get; set; } = 0.0;

        /// <summary>评价数量</summary>
        public int ReviewCount { get; set; } = 0;

        /// <summary>总下载次数</summary>
        public int Downloads { get; set; } = 0;

        /// <summary>总购买人数</summary>
        public int PurchaseCount { get; set; } = 0;

        /// <summary>总收藏人数</summary>
        public int FavoriteCount { get; set; } = 0;

        /// <summary>总浏览量</summary>
        public int ViewCount { get; set; } = 0;

        #endregion

        #region 时间戳

        /// <summary>创建/上传时间（Unix 毫秒）</summary>
        public long UploadDate { get; set; } = 0;

        /// <summary>最后更新时间（Unix 毫秒）</summary>
        public long LastUpdatedAt { get; set; }

        #endregion

        public string TableName => nameof(AssetSpecs);
    }

    /// <summary>
    /// 语言支持条目（用于前后端 JSON 传输）
    /// </summary>
    public class AssetLanguageSupport
    {
        /// <summary>语言显示名（例如：中文、English、日本語）</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>是否支持该语言</summary>
        public bool IsSupported { get; set; }
    }

    #endregion
}

/// <summary>
/// 资产管理审计日志
/// 
/// 记录系统管理员对资产执行的每一次操作（动作或字段更新），便于追踪与回溯。
/// 
/// ActionType 取值：
/// - "return"：退回资产
/// - "off-shelf"：下架资产
/// - "force-review"：强制重审（强制更新）
/// - "relist"：恢复上架
/// - "update-field"：字段更新
/// </summary>
public class AssetAuditLog : IDataBase
{
    /// <summary>审计记录主键 ID（自增）</summary>
    public int Id { get; set; }

    /// <summary>操作的资产 ID</summary>
    public int AssetId { get; set; }

    /// <summary>操作者用户 ID</summary>
    public int OperatorUserId { get; set; }

    /// <summary>操作者用户名</summary>
    public string OperatorUserName { get; set; } = string.Empty;

    /// <summary>动作类型（return / off-shelf / force-review / relist / update-field）</summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>操作原因（退回、下架等附带的说明）</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>操作时间（Unix 毫秒）</summary>
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>操作前资产状态（仅动作类操作有意义；字段更新时为 -1）</summary>
    public int BeforeStatus { get; set; } = -1;

    /// <summary>操作后资产状态（仅动作类操作有意义；字段更新时为 -1）</summary>
    public int AfterStatus { get; set; } = -1;

    /// <summary>
    /// 字段变更记录（JSON 格式，仅 update-field 时有值）
    /// 格式：{ "field": "xxx", "oldValue": "...", "newValue": "..." }
    /// </summary>
    public string FieldChangesJson { get; set; } = string.Empty;

    /// <summary>本次操作是否成功</summary>
    public bool Success { get; set; } = true;

    /// <summary>失败时的错误码（成功时为空）</summary>
    public string ErrorCode { get; set; } = string.Empty;

    /// <summary>失败时的错误消息（成功时为空）</summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>请求 ID（用于跨日志关联，格式为 GUID）</summary>
    public string RequestId { get; set; } = string.Empty;
}

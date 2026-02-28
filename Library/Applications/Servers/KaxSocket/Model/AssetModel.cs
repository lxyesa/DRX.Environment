using System;
using Drx.Sdk.Network.DataBase;

namespace KaxSocket.Model;

/// <summary>
/// 资产/商品数据模型
/// 
/// 结构说明：
/// - 基本信息：名称、版本、作者、分类等通用字段
/// - 媒体资源：主图、缩略图、截图等图像 URL
/// - 价格方案：多种购买选项（子表 <see cref="AssetPrice"/>），每个方案独立库存
/// - 规格信息：文件信息、统计数据、时间戳等元数据（子表 <see cref="AssetSpecs"/>）
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

    /// <summary>资源作者</summary>
    public string Author { get; set; }

    /// <summary>资源描述</summary>
    public string Description { get; set; }

    /// <summary>资源分类（如 "工具", "素材", "插件"）</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>标签列表（逗号分隔，如 "高性能,安全,稳定"）</summary>
    public string Tags { get; set; } = string.Empty;

    #endregion

    #region 媒体资源

    /// <summary>主图 URL（用于英雄区和商品卡片展示）</summary>
    public string PrimaryImage { get; set; } = string.Empty;

    /// <summary>缩略图 URL（用于列表页和推荐卡片）</summary>
    public string ThumbnailImage { get; set; } = string.Empty;

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

    #region 软删除

    /// <summary>是否已删除</summary>
    public bool IsDeleted { get; set; }

    /// <summary>删除时间戳（Unix 毫秒）</summary>
    public long DeletedAt { get; set; }

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

        /// <summary>最终价格（最小货币单位，如分）</summary>
        public int Price { get; set; }

        /// <summary>时间单位（"year", "month", "day", "hour", "once" 等）</summary>
        public string Unit { get; set; } = "once";

        /// <summary>时间数量（如 Duration=1 + Unit="year" 表示 1 年）</summary>
        public int Duration { get; set; } = 1;

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

    #endregion
}

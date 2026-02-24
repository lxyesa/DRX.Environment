using System;
using Drx.Sdk.Network.DataBase.Sqlite;

namespace KaxSocket.Model;

public class AssetModel : IDataBase
{
    public int Id { get; set; }
    public string Name { get; set; }    // 资源名称，例如 ModName
    public string Version { get; set; } // 资源版本，例如 ModVersion
    public string Author { get; set; }  // 资源作者，例如 ModAuthor
    public string Description { get; set; } // 资源描述，例如 ModDescription
    public long LastUpdatedAt { get; set; } // 资源最后更新时间，Unix 时间戳
    // 价格（以最小货币单位表示，例如分）
    public int Price { get; set; } = 0;
    // 库存数量
    public int Stock { get; set; } = 0;
    // 原始价格（未打折前的价格，最小货币单位，例如分）
    public int OriginalPrice { get; set; } = 0;
    // 资源分类，例如 "工具", "素材", "插件"
    public string Category { get; set; } = string.Empty;
    // 文件大小，字节为单位
    public long FileSize { get; set; } = 0;
    // 折扣率，取值为 0.0 - 1.0（例如 0.15 表示 15% 折扣）
    public double DiscountRate { get; set; } = 0.0;
    // 折后价格（如果有），以最小货币单位表示（例如分）
    public int SalePrice { get; set; } = 0;
    // 评分（平均分，0-5）
    public double Rating { get; set; } = 0.0;
    // 评论/评价数量
    public int ReviewCount { get; set; } = 0;
    // 兼容性描述（例如支持的版本或平台）
    public string Compatibility { get; set; } = string.Empty;
    // 下载次数（累计）
    public int Downloads { get; set; } = 0;
    // 上传时间或创建时间，Unix 时间戳
    public long UploadDate { get; set; } = 0;
    // 许可证信息，例如 MIT、Apache
    public string License { get; set; } = string.Empty;
    // 下载地址（可选）
    public string DownloadUrl { get; set; } = string.Empty;
    // 统计字段：购买人数（累计）
    public int PurchaseCount { get; set; } = 0;
    // 收藏人数（累计）
    public int FavoriteCount { get; set; } = 0;
    // 浏览人数或浏览量（累计）
    public int ViewCount { get; set; } = 0;
    // 软删除相关字段
    public bool IsDeleted { get; set; }
    public long DeletedAt { get; set; }
}

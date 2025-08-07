using Drx.Sdk.Network.DataBase.Sqlite;
using Drx.Sdk.Shared.JavaScript;

public enum StoreItemDurationUnit
{
    Day,
    Week,
    Month,
    Year
}

[ScriptExport("StoreItem", ScriptExportType.Class)]
public class StoreItem : IDataBase
{
    // 主表ID/商品ID
    [ScriptExport]
    public int Id { get; set; }
    [ScriptExport]
    public int OwnerId { get; set; }    // 商品所有者ID(对应UserData的ID)
    [ScriptExport]
    public string Title { get; set; }   // 商品标题
    [ScriptExport]
    public string Description { get; set; } // 商品描述
    [ScriptExport]
    public string Image { get; set; }    // 商品图片(url)
    [ScriptExport]
    public string Icon { get; set; }    // 商品图标(url)
    [ScriptExport]
    public int DownloadCount { get; set; } // 下载次数
    [ScriptExport]
    public float Rating { get; set; } // 评分
    [ScriptExport]
    public List<StoreItemTag> Tags { get; set; } = new(); // 商品标签
    [ScriptExport]
    public List<StoreItemPrice> StoreItemPrices { get; set; } = new();  // 商品的定价表
    [ScriptExport]
    public StoreItemDetail StoreItemDetails { get; set; } = new();    // 商品的详细信息
}

public class StoreItemPrice : IDataTable    // 商品的价格
{
    public int ParentId { get; set; }
    public string TableName => "StoreItemPrice";
    public string Title { get; set; } // 价格标题
    public float Price { get; set; }  // 价格，这里是原价
    public float Rebate { get; set; } // 折扣率，0-1之间
    public float Duration { get; set; } // 时长
    public StoreItemDurationUnit DurationUnit { get; set; } // 时长单位
}

public class StoreItemDetail : IDataTable    // 商品的详细信息
{
    public int ParentId { get; set; }

    public string TableName => "StoreItemDetail";

    public string Content { get; set; } // 商品详情内容
}

public class StoreItemTag : IDataTable    // 商品的标签
{
    public int ParentId { get; set; }

    public string TableName => "StoreItemTag";

    public string Tag { get; set; } // 商品标签
}
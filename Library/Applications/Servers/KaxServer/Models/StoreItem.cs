using Drx.Sdk.Network.DataBase.Sqlite;

public enum StoreItemDurationUnit
{
    Day,
    Week,
    Month,
    Year
}

public class StoreItem : IDataBase
{
    // 主表ID/商品ID
    public int Id { get; set; }
    public int OwnerId { get; set; }    // 商品所有者ID(对应UserData的ID)
    public string Title { get; set; }   // 商品标题
    public string Description { get; set; } // 商品描述
    public string Image { get; set; }    // 商品图片(url)
    public string Icon { get; set; }    // 商品图标(url)
    public int DownloadCount { get; set; } // 下载次数
    public float Rating { get; set; } // 评分
    public List<StoreItemTag> Tags { get; set; } = new(); // 商品标签
    public List<StoreItemPrice> StoreItemPrices { get; set; } = new();  // 商品的定价表
    public StoreItemDetail StoreItemDetails { get; set; } = new();    // 商品的详细信息
}

public class StoreItemPrice : IDataTable    // 商品的价格
{
    public int ParentId { get; set; }
    public string TableName => "StoreItemPrice";
    public string Title { get; set; } // 价格标题
    public decimal Price { get; set; }  // 价格，这里是原价
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
using System;
using System.Globalization;
using Drx.Sdk.Network.DataBase;

public class Cdk : IXmlSerializable
{
    public string Code { get; set; }
    public decimal? CoinAmount { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreationDate { get; set; }
    public string BatchId { get; set; }

    // 这个方法告诉数据库如何将一个Cdk对象写入XML。
    public void WriteToXml(IXmlNode node)
    {
        node.PushString("data", "Code", Code);
        node.PushBool("data", "IsUsed", IsUsed);
        node.PushString("data", "CreationDate", CreationDate.ToString("o"));
        if (CoinAmount.HasValue) node.PushDecimal("data", "CoinAmount", CoinAmount.Value);
        if (!string.IsNullOrEmpty(BatchId)) node.PushString("data", "BatchId", BatchId);
    }

    // 这个方法告诉数据库如何从XML读取数据并填充一个Cdk对象。
    public void ReadFromXml(IXmlNode node)
    {
        Code = node.GetString("data", "Code") ?? string.Empty;
        IsUsed = node.GetBool("data", "IsUsed");
        CreationDate = DateTime.Parse(node.GetString("data", "CreationDate"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        CoinAmount = node.GetDecimalNullable("data", "CoinAmount");
        BatchId = node.GetString("data", "BatchId");
    }
} 
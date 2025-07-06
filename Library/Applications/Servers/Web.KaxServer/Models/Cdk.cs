using System;
using System.Globalization;
using Drx.Sdk.Network.DataBase;

namespace Web.KaxServer.Models
{
    public class Cdk : IXmlSerializable
    {
        public string Code { get; set; } = string.Empty;
        public CdkType Type { get; set; }
        public int? AssetId { get; set; }
        public decimal? CoinAmount { get; set; }
        public int? DurationValue { get; set; }
        public DurationUnit? DurationUnit { get; set; }
        public bool IsUsed { get; set; }
        public DateTime CreationDate { get; set; }
        public string? UsedByUsername { get; set; }
        public DateTime? UsedDate { get; set; }
        public string? BatchId { get; set; }

        public void WriteToXml(IXmlNode node)
        {
            node.PushString("data", "Code", Code);
            node.PushInt("data", "Type", (int)Type);
            node.PushBool("data", "IsUsed", IsUsed);
            node.PushString("data", "CreationDate", CreationDate.ToString("o", CultureInfo.InvariantCulture));

            if (AssetId.HasValue) node.PushInt("data", "AssetId", AssetId.Value);
            if (CoinAmount.HasValue) node.PushDecimal("data", "CoinAmount", CoinAmount.Value);
            if (DurationValue.HasValue) node.PushInt("data", "DurationValue", DurationValue.Value);
            if (DurationUnit.HasValue) node.PushInt("data", "DurationUnit", (int)DurationUnit.Value);
            
            if (!string.IsNullOrEmpty(UsedByUsername)) node.PushString("data", "UsedByUsername", UsedByUsername);
            if (UsedDate.HasValue) node.PushString("data", "UsedDate", UsedDate.Value.ToString("o", CultureInfo.InvariantCulture));
            if (!string.IsNullOrEmpty(BatchId)) node.PushString("data", "BatchId", BatchId);
        }

        public void ReadFromXml(IXmlNode node)
        {
            Code = node.GetString("data", "Code") ?? string.Empty;
            Type = (CdkType)node.GetInt("data", "Type");
            IsUsed = node.GetBool("data", "IsUsed");
            CreationDate = DateTime.Parse(node.GetString("data", "CreationDate"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

            AssetId = node.GetIntNullable("data", "AssetId");
            CoinAmount = node.GetDecimalNullable("data", "CoinAmount");
            DurationValue = node.GetIntNullable("data", "DurationValue");

            var duInt = node.GetIntNullable("data", "DurationUnit");
            DurationUnit = duInt.HasValue ? (DurationUnit?)duInt : null;

            UsedByUsername = node.GetString("data", "UsedByUsername");

            var usedDateStr = node.GetString("data", "UsedDate");
            if (!string.IsNullOrEmpty(usedDateStr))
            {
                UsedDate = DateTime.Parse(usedDateStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            }
            
            BatchId = node.GetString("data", "BatchId");
        }
    }
} 
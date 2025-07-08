using System;
using System.Globalization;
using Drx.Sdk.Network.DataBase;
using Drx.Sdk.Network.Sqlite;

namespace Web.KaxServer.Models
{
    public class Cdk : IDataBase
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

        int IDataBase.Id { get => Code.GetHashCode(); set => Code.GetHashCode(); }
    }
} 
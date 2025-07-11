using System;
using Drx.Sdk.Network.Sqlite;

namespace KaxServer.Models;

public class UserData : IDataBase
{
    public int Id { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }
    public string Email { get; set; }
    public int Coins { get; set; } = 0;
    public int Level { get; set; } = 0;
    public int Exp { get; set; } = 0;
    public int NextLevelExp { get; set; } = 100;

    public UserSettingData UserSettingData { get; set; } = new UserSettingData();
}

public class UserSettingData : IDataTable
{
    public bool EmailNotifications { get; set; } = true;
    public bool NewsSubscription { get; set; } = false; // 资讯订阅
    public bool MarketingSubscription { get; set; } = false; // 市场推广订阅
    public DateTime LastChangeNameTime { get; set; } = DateTime.MinValue;
    public DateTime NextChangeNameTime { get; set; } = DateTime.MinValue;
    public int ParentId { get; set; }
    public string TableName => "UserSettingData";
}

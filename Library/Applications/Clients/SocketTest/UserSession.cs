using System;
using System.Globalization;
using Drx.Sdk.Network.DataBase;

public class UserSession : IXmlSerializable
{
    public Guid SessionId { get; set; }
    public int UserId { get; set; }
    public string UserName { get; set; }
    public DateTime LoginTime { get; set; }
    public DateTime LastActivityTime { get; set; }
    public string IpAddress { get; set; }
    public bool IsActive { get; set; }

    public void WriteToXml(IXmlNode node)
    {
        node.PushString("data", "SessionId", SessionId.ToString());
        node.PushInt("data", "UserId", UserId);
        node.PushString("data", "UserName", UserName);
        node.PushString("data", "LoginTime", LoginTime.ToString("o"));
        node.PushString("data", "LastActivityTime", LastActivityTime.ToString("o"));
        node.PushString("data", "IpAddress", IpAddress);
        node.PushBool("data", "IsActive", IsActive);
    }

    public void ReadFromXml(IXmlNode node)
    {
        SessionId = Guid.Parse(node.GetString("data", "SessionId"));
        UserId = node.GetInt("data", "UserId");
        UserName = node.GetString("data", "UserName");
        LoginTime = DateTime.Parse(node.GetString("data", "LoginTime"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        LastActivityTime = DateTime.Parse(node.GetString("data", "LastActivityTime"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        IpAddress = node.GetString("data", "IpAddress");
        IsActive = node.GetBool("data", "IsActive");
    }
} 
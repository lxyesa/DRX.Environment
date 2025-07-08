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
}

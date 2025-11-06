using System;
using Drx.Sdk.Network.DataBase.Sqlite;

namespace KaxSocket;

public static class KaxGlobal
{
    public static readonly SqliteUnified<UserData> UserDatabase = new SqliteUnified<UserData>($"{AppDomain.CurrentDomain.BaseDirectory}kax_users.db");
}

using System;
using System.IO;
using Drx.Sdk.Network.DataBase;
using Web.KaxServer.Models;

namespace Web.KaxServer.Services.Repositorys;

public static class UserRepository
{
    public static readonly Sqlite<UserData> UserDataSqlite;

    static UserRepository()
    {
        var dbDirectory = Path.Combine(AppContext.BaseDirectory, "Data", "userdata");
        if (!Directory.Exists(dbDirectory))
        {
            Directory.CreateDirectory(dbDirectory);
        }
        var dbPath = Path.Combine(dbDirectory, "user_data.db");
        UserDataSqlite = new Sqlite<UserData>(dbPath);
    }
    
    public static UserData? GetUser(int userId)
    {
        return UserDataSqlite.ReadSingle("UserId", userId);
    }
    
    public static UserData? GetUser(string username)
    {
        return UserDataSqlite.ReadSingle("Username", username);
    }

    public static List<UserData> GetAllUsers()
    {
        return UserDataSqlite.Read();
    }

    public static void SaveUser(UserData user)
    {
        UserDataSqlite.Save(user);
    }
}

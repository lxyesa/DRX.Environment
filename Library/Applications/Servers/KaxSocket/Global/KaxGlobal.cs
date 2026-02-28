using System;
using System.IO;
using System.Security.Claims;
using System.Linq;
using Microsoft.Data.Sqlite;
using Drx.Sdk.Network.DataBase;
using Drx.Sdk.Shared;
using KaxSocket.Model;
using Drx.Sdk.Network.Http.Auth;

namespace KaxSocket;

/// <summary>
/// KaxGlobal 主文件：持有全局静态数据库实例以及 Token、头像基础方法。
/// </summary>
public static partial class KaxGlobal
{
    public static readonly SqliteV2<UserData> UserDatabase = new SqliteV2<UserData>("kax_users.db", AppDomain.CurrentDomain.BaseDirectory);
    public static readonly SqliteV2<CdkModel> CdkDatabase = new SqliteV2<CdkModel>("cdk.db", AppDomain.CurrentDomain.BaseDirectory);
    public static readonly SqliteV2<AssetModel> AssetDataBase = new SqliteV2<AssetModel>("assets.db", AppDomain.CurrentDomain.BaseDirectory);

    public static string GenerateLoginToken(UserData user)
    {
        return JwtHelper.GenerateToken(user.Id.ToString(), user.UserName, user.Email);
    }

    public static ClaimsPrincipal ValidateToken(string token)
    {
        return JwtHelper.ValidateToken(token);
    }

    /// <summary>
    /// 通过用户 Id 返回已存在的头像文件的本地路径（优先 .png，其次 .jpg）。
    /// 未找到返回 null。路径位于 {AppBase}/resources/user/icon/{uid}.png|jpg
    /// </summary>
    public static string? GetUserAvatarPathById(int userId)
    {
        try
        {
            var iconsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "user", "icon");
            var png = Path.Combine(iconsDir, $"{userId}.png");
            var jpg = Path.Combine(iconsDir, $"{userId}.jpg");
            if (File.Exists(png)) return png;
            if (File.Exists(jpg)) return jpg;
            return null;
        }
        catch (Exception ex)
        {
            Logger.Warn($"GetUserAvatarPathById({userId}) 读取失败: {ex.Message}");
            return null;
        }
    }
}

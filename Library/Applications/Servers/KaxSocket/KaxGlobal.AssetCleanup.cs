using System;
using System.Linq;
using Microsoft.Data.Sqlite;
using Drx.Sdk.Shared;
using KaxSocket.Model;

namespace KaxSocket;

/// <summary>
/// KaxGlobal 资产清理：过期资产清理、无效资产清理、已删除资产清理。
/// </summary>
public static partial class KaxGlobal
{
    /// <summary>
    /// 安全地删除 ActiveAssets 子表中的过期记录。
    /// ExpiresAt == 0 表示永久有效，不会被删除。
    /// </summary>
    private static async Task<int> DeleteExpiredActiveAssetsAsync()
    {
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var childTable = "UserData_ActiveAssets";
        using var conn = new SqliteConnection(UserDatabase.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM [{childTable}] WHERE [ExpiresAt] > 0 AND [ExpiresAt] <= @now";
        cmd.Parameters.AddWithValue("@now", currentTime);
        return await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 安全地删除 ActiveAssets 中对应资源不存在的记录（防止孤立记录）。
    /// </summary>
    private static async Task<int> DeleteInvalidActiveAssetsAsync()
    {
        var assets = await AssetDataBase.SelectAllAsync();
        var assetIds = assets.Select(a => a.Id).ToArray();
        if (assetIds.Length == 0) return 0;

        var childTable = "UserData_ActiveAssets";
        var placeholders = string.Join(",", assetIds.Select((_, i) => $"@id{i}"));
        using var conn = new SqliteConnection(UserDatabase.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM [{childTable}] WHERE [AssetId] NOT IN ({placeholders})";
        for (int i = 0; i < assetIds.Length; i++) cmd.Parameters.AddWithValue($"@id{i}", assetIds[i]);
        return await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 安全地删除 ActiveAssets 中对应资源已被标记为删除的记录。
    /// </summary>
    private static async Task<int> DeleteMarkedDeletedAssetsAsync()
    {
        var deletedAssets = await AssetDataBase.SelectWhereAsync("IsDeleted", true);
        var deletedIds = deletedAssets.Select(a => a.Id).ToArray();
        if (deletedIds.Length == 0) return 0;

        var childTable = "UserData_ActiveAssets";
        var placeholders = string.Join(",", deletedIds.Select((_, i) => $"@id{i}"));
        using var conn = new SqliteConnection(UserDatabase.ConnectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"DELETE FROM [{childTable}] WHERE [AssetId] IN ({placeholders})";
        for (int i = 0; i < deletedIds.Length; i++) cmd.Parameters.AddWithValue($"@id{i}", deletedIds[i]);
        return await cmd.ExecuteNonQueryAsync();
    }

    public static async Task CleanUpExpiredAssets()
    {
        var deletedCount = await DeleteExpiredActiveAssetsAsync();
        if (deletedCount > 0)
            Logger.Info($"已清理 {deletedCount} 条过期激活资源。");
    }

    public static async Task CleanNotFoundAssets()
    {
        var deletedCount = await DeleteInvalidActiveAssetsAsync();
        if (deletedCount > 0)
            Logger.Info($"已清理 {deletedCount} 条无效激活资源（对应资源已不存在）。");
    }

    public static async Task CleanUpDeletedAssets()
    {
        var deletedCount = await DeleteMarkedDeletedAssetsAsync();
        if (deletedCount > 0)
            Logger.Info($"已清理 {deletedCount} 条已删除激活资源。");
    }

    /// <summary>
    /// 一键清理所有过期、无效、已删除的激活资产记录。
    /// </summary>
    public static async Task CleanUpAssets()
    {
        await CleanUpExpiredAssets();
        await CleanNotFoundAssets();
        await CleanUpDeletedAssets();
    }
}

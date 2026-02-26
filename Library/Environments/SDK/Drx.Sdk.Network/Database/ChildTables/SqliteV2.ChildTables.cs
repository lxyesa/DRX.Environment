using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Data.Sqlite;
using Drx.Sdk.Shared;

namespace Drx.Sdk.Network.DataBase;

/// <summary>
/// 高性能 Sqlite ORM - V2 版本 (Partial: 子表处理 - 入口与公共方法)
/// 提供子表创建、插入入口和共享辅助方法
/// </summary>
public partial class SqliteV2<T> where T : class, IDataBase, new()
{
    #region 子表建表

    /// <summary>
    /// 创建子表结构（支持 List 和 TableList 两种模式）
    /// </summary>
    private void CreateChildTable(SqliteConnection connection, Type childType, string childTableName, bool isTableList = false)
    {
        var props = childType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.Name != "TableName" && p.Name != "Id" && p.Name != "ParentId" 
                && (!isTableList || (p.Name != "CreatedAt" && p.Name != "UpdatedAt")))
            .ToArray();

        var columns = new System.Text.StringBuilder();
        
        if (isTableList)
        {
            columns.Append("Id TEXT PRIMARY KEY,");
            columns.Append("ParentId INTEGER,");
            columns.Append("CreatedAt INTEGER,");
            columns.Append("UpdatedAt INTEGER,");
        }
        else
        {
            columns.Append("Id INTEGER PRIMARY KEY AUTOINCREMENT,");
            columns.Append("ParentId INTEGER,");
        }

        foreach (var prop in props)
        {
            var sqlType = GetSqliteType(prop.PropertyType);
            columns.Append($"[{prop.Name}] {sqlType},");
        }

        var sql = $"""
            CREATE TABLE IF NOT EXISTS [{childTableName}] (
                {columns.ToString().TrimEnd(',')}
            )
            """;

        using var cmd = new SqliteCommand(sql, connection);
        cmd.ExecuteNonQuery();
    }

    #endregion

    #region 子表插入入口

    /// <summary>
    /// 同步插入子表数据（List 和 OneToOne 场景，TableList 采用延迟同步）
    /// </summary>
    private void InsertChildTablesSync(SqliteConnection connection, SqliteTransaction? transaction, T entity)
    {
        bool createdTransaction = false;
        if (transaction == null)
        {
            connection.Open();
            transaction = connection.BeginTransaction();
            createdTransaction = true;
        }
        
        try
        {
            InsertListChildren(connection, transaction, entity);
            InsertOneToOneChildren(connection, transaction, entity);
        }
        catch
        {
            if (createdTransaction)
                transaction?.Rollback();
            throw;
        }
        finally
        {
            if (createdTransaction)
                transaction?.Commit();
        }
    }

    /// <summary>
    /// 插入单个子表实体（List 和 OneToOne 共用）
    /// </summary>
    private void InsertChildEntity(SqliteConnection connection, SqliteTransaction transaction, IDataTable child, 
        int parentId, string childTableName, PropertyInfo[] childProps)
    {
        child.ParentId = parentId;

        var columns = new List<string> { "ParentId" };
        var values = new List<string> { "@ParentId" };

        foreach (var prop in childProps)
        {
            columns.Add($"[{prop.Name}]");
            values.Add($"@{prop.Name}");
        }

        var sql = $"INSERT INTO [{childTableName}] ({string.Join(",", columns)}) VALUES ({string.Join(",", values)})";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Transaction = transaction;

        cmd.Parameters.AddWithValue("@ParentId", parentId);
        foreach (var prop in childProps)
        {
            var value = prop.GetValue(child);
            cmd.Parameters.AddWithValue($"@{prop.Name}", value ?? DBNull.Value);
        }

        cmd.ExecuteNonQuery();
    }

    #endregion

    #region 子表加载入口

    /// <summary>
    /// 从数据库加载所有类型的子表数据
    /// </summary>
    private void LoadChildDataSync(T entity)
    {
        using var connection = GetConnection();
        LoadListChildData(connection, entity);
        LoadTableListChildData(connection, entity);
        LoadOneToOneChildData(connection, entity);
    }

    /// <summary>
    /// 初始化子表列序号（简化版，无需缓存）
    /// </summary>
    private void InitializeChildOrdinals(SqliteDataReader reader, PropertyInfo[] props)
    {
    }

    #endregion
}

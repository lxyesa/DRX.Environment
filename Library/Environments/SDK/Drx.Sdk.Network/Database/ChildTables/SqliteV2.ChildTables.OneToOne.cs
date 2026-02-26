using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Data.Sqlite;

namespace Drx.Sdk.Network.DataBase;

/// <summary>
/// 高性能 Sqlite ORM - V2 版本 (Partial: 一对一子表处理)
/// 处理 IDataTable 类型的一对一子表插入和加载
/// </summary>
public partial class SqliteV2<T> where T : class, IDataBase, new()
{
    #region 一对一子表操作

    /// <summary>
    /// 插入 IDataTable 类型的一对一子表数据
    /// </summary>
    private void InsertOneToOneChildren(SqliteConnection connection, SqliteTransaction transaction, T entity)
    {
        foreach (var childProp in _dataTableProperties)
        {
            var child = (IDataTable?)childProp.GetValue(entity);
            if (child == null)
                continue;

            var childTableName = $"{_tableName}_{childProp.Name}";
            var childType = childProp.PropertyType;
            var childProps = childType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.Name != "TableName" && p.Name != "Id" && p.Name != "ParentId")
                .ToArray();

            InsertChildEntity(connection, transaction, child, entity.Id, childTableName, childProps);
        }
    }

    /// <summary>
    /// 从数据库加载 IDataTable 类型的一对一子表数据
    /// </summary>
    private void LoadOneToOneChildData(SqliteConnection connection, T entity)
    {
        foreach (var childProp in _dataTableProperties)
        {
            var childType = childProp.PropertyType;
            var childTableName = $"{_tableName}_{childProp.Name}";

            var sql = $"SELECT * FROM [{childTableName}] WHERE ParentId = @parentId LIMIT 1";
            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@parentId", entity.Id);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                var child = (IDataTable)Activator.CreateInstance(childType)!;
                
                try
                {
                    child.Id = reader.GetInt32(reader.GetOrdinal("Id"));
                }
                catch { }

                try
                {
                    child.ParentId = reader.GetInt32(reader.GetOrdinal("ParentId"));
                }
                catch { }

                var childProps = childType
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && p.CanWrite && p.Name != "TableName")
                    .ToArray();

                foreach (var prop in childProps)
                {
                    try
                    {
                        var ordinal = reader.GetOrdinal(prop.Name);
                        if (!reader.IsDBNull(ordinal))
                        {
                            var value = GetReaderValue(reader, ordinal, prop.PropertyType);
                            prop.SetValue(child, value);
                        }
                    }
                    catch { }
                }

                childProp.SetValue(entity, child);
            }
        }
    }

    #endregion
}

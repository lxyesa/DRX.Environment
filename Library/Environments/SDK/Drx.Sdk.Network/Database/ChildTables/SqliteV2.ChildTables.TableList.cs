using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Data.Sqlite;

namespace Drx.Sdk.Network.DataBase;

/// <summary>
/// 高性能 Sqlite ORM - V2 版本 (Partial: TableList 子表处理)
/// 处理 TableList&lt;IDataTableV2&gt; 一对多子表的同步和加载
/// </summary>
public partial class SqliteV2<T> where T : class, IDataBase, new()
{
    #region TableList<IDataTableV2> 子表操作

    /// <summary>
    /// 同步 TableList 子表变更到数据库
    /// 在 Insert 时也需要调用此方法来确保新插入的主表能同步其 TableList 子表数据
    /// </summary>
    private void SyncTableListChanges(SqliteConnection connection, SqliteTransaction transaction, T entity)
    {
        foreach (var childListProp in _dataTableListProperties)
        {
            if (!IsTableList(childListProp.PropertyType))
                continue;

            var childList = childListProp.GetValue(entity);
            if (childList == null)
                continue;

            var syncMethod = childList.GetType().GetMethod("SyncChanges", 
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(SqliteConnection), typeof(SqliteTransaction) },
                null);

            syncMethod?.Invoke(childList, new object[] { connection, transaction });
        }
    }

    /// <summary>
    /// 从数据库加载 TableList&lt;IDataTableV2&gt; 类型的子表数据
    /// </summary>
    private void LoadTableListChildData(SqliteConnection connection, T entity)
    {
        foreach (var childListProp in _dataTableListProperties)
        {
            if (!IsTableList(childListProp.PropertyType))
                continue;

            var childType = GetDataTableListElementType(childListProp.PropertyType);
            var childTableName = $"{_tableName}_{childListProp.Name}";

            var listObj = Activator.CreateInstance(childListProp.PropertyType);
            var initMethod = childListProp.PropertyType.GetMethod("InitializeWithTableName", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            initMethod?.Invoke(listObj, new object[] { _connectionString, entity.Id, childTableName });

            var sql = $"SELECT * FROM [{childTableName}] WHERE ParentId = @parentId";
            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@parentId", entity.Id);

            using var reader = cmd.ExecuteReader();
            var childProps = childType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.Name != "TableName")
                .ToArray();

            InitializeChildOrdinals(reader, childProps);

            while (reader.Read())
            {
                var child = (object)Activator.CreateInstance(childType)!;
                
                try
                {
                    var idOrdinal = reader.GetOrdinal("Id");
                    if (!reader.IsDBNull(idOrdinal))
                    {
                        var idProp = childType.GetProperty("Id");
                        var idValue = reader.GetString(idOrdinal);
                        idProp?.SetValue(child, idValue);
                    }
                }
                catch { }

                try
                {
                    var parentIdOrdinal = reader.GetOrdinal("ParentId");
                    if (!reader.IsDBNull(parentIdOrdinal))
                    {
                        var parentIdProp = childType.GetProperty("ParentId");
                        var parentIdValue = reader.GetInt32(parentIdOrdinal);
                        parentIdProp?.SetValue(child, parentIdValue);
                    }
                }
                catch { }

                try
                {
                    var createdAtOrdinal = reader.GetOrdinal("CreatedAt");
                    if (!reader.IsDBNull(createdAtOrdinal))
                    {
                        var createdAtProp = childType.GetProperty("CreatedAt");
                        var createdAtValue = reader.GetInt64(createdAtOrdinal);
                        createdAtProp?.SetValue(child, createdAtValue);
                    }
                }
                catch { }

                try
                {
                    var updatedAtOrdinal = reader.GetOrdinal("UpdatedAt");
                    if (!reader.IsDBNull(updatedAtOrdinal))
                    {
                        var updatedAtProp = childType.GetProperty("UpdatedAt");
                        var updatedAtValue = reader.GetInt64(updatedAtOrdinal);
                        updatedAtProp?.SetValue(child, updatedAtValue);
                    }
                }
                catch { }

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

                var loadMethod = childListProp.PropertyType.GetMethod("LoadFromDatabase", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                loadMethod?.Invoke(listObj, new[] { child });
            }

            childListProp.SetValue(entity, listObj);
        }
    }

    #endregion
}

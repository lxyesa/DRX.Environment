using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Data.Sqlite;

namespace Drx.Sdk.Network.DataBase;

/// <summary>
/// 高性能 Sqlite ORM - V2 版本 (Partial: List 子表处理)
/// 处理 List&lt;IDataTable&gt; 一对多子表的插入和加载
/// </summary>
public partial class SqliteV2<T> where T : class, IDataBase, new()
{
    #region List<IDataTable> 子表操作

    /// <summary>
    /// 插入 List&lt;IDataTable&gt; 类型的子表数据
    /// </summary>
    private void InsertListChildren(SqliteConnection connection, SqliteTransaction transaction, T entity)
    {
        foreach (var childListProp in _dataTableListProperties)
        {
            if (IsTableList(childListProp.PropertyType))
                continue;

            var childList = (System.Collections.IList?)childListProp.GetValue(entity);
            if (childList == null || childList.Count == 0)
                continue;

            var childTableName = $"{_tableName}_{childListProp.Name}";
            var childType = GetDataTableListElementType(childListProp.PropertyType);
            var childProps = childType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.Name != "TableName" && p.Name != "Id" && p.Name != "ParentId")
                .ToArray();

            foreach (IDataTable child in childList)
            {
                InsertChildEntity(connection, transaction, child, entity.Id, childTableName, childProps);
            }
        }
    }

    /// <summary>
    /// 从数据库加载 List&lt;IDataTable&gt; 类型的子表数据
    /// </summary>
    private void LoadListChildData(SqliteConnection connection, T entity)
    {
        foreach (var childListProp in _dataTableListProperties)
        {
            if (IsTableList(childListProp.PropertyType))
                continue;

            var childType = GetDataTableListElementType(childListProp.PropertyType);
            var childTableName = $"{_tableName}_{childListProp.Name}";

            var listObj = Activator.CreateInstance(childListProp.PropertyType);
            var addMethod = childListProp.PropertyType.GetMethod("Add");

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
                        var idValue = reader.GetInt32(idOrdinal);
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

                addMethod?.Invoke(listObj, new[] { child });
            }

            childListProp.SetValue(entity, listObj);
        }
    }

    #endregion
}

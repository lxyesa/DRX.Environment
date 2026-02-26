using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Drx.Sdk.Network.DataBase;

/// <summary>
/// 高性能 Sqlite ORM - V2 版本 (Partial: 工具方法)
/// 处理反射缓存、参数绑定、SQL 生成、类型转换等底层优化
/// </summary>
public partial class SqliteV2<T> where T : class, IDataBase, new()
{
    #region 工具方法

    /// <summary>
    /// 绑定命令参数
    /// </summary>
    private void BindParameters(SqliteCommand cmd, T entity)
    {
        foreach (var prop in _simpleProperties)
        {
            var value = GetPropertyValue(entity, prop);
            var param = cmd.Parameters[$"@{prop.Name}"];
            param.Value = value ?? DBNull.Value;
        }
    }

    /// <summary>
    /// 高效获取属性值（使用表达式树缓存）
    /// </summary>
    private object? GetPropertyValue(T entity, PropertyInfo prop)
    {
        var getter = GetterCache.GetOrAdd(prop, p => 
        {
            var paramExpr = Expression.Parameter(typeof(object));
            var objExpr = Expression.Convert(paramExpr, _entityType);
            var memberExpr = Expression.MakeMemberAccess(objExpr, p);
            var convertExpr = Expression.Convert(memberExpr, typeof(object));
            return Expression.Lambda<Func<object, object?>>(convertExpr, paramExpr).Compile();
        });

        return getter(entity);
    }

    /// <summary>
    /// 高效设置属性值（使用表达式树缓存）
    /// </summary>
    private void SetPropertyValue(T entity, PropertyInfo prop, object? value)
    {
        var setter = SetterCache.GetOrAdd(prop, p =>
        {
            var objParam = Expression.Parameter(typeof(object));
            var valueParam = Expression.Parameter(typeof(object));
            var objExpr = Expression.Convert(objParam, _entityType);
            var valueExpr = Expression.Convert(valueParam, p.PropertyType);
            var assignExpr = Expression.Assign(
                Expression.MakeMemberAccess(objExpr, p),
                valueExpr);
            return Expression.Lambda<Action<object, object?>>(assignExpr, objParam, valueParam).Compile();
        });

        setter(entity, value);
    }

    /// <summary>
    /// 从 SqliteDataReader 读取值并转换为目标类型
    /// </summary>
    private object GetReaderValue(SqliteDataReader reader, int ordinal, Type targetType)
    {
        if (targetType == typeof(int))
            return reader.GetInt32(ordinal);
        if (targetType == typeof(long))
            return reader.GetInt64(ordinal);
        if (targetType == typeof(bool))
            return reader.GetBoolean(ordinal);
        if (targetType == typeof(double))
            return reader.GetDouble(ordinal);
        if (targetType == typeof(float))
            return reader.GetFloat(ordinal);
        if (targetType == typeof(string))
            return reader.GetString(ordinal);
        if (targetType == typeof(DateTime))
            return reader.GetDateTime(ordinal);
        if (targetType == typeof(decimal))
            return reader.GetDecimal(ordinal);
        if (targetType == typeof(byte[]))
            return (byte[])reader.GetValue(ordinal);

        // 处理 Enum 类型：数据库存储为整数或字符串，需要转换为对应的 enum 值
        if (targetType.IsEnum)
        {
            var rawValue = reader.GetValue(ordinal);
            if (rawValue == null || rawValue is DBNull)
                return Enum.GetValues(targetType).GetValue(0) ?? 0; // 返回默认值
            
            // 尝试从整数值转换
            if (rawValue is int intVal)
                return Enum.ToObject(targetType, intVal);
            
            // 尝试从字符串值转换
            if (rawValue is string strVal)
                return Enum.Parse(targetType, strVal, ignoreCase: true);
            
            // 尝试从 long 转换
            if (rawValue is long longVal)
                return Enum.ToObject(targetType, longVal);
            
            return Enum.ToObject(targetType, Convert.ToInt32(rawValue));
        }

        return reader.GetValue(ordinal);
    }

    /// <summary>
    /// 从 SqliteDataReader 映射实体对象
    /// </summary>
    private T MapFromReader(SqliteDataReader reader)
    {
        var entity = new T();
        entity.Id = reader.GetInt32(_columnMapping.ColumnOrdinals["Id"]);

        foreach (var prop in _simpleProperties)
        {
            if (!_columnMapping.ColumnOrdinals.TryGetValue(prop.Name, out var ordinal))
                continue;

            if (reader.IsDBNull(ordinal))
                continue;

            var value = GetReaderValue(reader, ordinal, prop.PropertyType);
            SetPropertyValue(entity, prop, value);
        }

        // 加载子表数据
        LoadChildDataSync(entity);

        return entity;
    }

    /// <summary>
    /// 初始化列序号映射
    /// </summary>
    private void InitializeOrdinals(SqliteDataReader reader)
    {
        if (_columnMapping.ColumnOrdinals.Count > 0)
            return;

        lock (_lockObj)
        {
            if (_columnMapping.ColumnOrdinals.Count > 0)
                return;

            _columnMapping.ColumnOrdinals["Id"] = reader.GetOrdinal("Id");
            foreach (var prop in _simpleProperties)
            {
                try
                {
                    _columnMapping.ColumnOrdinals[prop.Name] = reader.GetOrdinal(prop.Name);
                }
                catch
                {
                    // 列不存在，跳过
                }
            }
        }
    }

    /// <summary>
    /// 内部插入方法（供同步和异步调用）
    /// </summary>
    private void InsertInternal(SqliteConnection connection, SqliteTransaction transaction, T entity)
    {
        var sql = _sqlCache["INSERT"];
        using var cmd = new SqliteCommand(sql, connection, transaction);

        foreach (var prop in _simpleProperties)
        {
            var value = GetPropertyValue(entity, prop);
            cmd.Parameters.AddWithValue($"@{prop.Name}", value ?? DBNull.Value);
        }

        cmd.ExecuteNonQuery();
        
        // 获取新插入的 ID
        using var idCmd = new SqliteCommand("SELECT last_insert_rowid()", connection, transaction);
        var newId = (long)idCmd.ExecuteScalar()!;
        entity.Id = (int)newId;
        
        // 插入 List<IDataTable> 子表数据
        InsertChildTablesSync(connection, transaction, entity);
    }

    /// <summary>
    /// 构建 INSERT SQL 语句
    /// </summary>
    private string BuildInsertSql()
    {
        var columns = string.Join(",", _simpleProperties.Select(p => $"[{p.Name}]"));
        var values = string.Join(",", _simpleProperties.Select(p => $"@{p.Name}"));
        return $"INSERT INTO [{_tableName}] ({columns}) VALUES ({values})";
    }

    /// <summary>
    /// 构建 UPDATE SQL 语句
    /// </summary>
    private string BuildUpdateSql()
    {
        var sets = string.Join(",", _simpleProperties.Select(p => $"[{p.Name}] = @{p.Name}"));
        return $"UPDATE [{_tableName}] SET {sets} WHERE Id = @id";
    }

    /// <summary>
    /// 将 .NET 类型映射到 SQLite 类型
    /// </summary>
    private string GetSqliteType(Type type)
    {
        if (type == typeof(int) || type == typeof(long) || type == typeof(bool))
            return "INTEGER";
        if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
            return "REAL";
        if (type == typeof(byte[]))
            return "BLOB";
        return "TEXT";
    }

    /// <summary>
    /// 判断是否为简单类型（可直接存储在数据库）
    /// </summary>
    private bool IsSimpleType(Type type)
    {
        return type == typeof(int) || type == typeof(long) || type == typeof(bool) ||
               type == typeof(double) || type == typeof(float) || type == typeof(decimal) ||
               type == typeof(string) || type == typeof(DateTime) || type == typeof(byte[]) ||
               type.IsValueType;
    }

    /// <summary>
    /// 判断是否为 List<IDataTable> 类型
    /// </summary>
    private bool IsDataTableList(Type type)
    {
        if (!type.IsGenericType)
            return false;

        var genericDef = type.GetGenericTypeDefinition();
        if (genericDef != typeof(List<>))
            return false;

        var elementType = type.GetGenericArguments()[0];
        return typeof(IDataTable).IsAssignableFrom(elementType);
    }

    /// <summary>
    /// 判断是否为 TableList<IDataTableV2> 类型
    /// </summary>
    private bool IsTableList(Type type)
    {
        if (!type.IsGenericType)
            return false;

        var genericDef = type.GetGenericTypeDefinition();
        if (genericDef.Name != "TableList`1")
            return false;

        var elementType = type.GetGenericArguments()[0];
        return typeof(IDataTableV2).IsAssignableFrom(elementType);
    }

    /// <summary>
    /// 获取 List<T> 或 TableList<T> 的元素类型
    /// </summary>
    private Type GetDataTableListElementType(Type listType)
    {
        return listType.GetGenericArguments()[0];
    }

    #endregion
}

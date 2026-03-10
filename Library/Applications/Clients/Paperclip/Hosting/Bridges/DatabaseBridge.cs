// Copyright (c) DRX SDK — Paperclip SQLite 原始查询桥接层
// 职责：将 SQLite 数据库的原始 SQL 操作导出到 JS/TS 脚本
// 关键依赖：Microsoft.Data.Sqlite

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace DrxPaperclip.Hosting;

/// <summary>
/// SQLite 数据库脚本桥接层。提供原始 SQL 查询能力，返回动态对象数组供脚本消费。
/// 脚本使用方式：
/// <code>
/// const db = Database.open("data.db");
/// Database.execute(db, "CREATE TABLE users(id INTEGER PRIMARY KEY, name TEXT)");
/// Database.execute(db, "INSERT INTO users(name) VALUES(@name)", { name: "Alice" });
/// const rows = Database.query(db, "SELECT * FROM users");
/// Database.close(db);
/// </code>
/// </summary>
public static class DatabaseBridge
{
    /// <summary>
    /// 打开或创建 SQLite 数据库，返回连接字符串作为句柄。
    /// </summary>
    public static string open(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("databasePath 不能为空。", nameof(databasePath));

        var fullPath = Path.GetFullPath(databasePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var connStr = $"Data Source={fullPath}";

        // 验证连接可用
        using var conn = new SqliteConnection(connStr);
        conn.Open();

        return connStr;
    }

    /// <summary>
    /// 执行非查询 SQL（INSERT/UPDATE/DELETE/DDL），返回受影响行数。
    /// </summary>
    public static int execute(string connectionString, string sql, object? parameters = null)
    {
        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        BindParameters(cmd, parameters);
        return cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 执行查询 SQL，返回动态对象数组。
    /// </summary>
    public static object[] query(string connectionString, string sql, object? parameters = null)
    {
        var results = new List<object>();

        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        BindParameters(cmd, parameters);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var row = new ExpandoObject() as IDictionary<string, object?>;
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                row[reader.GetName(i)] = value;
            }
            results.Add(row);
        }

        return results.ToArray();
    }

    /// <summary>
    /// 查询单个标量值。
    /// </summary>
    public static object? scalar(string connectionString, string sql, object? parameters = null)
    {
        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        BindParameters(cmd, parameters);
        var result = cmd.ExecuteScalar();
        return result == DBNull.Value ? null : result;
    }

    /// <summary>
    /// 在事务中执行多条 SQL。
    /// </summary>
    public static void transaction(string connectionString, string[] sqlStatements)
    {
        if (sqlStatements == null || sqlStatements.Length == 0) return;

        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            foreach (var sql in sqlStatements)
            {
                if (string.IsNullOrWhiteSpace(sql)) continue;
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                cmd.Transaction = tx;
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    /// <summary>
    /// 获取所有表名。
    /// </summary>
    public static string[] tables(string connectionString)
    {
        var result = new List<string>();
        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(reader.GetString(0));
        }
        return result.ToArray();
    }

    /// <summary>
    /// 关闭连接（清理连接池缓存）。
    /// </summary>
    public static void close(string connectionString)
    {
        SqliteConnection.ClearPool(new SqliteConnection(connectionString));
    }

    #region 便利查询

    /// <summary>
    /// 查询单行，无结果返回 null。
    /// </summary>
    public static object? queryOne(string connectionString, string sql, object? parameters = null)
    {
        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        BindParameters(cmd, parameters);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        var row = new ExpandoObject() as IDictionary<string, object?>;
        for (var i = 0; i < reader.FieldCount; i++)
        {
            row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }
        return row;
    }

    /// <summary>
    /// 快速计数。where 为空则统计全表。
    /// </summary>
    public static long count(string connectionString, string table, string? where = null, object? parameters = null)
    {
        ValidateTableName(table);
        var sql = $"SELECT COUNT(*) FROM [{table}]";
        if (!string.IsNullOrWhiteSpace(where))
            sql += $" WHERE {where}";

        var result = scalar(connectionString, sql, parameters);
        return result is long l ? l : Convert.ToInt64(result);
    }

    /// <summary>
    /// 行是否存在。
    /// </summary>
    public static bool exists(string connectionString, string table, string where, object? parameters = null)
    {
        ValidateTableName(table);
        if (string.IsNullOrWhiteSpace(where))
            throw new ArgumentException("where 条件不能为空。", nameof(where));

        var sql = $"SELECT 1 FROM [{table}] WHERE {where} LIMIT 1";
        return scalar(connectionString, sql, parameters) != null;
    }

    #endregion

    #region 便利 CRUD

    /// <summary>
    /// 对象直接插入（键值对自动生成 INSERT 语句），返回 last_insert_rowid。
    /// </summary>
    public static long insert(string connectionString, string table, object data)
    {
        ValidateTableName(table);
        if (data == null) throw new ArgumentNullException(nameof(data));

        var dict = ToDictionary(data);
        if (dict.Count == 0) throw new ArgumentException("data 不能为空对象。", nameof(data));

        var columns = string.Join(",", dict.Keys.Select(k => $"[{k}]"));
        var paramNames = string.Join(",", dict.Keys.Select(k => $"@{k}"));
        var sql = $"INSERT INTO [{table}] ({columns}) VALUES ({paramNames}); SELECT last_insert_rowid();";

        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var kvp in dict)
        {
            cmd.Parameters.AddWithValue($"@{kvp.Key}", kvp.Value ?? DBNull.Value);
        }
        var result = cmd.ExecuteScalar();
        return result is long l ? l : Convert.ToInt64(result);
    }

    /// <summary>
    /// 批量插入，在单个事务内执行，返回受影响总行数。
    /// </summary>
    public static int insertBatch(string connectionString, string table, object[] items)
    {
        ValidateTableName(table);
        if (items == null || items.Length == 0) return 0;

        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            int total = 0;
            foreach (var item in items)
            {
                var dict = ToDictionary(item);
                if (dict.Count == 0) continue;

                var columns = string.Join(",", dict.Keys.Select(k => $"[{k}]"));
                var paramNames = string.Join(",", dict.Keys.Select(k => $"@{k}"));

                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"INSERT INTO [{table}] ({columns}) VALUES ({paramNames})";
                cmd.Transaction = tx;
                foreach (var kvp in dict)
                {
                    cmd.Parameters.AddWithValue($"@{kvp.Key}", kvp.Value ?? DBNull.Value);
                }
                total += cmd.ExecuteNonQuery();
            }
            tx.Commit();
            return total;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    /// <summary>
    /// 对象更新（data 中的键值对生成 SET 子句）。
    /// </summary>
    public static int update(string connectionString, string table, object data, string where, object? parameters = null)
    {
        ValidateTableName(table);
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (string.IsNullOrWhiteSpace(where))
            throw new ArgumentException("where 条件不能为空。", nameof(where));

        var dataDict = ToDictionary(data);
        if (dataDict.Count == 0) throw new ArgumentException("data 不能为空对象。", nameof(data));

        // 生成 SET data_col = @__d_col 避免与 where 参数冲突
        var setClauses = string.Join(",", dataDict.Keys.Select(k => $"[{k}] = @__d_{k}"));
        var sql = $"UPDATE [{table}] SET {setClauses} WHERE {where}";

        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;

        foreach (var kvp in dataDict)
        {
            cmd.Parameters.AddWithValue($"@__d_{kvp.Key}", kvp.Value ?? DBNull.Value);
        }
        BindParameters(cmd, parameters);

        return cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// 按条件删除行。
    /// </summary>
    public static int deleteWhere(string connectionString, string table, string where, object? parameters = null)
    {
        ValidateTableName(table);
        if (string.IsNullOrWhiteSpace(where))
            throw new ArgumentException("where 条件不能为空，使用 execute 执行无条件删除。", nameof(where));

        var sql = $"DELETE FROM [{table}] WHERE {where}";
        return execute(connectionString, sql, parameters);
    }

    /// <summary>
    /// INSERT OR REPLACE（SQLite UPSERT）。conflictColumns 为冲突判定列名数组。
    /// </summary>
    public static long upsert(string connectionString, string table, object data, string[] conflictColumns)
    {
        ValidateTableName(table);
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (conflictColumns == null || conflictColumns.Length == 0)
            throw new ArgumentException("conflictColumns 不能为空。", nameof(conflictColumns));

        var dict = ToDictionary(data);
        if (dict.Count == 0) throw new ArgumentException("data 不能为空对象。", nameof(data));

        var columns = string.Join(",", dict.Keys.Select(k => $"[{k}]"));
        var paramNames = string.Join(",", dict.Keys.Select(k => $"@{k}"));
        var conflictCols = string.Join(",", conflictColumns.Select(c => $"[{c}]"));

        // 排除冲突列后生成 UPDATE SET 子句
        var updateKeys = dict.Keys.Where(k => !conflictColumns.Contains(k, StringComparer.OrdinalIgnoreCase)).ToList();
        var updateClause = updateKeys.Count > 0
            ? string.Join(",", updateKeys.Select(k => $"[{k}] = excluded.[{k}]"))
            : string.Join(",", dict.Keys.Select(k => $"[{k}] = excluded.[{k}]"));

        var sql = $"INSERT INTO [{table}] ({columns}) VALUES ({paramNames}) ON CONFLICT({conflictCols}) DO UPDATE SET {updateClause}; SELECT last_insert_rowid();";

        using var conn = new SqliteConnection(connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var kvp in dict)
        {
            cmd.Parameters.AddWithValue($"@{kvp.Key}", kvp.Value ?? DBNull.Value);
        }
        var result = cmd.ExecuteScalar();
        return result is long l ? l : Convert.ToInt64(result);
    }

    #endregion

    #region DDL 便利方法

    /// <summary>
    /// 获取表的列信息。
    /// </summary>
    public static object[] columns(string connectionString, string table)
    {
        ValidateTableName(table);
        return query(connectionString, $"PRAGMA table_info([{table}])");
    }

    /// <summary>
    /// 便捷建表。columns 为 { name: string, type: string, primaryKey?: bool, notNull?: bool, defaultValue?: any } 对象数组。
    /// </summary>
    public static void createTable(string connectionString, string table, object[] columnDefs)
    {
        ValidateTableName(table);
        if (columnDefs == null || columnDefs.Length == 0)
            throw new ArgumentException("至少需要一个列定义。", nameof(columnDefs));

        var colSqls = new List<string>();
        foreach (var colDef in columnDefs)
        {
            var dict = ToDictionary(colDef);
            if (!dict.TryGetValue("name", out var nameObj) || nameObj is not string name || string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("列定义必须包含 name 字段。");

            ValidateIdentifier(name);
            var type = dict.TryGetValue("type", out var typeObj) && typeObj is string t ? t : "TEXT";
            var sb = new System.Text.StringBuilder($"[{name}] {type}");

            if (dict.TryGetValue("primaryKey", out var pk) && IsTrue(pk))
                sb.Append(" PRIMARY KEY");
            if (dict.TryGetValue("notNull", out var nn) && IsTrue(nn))
                sb.Append(" NOT NULL");
            if (dict.TryGetValue("defaultValue", out var dv) && dv != null)
                sb.Append($" DEFAULT {FormatDefault(dv)}");

            colSqls.Add(sb.ToString());
        }

        var sql = $"CREATE TABLE IF NOT EXISTS [{table}] ({string.Join(", ", colSqls)})";
        execute(connectionString, sql);
    }

    /// <summary>
    /// 删除表。
    /// </summary>
    public static void dropTable(string connectionString, string table)
    {
        ValidateTableName(table);
        execute(connectionString, $"DROP TABLE IF EXISTS [{table}]");
    }

    /// <summary>
    /// 为表添加列。
    /// </summary>
    public static void addColumn(string connectionString, string table, string columnName, string columnType, object? defaultValue = null)
    {
        ValidateTableName(table);
        ValidateIdentifier(columnName);
        var sql = $"ALTER TABLE [{table}] ADD COLUMN [{columnName}] {columnType}";
        if (defaultValue != null)
            sql += $" DEFAULT {FormatDefault(defaultValue)}";
        execute(connectionString, sql);
    }

    /// <summary>
    /// 创建索引。
    /// </summary>
    public static void createIndex(string connectionString, string table, string[] columnNames, bool unique = false)
    {
        ValidateTableName(table);
        if (columnNames == null || columnNames.Length == 0)
            throw new ArgumentException("至少需要一个列名。", nameof(columnNames));

        foreach (var col in columnNames) ValidateIdentifier(col);
        var indexName = $"idx_{table}_{string.Join("_", columnNames)}";
        var uniqueStr = unique ? "UNIQUE " : "";
        var cols = string.Join(",", columnNames.Select(c => $"[{c}]"));
        execute(connectionString, $"CREATE {uniqueStr}INDEX IF NOT EXISTS [{indexName}] ON [{table}] ({cols})");
    }

    #endregion

    #region 参数绑定

    /// <summary>
    /// 将脚本对象（JS 对象 / ExpandoObject / IDictionary）绑定为 SQL 参数。
    /// 参数名以 @ 前缀自动补全。
    /// </summary>
    private static void BindParameters(SqliteCommand cmd, object? parameters)
    {
        if (parameters is null) return;

        if (parameters is IDictionary<string, object?> dict)
        {
            foreach (var kvp in dict)
            {
                var paramName = kvp.Key.StartsWith('@') ? kvp.Key : "@" + kvp.Key;
                cmd.Parameters.AddWithValue(paramName, kvp.Value ?? DBNull.Value);
            }
            return;
        }

        if (parameters is System.Collections.IDictionary rawDict)
        {
            foreach (var key in rawDict.Keys)
            {
                var k = key?.ToString();
                if (string.IsNullOrEmpty(k)) continue;
                var paramName = k.StartsWith('@') ? k : "@" + k;
                cmd.Parameters.AddWithValue(paramName, rawDict[key!] ?? DBNull.Value);
            }
        }
    }

    #endregion

    #region 内部辅助

    /// <summary>
    /// 验证表名安全性（仅允许字母、数字、下划线）。
    /// </summary>
    private static void ValidateTableName(string table)
    {
        if (string.IsNullOrWhiteSpace(table))
            throw new ArgumentException("表名不能为空。", nameof(table));
        if (!System.Text.RegularExpressions.Regex.IsMatch(table, @"^[a-zA-Z_]\w{0,127}$"))
            throw new ArgumentException($"表名 '{table}' 包含非法字符。", nameof(table));
    }

    /// <summary>
    /// 验证标识符安全性。
    /// </summary>
    private static void ValidateIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("标识符不能为空。");
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-zA-Z_]\w{0,127}$"))
            throw new ArgumentException($"标识符 '{name}' 包含非法字符。");
    }

    /// <summary>
    /// 将脚本对象转为字典。
    /// </summary>
    private static Dictionary<string, object?> ToDictionary(object obj)
    {
        if (obj is IDictionary<string, object?> d)
            return new Dictionary<string, object?>(d, StringComparer.Ordinal);

        if (obj is System.Collections.IDictionary rawDict)
        {
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var key in rawDict.Keys)
            {
                var k = key?.ToString();
                if (!string.IsNullOrEmpty(k))
                    result[k] = rawDict[key!];
            }
            return result;
        }

        // 反射 fallback（C# 匿名类型等）
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var prop in obj.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (!prop.CanRead) continue;
            dict[prop.Name] = prop.GetValue(obj);
        }
        return dict;
    }

    /// <summary>
    /// 布尔值判定辅助。
    /// </summary>
    private static bool IsTrue(object? value)
    {
        if (value is bool b) return b;
        if (value is int i) return i != 0;
        if (value is long l) return l != 0;
        if (value is string s) return s.Equals("true", StringComparison.OrdinalIgnoreCase);
        return false;
    }

    /// <summary>
    /// SQL 默认值格式化。
    /// </summary>
    private static string FormatDefault(object value)
    {
        if (value is string s) return $"'{s.Replace("'", "''")}'";
        if (value is bool b) return b ? "1" : "0";
        return value.ToString() ?? "NULL";
    }

    #endregion
}

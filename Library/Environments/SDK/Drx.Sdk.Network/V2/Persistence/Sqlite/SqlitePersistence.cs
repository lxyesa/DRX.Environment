using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;

namespace Drx.Sdk.Network.V2.Persistence.Sqlite;

/// <summary>
/// SQLite 持久化，依赖Microsoft.Data.Sqlite
/// </summary>
public class SqlitePersistence : IDisposable
{
    private readonly string _databasePath;
    private readonly bool _readOnly;
    private readonly int _cacheSize;
    private SqliteConnection? _connection;
    private bool _disposed = false;

    // 简单内存缓存
    private readonly ConcurrentDictionary<string, object?> _cache = new();
    private readonly object _lock = new object();

    public SqlitePersistence(string databasePath, bool readOnly = false, bool createIfNotExists = true, int cacheSize = 1024)
    {
        _databasePath = databasePath ?? throw new ArgumentNullException(nameof(databasePath));
        _readOnly = readOnly;
        _cacheSize = cacheSize;

        InitializeDatabase(createIfNotExists);
    }

    private void InitializeDatabase(bool createIfNotExists)
    {
        var connectionStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = _readOnly ? SqliteOpenMode.ReadOnly :
                   createIfNotExists ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadWrite
        };

        _connection = new SqliteConnection(connectionStringBuilder.ToString());
        _connection.Open();

        // 配置 SQLite PRAGMA
        if (!_readOnly)
        {
            ExecuteNonQuery("PRAGMA journal_mode=WAL;");
            ExecuteNonQuery("PRAGMA synchronous=NORMAL;");
            ExecuteNonQuery($"PRAGMA cache_size=-{_cacheSize};"); // 负数表示KB
            ExecuteNonQuery("PRAGMA foreign_keys=ON;");
            ExecuteNonQuery("PRAGMA temp_store=MEMORY;");
        }

        // 创建主键值表
        if (!_readOnly)
        {
            ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS KeyValue (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TableName TEXT NOT NULL,
                    Key TEXT NOT NULL,
                    Type TEXT NOT NULL,
                    Value BLOB,
                    CreatedAt INTEGER NOT NULL,
                    UpdatedAt INTEGER NOT NULL,
                    UNIQUE(TableName, Key)
                );");

            ExecuteNonQuery("CREATE INDEX IF NOT EXISTS idx_table_key ON KeyValue(TableName, Key);");
        }
    }

    private void ExecuteNonQuery(string sql, params SqliteParameter[] parameters)
    {
        ThrowIfDisposed();
        using var command = _connection!.CreateCommand();
        command.CommandText = sql;
        if (parameters != null)
            command.Parameters.AddRange(parameters);
        command.ExecuteNonQuery();
    }

    private object? ExecuteScalar(string sql, params SqliteParameter[] parameters)
    {
        ThrowIfDisposed();
        using var command = _connection!.CreateCommand();
        command.CommandText = sql;
        if (parameters != null)
            command.Parameters.AddRange(parameters);
        return command.ExecuteScalar();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SqlitePersistence));
    }

    private void ThrowIfReadOnly()
    {
        if (_readOnly)
            throw new InvalidOperationException("数据库处于只读模式，无法执行写操作");
    }

    private string GetCacheKey(string tableName, string key) => $"{tableName}:{key}";

    /// <summary>
    /// 将 SQL 标识符安全转义（用于表名或列名），通过将内部的双引号重复两次。
    /// 返回可安全插入到 SQL 中的未包裹标识符片段（不包含外层双引号）。
    /// 使用时请用 `"{EscapeIdentifier(name)}"` 将其包裹为标识符。
    /// </summary>
    private static string EscapeIdentifier(string identifier)
    {
        if (identifier == null) return string.Empty;
        // 将内部的双引号替换为两个双引号，符合 SQLite 标识符转义规则
        return identifier.Replace("\"", "\"\"");
    }

    // --------------------------------------------------------------------------------
    // 表操作
    // --------------------------------------------------------------------------------

    /// <summary>
    /// 创建逻辑表（实际上是在 KeyValue 表中标记命名空间）
    /// </summary>
    public bool CreateTable(string tableName)
    {
        ThrowIfDisposed();
        ThrowIfReadOnly();

        if (string.IsNullOrEmpty(tableName))
            throw new ArgumentException("表名不能为空", nameof(tableName));

        // 在单表模式下，创建表就是插入一个元数据记录
        try
        {
            using var transaction = _connection!.BeginTransaction();

            var exists = ExecuteScalar(
                "SELECT COUNT(*) FROM KeyValue WHERE TableName = @table AND Key = '__TABLE_META__'",
                new SqliteParameter("@table", tableName));

            if (Convert.ToInt32(exists) > 0)
                return false; // 表已存在

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            ExecuteNonQuery(@"
                INSERT INTO KeyValue (TableName, Key, Type, Value, CreatedAt, UpdatedAt)
                VALUES (@table, '__TABLE_META__', 'meta', NULL, @now, @now)",
                new SqliteParameter("@table", tableName),
                new SqliteParameter("@now", now));

            transaction.Commit();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 删除逻辑表及其所有数据
    /// </summary>
    public bool DeleteTable(string tableName)
    {
        ThrowIfDisposed();
        ThrowIfReadOnly();

        if (string.IsNullOrEmpty(tableName))
            return false;

        try
        {
            using var transaction = _connection!.BeginTransaction();

            ExecuteNonQuery(
                "DELETE FROM KeyValue WHERE TableName = @table",
                new SqliteParameter("@table", tableName));

            // 清理缓存中相关的条目
            var keysToRemove = new List<string>();
            foreach (var cacheKey in _cache.Keys)
            {
                if (cacheKey.StartsWith($"{tableName}:"))
                    keysToRemove.Add(cacheKey);
            }

            foreach (var key in keysToRemove)
                _cache.TryRemove(key, out _);

            transaction.Commit();
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ----------------------------------------------------------------------------------
    // 键值对操作
    // ----------------------------------------------------------------------------------

    /// <summary>
    /// 读取字符串值
    /// </summary>
    public string? ReadString(string tableName, string key)
    {
        return ReadValue<string>(tableName, key, "string");
    }

    /// <summary>
    /// 写入字符串值
    /// </summary>
    public bool WriteString(string tableName, string key, string value)
    {
        return WriteValue(tableName, key, value, "string");
    }

    /// <summary>
    /// 更新字符串值
    /// </summary>
    public bool UpdateString(string tableName, string key, string value)
    {
        return UpdateValue(tableName, key, value, "string");
    }

    /// <summary>
    /// 读取 Int32 值
    /// </summary>
    public int? ReadInt32(string tableName, string key)
    {
        return ReadValue<int?>(tableName, key, "int32");
    }

    /// <summary>
    /// 写入 Int32 值
    /// </summary>
    public bool WriteInt32(string tableName, string key, int value)
    {
        return WriteValue(tableName, key, value, "int32");
    }

    /// <summary>
    /// 更新 Int32 值
    /// </summary>
    public bool UpdateInt32(string tableName, string key, int value)
    {
        return UpdateValue(tableName, key, value, "int32");
    }

    /// <summary>
    /// 读取 Int64 值
    /// </summary>
    public long? ReadInt64(string tableName, string key)
    {
        return ReadValue<long?>(tableName, key, "int64");
    }

    /// <summary>
    /// 写入 Int64 值
    /// </summary>
    public bool WriteInt64(string tableName, string key, long value)
    {
        return WriteValue(tableName, key, value, "int64");
    }

    /// <summary>
    /// 更新 Int64 值
    /// </summary>
    public bool UpdateInt64(string tableName, string key, long value)
    {
        return UpdateValue(tableName, key, value, "int64");
    }

    /// <summary>
    /// 读取 Double 值
    /// </summary>
    public double? ReadDouble(string tableName, string key)
    {
        return ReadValue<double?>(tableName, key, "double");
    }

    /// <summary>
    /// 写入 Double 值
    /// </summary>
    public bool WriteDouble(string tableName, string key, double value)
    {
        return WriteValue(tableName, key, value, "double");
    }

    /// <summary>
    /// 更新 Double 值
    /// </summary>
    public bool UpdateDouble(string tableName, string key, double value)
    {
        return UpdateValue(tableName, key, value, "double");
    }

    /// <summary>
    /// 读取 Float 值
    /// </summary>
    public float? ReadFloat(string tableName, string key)
    {
        return ReadValue<float?>(tableName, key, "float");
    }

    /// <summary>
    /// 写入 Float 值
    /// </summary>
    public bool WriteFloat(string tableName, string key, float value)
    {
        return WriteValue(tableName, key, value, "float");
    }

    /// <summary>
    /// 更新 Float 值
    /// </summary>
    public bool UpdateFloat(string tableName, string key, float value)
    {
        return UpdateValue(tableName, key, value, "float");
    }

    /// <summary>
    /// 读取 Bool 值
    /// </summary>
    public bool? ReadBool(string tableName, string key)
    {
        return ReadValue<bool?>(tableName, key, "bool");
    }

    /// <summary>
    /// 写入 Bool 值
    /// </summary>
    public bool WriteBool(string tableName, string key, bool value)
    {
        return WriteValue(tableName, key, value, "bool");
    }

    /// <summary>
    /// 更新 Bool 值
    /// </summary>
    public bool UpdateBool(string tableName, string key, bool value)
    {
        return UpdateValue(tableName, key, value, "bool");
    }

    /// <summary>
    /// 读取字节数组
    /// </summary>
    public byte[]? ReadBytes(string tableName, string key)
    {
        return ReadValue<byte[]>(tableName, key, "bytes");
    }

    /// <summary>
    /// 写入字节数组
    /// </summary>
    public bool WriteBytes(string tableName, string key, byte[] value)
    {
        return WriteValue(tableName, key, value, "bytes");
    }

    /// <summary>
    /// 更新字节数组
    /// </summary>
    public bool UpdateBytes(string tableName, string key, byte[] value)
    {
        return UpdateValue(tableName, key, value, "bytes");
    }

    /// <summary>
    /// 检查键是否存在
    /// </summary>
    public bool KeyExists(string tableName, string key)
    {
        ThrowIfDisposed();

        var cacheKey = GetCacheKey(tableName, key);
        if (_cache.ContainsKey(cacheKey))
            return true;

        var result = ExecuteScalar(
            "SELECT COUNT(*) FROM KeyValue WHERE TableName = @table AND Key = @key",
            new SqliteParameter("@table", tableName),
            new SqliteParameter("@key", key));

        return Convert.ToInt32(result) > 0;
    }

    /// <summary>
    /// 删除键
    /// </summary>
    public bool RemoveKey(string tableName, string key)
    {
        ThrowIfDisposed();
        ThrowIfReadOnly();

        try
        {
            using var transaction = _connection!.BeginTransaction();

            ExecuteNonQuery(
                "DELETE FROM KeyValue WHERE TableName = @table AND Key = @key",
                new SqliteParameter("@table", tableName),
                new SqliteParameter("@key", key));

            var cacheKey = GetCacheKey(tableName, key);
            _cache.TryRemove(cacheKey, out _);

            transaction.Commit();
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ----------------------------------------------------------------------------------
    // 复合数据操作
    // ----------------------------------------------------------------------------------

    /// <summary>
    /// 写入复合数据
    /// </summary>
    public bool WriteComposite(string tableName, string key, Func<CompositeBuilder, CompositeBuilder> buildAction)
    {
        ThrowIfDisposed();
        ThrowIfReadOnly();

        try
        {
            var builder = new CompositeBuilder(tableName, key);
            builder = buildAction(builder);
            var data = builder.Build();

            return WriteValue(tableName, key, data, "composite");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 读取复合数据
    /// </summary>
    public CompositeBuilder? ReadComposite(string tableName, string key)
    {
        var data = ReadValue<byte[]>(tableName, key, "composite");
        if (data == null) return null;

        return CompositeBuilder.Parse(data, tableName, key);
    }

    /// <summary>
    /// 删除复合数据
    /// </summary>
    public bool RemoveComposite(string tableName, string key)
    {
        return RemoveKey(tableName, key);
    }

    /// <summary>
    /// 检查复合数据是否存在
    /// </summary>
    public bool CompositeExists(string tableName, string key)
    {
        ThrowIfDisposed();

        var result = ExecuteScalar(
            "SELECT COUNT(*) FROM KeyValue WHERE TableName = @table AND Key = @key AND Type = 'composite'",
            new SqliteParameter("@table", tableName),
            new SqliteParameter("@key", key));

        return Convert.ToInt32(result) > 0;
    }

    /// <summary>
    /// 列出表中所有键
    /// </summary>
    public List<string> ListKeys(string tableName)
    {
        ThrowIfDisposed();

        var keys = new List<string>();
        using var command = _connection!.CreateCommand();
        command.CommandText = "SELECT Key FROM KeyValue WHERE TableName = @table AND Key != '__TABLE_META__'";
        command.Parameters.AddWithValue("@table", tableName);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            keys.Add(reader.GetString(0));
        }

        return keys;
    }

    // ----------------------------------------------------------------------------------
    // 通用读写方法
    // ----------------------------------------------------------------------------------

    private T? ReadValue<T>(string tableName, string key, string expectedType)
    {
        ThrowIfDisposed();

        var cacheKey = GetCacheKey(tableName, key);
        if (_cache.TryGetValue(cacheKey, out var cachedValue) && cachedValue is T)
            return (T)cachedValue;

        using var command = _connection!.CreateCommand();
        command.CommandText = "SELECT Value, Type FROM KeyValue WHERE TableName = @table AND Key = @key";
        command.Parameters.AddWithValue("@table", tableName);
        command.Parameters.AddWithValue("@key", key);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            var type = reader.GetString(1);
            if (type != expectedType) return default(T);

            var value = reader.IsDBNull(0) ? null : reader.GetValue(0);

            T? result = default(T);
            if (value != null)
            {
                if (value is T directValue)
                    result = directValue;
                else
                    result = (T?)Convert.ChangeType(value, typeof(T));
            }

            _cache.TryAdd(cacheKey, result);
            return result;
        }

        return default(T);
    }

    private bool WriteValue<T>(string tableName, string key, T value, string type)
    {
        ThrowIfDisposed();
        ThrowIfReadOnly();

        try
        {
            using var transaction = _connection!.BeginTransaction();

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            ExecuteNonQuery(@"
                INSERT INTO KeyValue (TableName, Key, Type, Value, CreatedAt, UpdatedAt)
                VALUES (@table, @key, @type, @value, @now, @now)
                ON CONFLICT(TableName, Key) DO UPDATE SET 
                    Type = @type, Value = @value, UpdatedAt = @now",
                new SqliteParameter("@table", tableName),
                new SqliteParameter("@key", key),
                new SqliteParameter("@type", type),
                new SqliteParameter("@value", value),
                new SqliteParameter("@now", now));

            var cacheKey = GetCacheKey(tableName, key);
            _cache.AddOrUpdate(cacheKey, value, (k, v) => value);

            transaction.Commit();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool UpdateValue<T>(string tableName, string key, T value, string type)
    {
        ThrowIfDisposed();
        ThrowIfReadOnly();

        if (!KeyExists(tableName, key))
            return false;

        return WriteValue(tableName, key, value, type);
    }

    // ----------------------------------------------------------------------------------
    // 缓存和同步
    // ----------------------------------------------------------------------------------

    /// <summary>
    /// 更新缓存大小
    /// </summary>
    public bool UpdateCacheSize(int newCacheSize)
    {
        ThrowIfDisposed();

        try
        {
            ExecuteNonQuery($"PRAGMA cache_size=-{newCacheSize};");
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 强制同步缓存到磁盘
    /// </summary>
    public void SyncCache()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            try
            {
                ExecuteNonQuery("PRAGMA wal_checkpoint(FULL);");
            }
            catch
            {
                // 忽略错误，尽力而为
            }
        }
    }

    /// <summary>
    /// 重新加载缓存
    /// </summary>
    public void ReloadCache()
    {
        ThrowIfDisposed();

        _cache.Clear();
    }

    /// <summary>
    /// 比较缓存与磁盘数据一致性
    /// </summary>
    public bool CompareCacheWithDisk()
    {
        ThrowIfDisposed();

        try
        {
            foreach (var kvp in _cache)
            {
                var parts = kvp.Key.Split(':', 2);
                if (parts.Length != 2) continue;

                var tableName = parts[0];
                var key = parts[1];

                // 简单比较 - 实际实现可能需要更复杂的逻辑
                using var command = _connection!.CreateCommand();
                command.CommandText = "SELECT Value FROM KeyValue WHERE TableName = @table AND Key = @key";
                command.Parameters.AddWithValue("@table", tableName);
                command.Parameters.AddWithValue("@key", key);

                var diskValue = command.ExecuteScalar();
                if (!Equals(kvp.Value, diskValue))
                    return false;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 安全关闭并保存数据库
    /// </summary>
    public void Close()
    {
        Dispose();
    }

    /// <summary>
    /// 输出数据库相关信息：数据库文件路径、文件大小、page_count/page_size，以及所有表名、行数和近似大小（字节）。
    /// 该方法仅作调试/诊断用途。
    /// </summary>
    /// <summary>
    /// Dump 数据库信息并可选地将文本/二进制保存到本地文件 `laststart.dump`。
    /// </summary>
    /// <param name="writeBinary">若为 true，则同时将文本形式写入 laststart.dump（覆盖）并把数据库文件原始二进制也写入同目录下的 laststart.db.bin</param>
    public void Dump(bool writeBinary = false)
    {
        ThrowIfDisposed();

        try
        {
            // 输出数据库文件路径与物理大小（若存在）。将路径解析为绝对路径以便诊断。
            string resolvedPath = _databasePath ?? string.Empty;
            try
            {
                // 如果是内存数据库或 URI 方案，直接打印原始值
                if (string.Equals(resolvedPath, ":memory:", StringComparison.OrdinalIgnoreCase) || resolvedPath.Contains("mode=memory", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"Database (memory/URI): {resolvedPath}");
                }
                else
                {
                    var full = System.IO.Path.GetFullPath(resolvedPath);
                    Console.WriteLine($"Database file: {full}");
                    try
                    {
                        var fi = new System.IO.FileInfo(full);
                        if (fi.Exists)
                        {
                            Console.WriteLine($"File size (bytes): {fi.Length}");

                            // 同目录下可能存在 WAL/SHM/JOURNAL 文件，列出并显示大小
                            var dir = fi.DirectoryName ?? System.IO.Path.GetDirectoryName(full) ?? string.Empty;
                            var baseName = System.IO.Path.GetFileName(full);
                            var wal = System.IO.Path.Combine(dir, baseName + "-wal");
                            var shm = System.IO.Path.Combine(dir, baseName + "-shm");
                            var journal = System.IO.Path.Combine(dir, baseName + "-journal");

                            void PrintIfExists(string p)
                            {
                                try
                                {
                                    var f = new System.IO.FileInfo(p);
                                    if (f.Exists)
                                        Console.WriteLine($"  {System.IO.Path.GetFileName(p)}: {f.Length} bytes");
                                }
                                catch { }
                            }

                            PrintIfExists(wal);
                            PrintIfExists(shm);
                            PrintIfExists(journal);
                        }
                        else
                        {
                            Console.WriteLine("Database file does not exist on disk.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to get file info: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to resolve database path: {ex.Message}");
            }

            // 查询 page_count 和 page_size
            try
            {
                using var cmd1 = _connection!.CreateCommand();
                cmd1.CommandText = "PRAGMA page_count;";
                var pageCountObj = cmd1.ExecuteScalar();
                var pageCount = pageCountObj == null ? 0L : Convert.ToInt64(pageCountObj);

                using var cmd2 = _connection.CreateCommand();
                cmd2.CommandText = "PRAGMA page_size;";
                var pageSizeObj = cmd2.ExecuteScalar();
                var pageSize = pageSizeObj == null ? 0L : Convert.ToInt64(pageSizeObj);

                Console.WriteLine($"page_count: {pageCount}, page_size: {pageSize}, estimated DB size: {pageCount * pageSize} bytes");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to query PRAGMA page_count/page_size: {ex.Message}");
            }

            // 列出所有表（不包含 sqlite_internal 表）
            var tables = new List<string>();
            try
            {
                using var cmd = _connection!.CreateCommand();
                cmd.CommandText = "SELECT name, type FROM sqlite_master WHERE (type='table' OR type='view') AND name NOT LIKE 'sqlite_%' ORDER BY name;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var name = reader.GetString(0);
                    tables.Add(name);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to list tables: {ex.Message}");
            }

            if (tables.Count == 0)
            {
                Console.WriteLine("No user tables found in database.");
                return;
            }

            // 可选：准备将文本输出写入文件
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            void AppendLine(string s)
            {
                Console.WriteLine(s);
                if (writeBinary) sb.AppendLine(s);
            }

            // 获取每个表的行数，总行数用于估算大小
            var tableRowCounts = new Dictionary<string, long>();
            long totalRows = 0;
            foreach (var t in tables)
            {
                try
                {
                    using var c = _connection!.CreateCommand();
                    // 表名来自 sqlite_master，但仍需作为标识符安全地转义双引号
                    var escapedTable = EscapeIdentifier(t);
                    c.CommandText = $"SELECT COUNT(*) FROM \"{escapedTable}\";";
                    var obj = c.ExecuteScalar();
                    var cnt = obj == null ? 0L : Convert.ToInt64(obj);
                    tableRowCounts[t] = cnt;
                    totalRows += cnt;
                }
                catch
                {
                    tableRowCounts[t] = 0;
                }
            }

            // 如果要求写入本地文件，则把字符串写入 laststart.dump，并在需要时把数据库二进制也写入 laststart.db.bin
            if (writeBinary)
            {
                try
                {
                    var outPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "laststart.dump");
                    System.IO.File.WriteAllText(outPath, sb.ToString(), System.Text.Encoding.UTF8);
                    Console.WriteLine($"Dump written to: {outPath}");

                    // 同时写入数据库的二进制副本（如果是文件），文件名 laststart.db.bin
                    try
                    {
                        if (!string.Equals(resolvedPath, ":memory:", StringComparison.OrdinalIgnoreCase) && !resolvedPath.Contains("mode=memory", StringComparison.OrdinalIgnoreCase))
                        {
                            var fullDb = System.IO.Path.GetFullPath(resolvedPath);
                            if (System.IO.File.Exists(fullDb))
                            {
                                var dbBin = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "laststart.db.bin");
                                System.IO.File.Copy(fullDb, dbBin, true);
                                Console.WriteLine($"Database binary copied to: {dbBin}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to write DB binary: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to write dump file: {ex.Message}");
                }
            }

            // 估算每表大小：若 totalRows>0 则按行数占比估算，否则使用每表大致 BLOB 列总和或无法估算
            long fileSizeBytes = 0;
            try
            {
                // 使用之前解析的路径，避免直接对可能为 null 的字段调用 FileInfo
                if (!string.Equals(resolvedPath, ":memory:", StringComparison.OrdinalIgnoreCase) && !resolvedPath.Contains("mode=memory", StringComparison.OrdinalIgnoreCase))
                {
                    var fullForSize = System.IO.Path.GetFullPath(resolvedPath);
                    var fi2 = new System.IO.FileInfo(fullForSize);
                    if (fi2.Exists) fileSizeBytes = fi2.Length;
                }
            }
            catch { }

            AppendLine("Tables:");
            foreach (var t in tables)
            {
                var rows = tableRowCounts.ContainsKey(t) ? tableRowCounts[t] : 0L;
                long approxBytes = 0;
                if (totalRows > 0 && fileSizeBytes > 0)
                {
                    approxBytes = (long)Math.Round((double)rows / (double)totalRows * fileSizeBytes);
                }
                AppendLine($"  {t}: rows={rows}, approx_size_bytes={approxBytes}");

                // 列出表内所有键以及键对应的值；对于复合类型，展开并使用缩进表示
                try
                {
                    using var cmd = _connection!.CreateCommand();
                    // 使用参数化查询读取 KeyValue 表中的条目（TableName 作为值）
                    cmd.CommandText = "SELECT Key, Type, Value FROM KeyValue WHERE TableName = @table AND Key != '__TABLE_META__'";
                    cmd.Parameters.AddWithValue("@table", t);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var key = reader.GetString(0);
                        var type = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        var valueObj = reader.IsDBNull(2) ? null : reader.GetValue(2);

                        if (type == "composite" && valueObj is byte[] bytes)
                        {
                            AppendLine($"    {key}: (composite)");
                            var cb = CompositeBuilder.Parse(bytes, t, key);
                            if (cb != null)
                            {
                                var dict = cb.AsDictionary();
                                foreach (var kvp in dict)
                                {
                                    var display = kvp.Value switch
                                    {
                                        byte[] b => $"byte[{b.Length}]",
                                        null => "null",
                                        _ => kvp.Value.ToString()
                                    };
                                    AppendLine($"      {kvp.Key}: {display} ({kvp.Value?.GetType().Name ?? "null"})");
                                }
                            }
                            else
                            {
                                AppendLine($"      (failed to parse composite for key {key})");
                            }
                        }
                        else
                        {
                            // 非复合类型，直接打印
                            var display = valueObj switch
                            {
                                byte[] b => $"byte[{b.Length}]",
                                null => "null",
                                _ => valueObj.ToString()
                            };
                            AppendLine($"    {key}: {display} ({type})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppendLine($"    Failed to list keys for table {t}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Dump failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            SyncCache();
            _connection?.Close();
            _connection?.Dispose();
            _cache.Clear();
            _disposed = true;
        }
    }
}

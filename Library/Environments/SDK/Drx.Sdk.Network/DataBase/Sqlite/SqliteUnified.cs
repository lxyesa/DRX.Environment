using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using System.Linq.Expressions;
using System.Text;
using System.ComponentModel.DataAnnotations;

// 新增 Publish 属性定义
[AttributeUsage(AttributeTargets.Property)]
public class PublishAttribute : Attribute
{
}

namespace Drx.Sdk.Network.DataBase.Sqlite
{
    /// <summary>
    /// 基于 Microsoft.Data.Sqlite 的统一数据库操作类
    /// </summary>
    /// <typeparam name="T">继承自 IDataBase 的数据类型</typeparam>
    public class SqliteUnified<T> where T : class, IDataBase, new()
    {
        private readonly string _connectionString;
        private readonly string _tableName;
        private readonly Dictionary<string, PropertyInfo> _properties;
        private readonly Dictionary<string, PropertyInfo> _dataTableProperties;
        private readonly Dictionary<string, PropertyInfo> _dataTableListProperties;

        // 新增：缓存数组、字典、链表等复杂集合属性
        private readonly Dictionary<string, PropertyInfo> _arrayProperties;
        private readonly Dictionary<string, PropertyInfo> _dictionaryProperties;
        private readonly Dictionary<string, PropertyInfo> _linkedListProperties;

        // 静态缓存getter/setter委托，提升反射性能
        private static readonly Dictionary<Type, Dictionary<string, Func<object, object?>>> GetterCache = new();
        private static readonly Dictionary<Type, Dictionary<string, Action<object, object?>>> SetterCache = new();
        // 优化：缓存子表类型的可读写属性列表和无参构造委托，减少反射损耗
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, PropertyInfo[]> ChildTypePropertiesCache = new();
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Func<object>> ChildTypeFactoryCache = new();

        /// <summary>
        /// 初始化 SqliteUnified 实例
        /// </summary>
        /// <param name="databasePath">数据库文件路径（相对于程序运行根目录）</param>
        /// <param name="basePath">基础路径，默认为程序运行根目录</param>
        public SqliteUnified(string databasePath, string? basePath = null)
        {
            basePath = basePath ?? AppDomain.CurrentDomain.BaseDirectory;
            var fullPath = Path.Combine(basePath, databasePath);

            // 确保目录存在
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _connectionString = $"Data Source={fullPath}";
            _tableName = typeof(T).Name;

            // 缓存属性信息
            _properties = typeof(T).GetProperties()
                .Where(p => p.CanRead && p.CanWrite)
                .ToDictionary(p => p.Name, p => p);

            // 缓存getter/setter委托
            CacheAccessors(typeof(T));

            // 缓存继承自 IDataTable 的属性（一对一关系）
            _dataTableProperties = _properties
                .Where(kvp => typeof(IDataTable).IsAssignableFrom(kvp.Value.PropertyType))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // 缓存 List<IDataTable> 类型的属性（一对多关系）
            _dataTableListProperties = _properties
                .Where(kvp => IsDataTableList(kvp.Value.PropertyType))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // 缓存数组属性（如 T[]、int[]、UserData[] 等）
            _arrayProperties = _properties
                .Where(kvp => IsArrayProperty(kvp.Value.PropertyType))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // 缓存字典属性（如 Dictionary<K,V>，支持嵌套）
            _dictionaryProperties = _properties
                .Where(kvp => IsDictionaryProperty(kvp.Value.PropertyType))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // 缓存链表属性（如 LinkedList<T>）
            _linkedListProperties = _properties
                .Where(kvp => IsLinkedListProperty(kvp.Value.PropertyType))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // 为所有子类型缓存 getter/setter
            foreach (var dataTableProp in _dataTableProperties.Values)
            {
                CacheAccessors(dataTableProp.PropertyType);
            }

            foreach (var dataTableListProp in _dataTableListProperties.Values)
            {
                var childType = GetDataTableListElementType(dataTableListProp.PropertyType);
                CacheAccessors(childType);
            }

            // 初始化数据库和表
            InitializeDatabase();

            // 自动修复表结构
            RepairTable();

        }

        /// <summary>
        /// 修复表结构，自动添加缺失字段
        /// </summary>
        private void RepairTable()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // 修复主表
            RepairTableForType(connection, typeof(T), _tableName);

            // 修复一对一子表
            foreach (var dataTableProp in _dataTableProperties.Values)
            {
                var childType = dataTableProp.PropertyType;
                var childTableName = $"{_tableName}_{dataTableProp.Name}";
                RepairTableForType(connection, childType, childTableName);
            }

            // 修复一对多子表
            foreach (var dataTableListProp in _dataTableListProperties.Values)
            {
                var childType = GetDataTableListElementType(dataTableListProp.PropertyType);
                var childTableName = $"{_tableName}_{dataTableListProp.Name}";
                RepairTableForType(connection, childType, childTableName);
            }

            // 修复数组子表
            foreach (var arrayProp in _arrayProperties.Values)
            {
                var elemType = arrayProp.PropertyType.GetElementType();
                if (elemType != null)
                {
                    var childTableName = $"{_tableName}_{arrayProp.Name}";
                    RepairTableForType(connection, elemType, childTableName);
                }
            }

            // 修复链表子表
            foreach (var linkedProp in _linkedListProperties.Values)
            {
                var elemType = linkedProp.PropertyType.GetGenericArguments()[0];
                var childTableName = $"{_tableName}_{linkedProp.Name}";
                RepairTableForType(connection, elemType, childTableName);
            }

            // 修复字典子表
            foreach (var dictProp in _dictionaryProperties.Values)
            {
                var keyType = dictProp.PropertyType.GetGenericArguments()[0];
                var valueType = dictProp.PropertyType.GetGenericArguments()[1];
                var childTableName = $"{_tableName}_{dictProp.Name}";
                // 字典子表结构：Id, ParentId, DictKey, DictValue
                RepairTableForType(connection, typeof(DictionaryEntrySurrogate), childTableName);
                // 若值为复杂类型，递归建表
                if (IsComplexType(valueType))
                {
                    var valueTableName = $"{childTableName}_Value";
                    RepairTableForType(connection, valueType, valueTableName);
                }
            }
        }

        /// <summary>
        /// 流式异步枚举所有实体，逐条读取并异步加载子表，适用于大数据集以降低瞬时内存占用。
        /// </summary>
        public async IAsyncEnumerable<T> GetAllStreamAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            var sql = $"SELECT * FROM [{_tableName}]";
            using var cmd = new SqliteCommand(sql, connection);
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var entity = CreateEntityFromReader(reader);
                // 异步加载子表数据（如果存在）
                await LoadChildEntitiesAsync(connection, entity, cancellationToken).ConfigureAwait(false);
                yield return entity;
            }
        }

        /// <summary>
        /// 批量异步写入（按 batch 分段，内部顺序调用 PushAsync），用于将大量数据直接写入磁盘，避免在内存中保留全部集合。
        /// 简单实现：按 batch 将元素分组并逐条调用 PushAsync。若需更高写入性能，可在未来将 batch 内的多条写入合并到单个事务中。
        /// </summary>
        public async Task PushBatchAsync(IEnumerable<T> items, int batchSize = 1000, CancellationToken cancellationToken = default)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (batchSize <= 0) batchSize = 1000;

            var buffer = new List<T>(batchSize);
            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                buffer.Add(item);
                if (buffer.Count >= batchSize)
                {
                    // 在单个连接/事务中写入整个 batch，降低连接/事务开销
                    using var connection = new SqliteConnection(_connectionString);
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                    var tranObj = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                    var transaction = tranObj as SqliteTransaction ?? throw new InvalidOperationException("SqliteTransaction 获取失败");
                    try
                    {
                        foreach (var e in buffer)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            // 插入主表与子表（在同一事务内）
                            await InsertEntityInTransactionAsync(connection, transaction, e, cancellationToken).ConfigureAwait(false);
                        }
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                    finally
                    {
                        // 事务对象会随 connection.Dispose 一起清理
                    }
                    buffer.Clear();
                }
            }

            if (buffer.Count > 0)
            {
                using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                var tranObj = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                var transaction = tranObj as SqliteTransaction ?? throw new InvalidOperationException("SqliteTransaction 获取失败");
                try
                {
                    foreach (var e in buffer)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await InsertEntityInTransactionAsync(connection, transaction, e, cancellationToken).ConfigureAwait(false);
                    }
                    transaction.Commit();
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
        }

        // 在指定 connection/transaction 中插入实体及其子表，内部复用已实现的插入方法
        private Task InsertEntityInTransactionAsync(SqliteConnection connection, SqliteTransaction transaction, T entity, CancellationToken cancellationToken)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            // 确保 Id
            if (entity.Id == 0)
            {
                entity.Id = (int)(DateTime.UtcNow.Ticks % int.MaxValue);
            }

            // 插入主实体
            InsertMainEntity(connection, transaction, entity);

            // 同步一对一子表
            foreach (var dataTableProp in _dataTableProperties)
            {
                var childEntity = dataTableProp.Value.GetValue(entity) as IDataTable;
                if (childEntity != null)
                {
                    childEntity.ParentId = entity.Id;
                    InsertChildEntity(connection, transaction, childEntity, $"{_tableName}_{dataTableProp.Key}");
                }
            }

            // 同步一对多子表
            foreach (var dataTableListProp in _dataTableListProperties)
            {
                var childList = dataTableListProp.Value.GetValue(entity) as IEnumerable;
                if (childList != null)
                {
                    var tableName = $"{_tableName}_{dataTableListProp.Key}";
                    // 先删除旧的（保持与 Push 语义一致）
                    var deleteSql = $"DELETE FROM [{tableName}] WHERE [ParentId] = @parentId";
                    using (var deleteCommand = new SqliteCommand(deleteSql, connection, transaction))
                    {
                        deleteCommand.Parameters.AddWithValue("@parentId", entity.Id);
                        deleteCommand.ExecuteNonQuery();
                    }

                    foreach (IDataTable childEntity in childList)
                    {
                        if (childEntity != null)
                        {
                            childEntity.ParentId = entity.Id;
                            InsertChildEntity(connection, transaction, childEntity, tableName);
                        }
                    }
                }
            }

            // 处理数组属性
            foreach (var arrayProp in _arrayProperties)
            {
                var arr = arrayProp.Value.GetValue(entity) as Array;
                if (arr != null)
                {
                    var tableName = $"{_tableName}_{arrayProp.Key}";
                    var deleteSql = $"DELETE FROM [{tableName}] WHERE [ParentId] = @parentId";
                    using (var deleteCommand = new SqliteCommand(deleteSql, connection, transaction))
                    {
                        deleteCommand.Parameters.AddWithValue("@parentId", entity.Id);
                        deleteCommand.ExecuteNonQuery();
                    }

                    foreach (var elem in arr)
                    {
                        if (elem != null)
                        {
                            if (IsComplexType(elem.GetType()))
                            {
                                var pi = elem.GetType().GetProperty("ParentId");
                                if (pi != null) pi.SetValue(elem, entity.Id);
                            }
                            if (elem is IDataTable dtElem)
                            {
                                InsertChildEntity(connection, transaction, dtElem, tableName);
                            }
                            else
                            {
                                var surrogate = new DictionaryEntrySurrogate
                                {
                                    ParentId = entity.Id,
                                    DictKey = "",
                                    DictValue = elem?.ToString() ?? ""
                                };
                                InsertChildEntity(connection, transaction, surrogate, tableName);
                            }
                        }
                    }
                }
            }

            // 处理链表属性
            foreach (var linkedProp in _linkedListProperties)
            {
                var linked = linkedProp.Value.GetValue(entity) as System.Collections.IEnumerable;
                if (linked != null)
                {
                    var tableName = $"{_tableName}_{linkedProp.Key}";
                    var elemType = linkedProp.Value.PropertyType.GetGenericArguments()[0];

                    var deleteSql = $"DELETE FROM [{tableName}] WHERE [ParentId] = @parentId";
                    using (var deleteCommand = new SqliteCommand(deleteSql, connection, transaction))
                    {
                        deleteCommand.Parameters.AddWithValue("@parentId", entity.Id);
                        deleteCommand.ExecuteNonQuery();
                    }

                    foreach (var elem in linked)
                    {
                        if (elem != null)
                        {
                            if (IsSimpleType(elemType))
                            {
                                InsertSimpleTypeCollectionElement(connection, transaction, tableName, entity.Id, elem);
                            }
                            else if (elem is IDataTable dtElem)
                            {
                                dtElem.ParentId = entity.Id;
                                InsertChildEntity(connection, transaction, dtElem, tableName);
                            }
                            else if (IsComplexType(elem.GetType()))
                            {
                                var pi = elem.GetType().GetProperty("ParentId");
                                if (pi != null) pi.SetValue(elem, entity.Id);
                            }
                        }
                    }
                }
            }

            // 处理字典属性
            foreach (var dictProp in _dictionaryProperties)
            {
                var dict = dictProp.Value.GetValue(entity);
                if (dict is System.Collections.IDictionary idict)
                {
                    var tableName = $"{_tableName}_{dictProp.Key}";
                    var deleteSql = $"DELETE FROM [{tableName}] WHERE [ParentId] = @parentId";
                    using (var deleteCommand = new SqliteCommand(deleteSql, connection, transaction))
                    {
                        deleteCommand.Parameters.AddWithValue("@parentId", entity.Id);
                        deleteCommand.ExecuteNonQuery();
                    }

                    foreach (System.Collections.DictionaryEntry entry in idict)
                    {
                        var surrogate = new DictionaryEntrySurrogate
                        {
                            ParentId = entity.Id,
                            DictKey = entry.Key?.ToString() ?? "",
                            DictValue = entry.Value?.ToString() ?? ""
                        };
                        InsertChildEntity(connection, transaction, surrogate, tableName);

                        if (entry.Value != null && IsComplexType(entry.Value.GetType()))
                        {
                            var valueTableName = $"{tableName}_Value";
                            if (entry.Value.GetType().GetProperty("ParentId") != null)
                            {
                                entry.Value.GetType().GetProperty("ParentId")?.SetValue(entry.Value, entity.Id);
                            }
                            if (entry.Value is IDataTable dtVal)
                            {
                                InsertChildEntity(connection, transaction, dtVal, valueTableName);
                            }
                            else
                            {
                                var surrogateVal = new DictionaryEntrySurrogate
                                {
                                    ParentId = entity.Id,
                                    DictKey = entry.Key?.ToString() ?? "",
                                    DictValue = entry.Value?.ToString() ?? ""
                                };
                                InsertChildEntity(connection, transaction, surrogateVal, valueTableName);
                            }
                        }
                    }
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 修复指定表结构
        /// </summary>
        private void RepairTableForType(SqliteConnection connection, Type type, string tableName)
        {
            // 检查表是否存在，不存在则先创建
            using (var checkCmd = new SqliteCommand($"SELECT name FROM sqlite_master WHERE type='table' AND name='{tableName}'", connection))
            using (var checkReader = checkCmd.ExecuteReader())
            {
                if (!checkReader.Read())
                {
                    // 表不存在，自动创建
                    CreateTable(connection, type, tableName);
                }
            }

            // 获取表中已有字段
            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = new SqliteCommand($"PRAGMA table_info([{tableName}])", connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var colName = reader["name"] as string;
                    if (!string.IsNullOrEmpty(colName))
                        existingColumns.Add(colName);
                }
            }

            // 检查 Data 类属性，补充缺失字段
            foreach (var property in type.GetProperties().Where(p => p.CanRead && p.CanWrite && IsSimpleType(p.PropertyType)))
            {
                if (!existingColumns.Contains(property.Name))
                {
                    var columnType = GetSqliteType(property.PropertyType);
                    var alterSql = $"ALTER TABLE [{tableName}] ADD COLUMN [{property.Name}] {columnType}";
                    using var alterCmd = new SqliteCommand(alterSql, connection);
                    alterCmd.ExecuteNonQuery();
                }
            }

            // 自动为表的主键字段创建唯一索引
            // 只对Id字段创建唯一索引，ParentId不应该是唯一的（一对多关系）
            if (existingColumns.Contains("Id"))
            {
                var createIndexSql = $"CREATE UNIQUE INDEX IF NOT EXISTS idx_{tableName}_Id ON [{tableName}]([Id]);";
                using var indexCmd = new SqliteCommand(createIndexSql, connection);
                indexCmd.ExecuteNonQuery();
            }

            // 为ParentId创建普通索引（非唯一），提高查询性能
            if (existingColumns.Contains("ParentId"))
            {
                var createIndexSql = $"CREATE INDEX IF NOT EXISTS idx_{tableName}_ParentId ON [{tableName}]([ParentId]);";
                using var indexCmd = new SqliteCommand(createIndexSql, connection);
                indexCmd.ExecuteNonQuery();
            }
        }
        /// 构建并缓存类型的getter/setter委托
        /// </summary>
        private static void CacheAccessors(Type type)
        {
            lock (GetterCache)
            {
                if (!GetterCache.ContainsKey(type))
                {
                    var getterDict = new Dictionary<string, Func<object, object?>>();
                    var setterDict = new Dictionary<string, Action<object, object?>>();
                    foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                    {
                        if (!prop.CanRead || !prop.CanWrite) continue;
                        var getMethod = prop.GetGetMethod();
                        var setMethod = prop.GetSetMethod();
                        if (getMethod != null)
                        {
                            var instance = Expression.Parameter(typeof(object), "instance");
                            var convert = Expression.Convert(instance, type);
                            var call = Expression.Call(convert, getMethod);
                            var castResult = Expression.Convert(call, typeof(object));
                            var lambda = Expression.Lambda<Func<object, object?>>(castResult, instance).Compile();
                            getterDict[prop.Name] = lambda;
                        }
                        if (setMethod != null)
                        {
                            var instance = Expression.Parameter(typeof(object), "instance");
                            var value = Expression.Parameter(typeof(object), "value");
                            var convert = Expression.Convert(instance, type);
                            var valueCast = Expression.Convert(value, prop.PropertyType);
                            var call = Expression.Call(convert, setMethod, valueCast);
                            var lambda = Expression.Lambda<Action<object, object?>>(call, instance, value).Compile();
                            setterDict[prop.Name] = lambda;
                        }
                    }
                    GetterCache[type] = getterDict;
                    SetterCache[type] = setterDict;
                }
            }
        }

        /// <summary>
        /// 初始化数据库和表结构
        /// </summary>
        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // 创建主表
            CreateTable(connection, typeof(T), _tableName);

            // 创建关联表（一对一）
            foreach (var dataTableProp in _dataTableProperties.Values)
            {
                var childType = dataTableProp.PropertyType;
                var childTableName = $"{_tableName}_{dataTableProp.Name}";
                CreateTable(connection, childType, childTableName);
            }

            // 创建关联表（一对多）
            foreach (var dataTableListProp in _dataTableListProperties.Values)
            {
                var childType = GetDataTableListElementType(dataTableListProp.PropertyType);
                var childTableName = $"{_tableName}_{dataTableListProp.Name}";
                CreateTable(connection, childType, childTableName);
            }

            // 创建数组子表
            foreach (var arrayProp in _arrayProperties.Values)
            {
                var elemType = arrayProp.PropertyType.GetElementType();
                if (elemType != null)
                {
                    var childTableName = $"{_tableName}_{arrayProp.Name}";
                    // 为简单类型数组创建包含ParentId的代理表
                    if (IsSimpleType(elemType))
                    {
                        CreateSimpleTypeCollectionTable(connection, childTableName, elemType);
                    }
                    else
                    {
                        CreateTable(connection, elemType, childTableName);
                    }
                }
            }

            // 创建链表子表
            foreach (var linkedProp in _linkedListProperties.Values)
            {
                var elemType = linkedProp.PropertyType.GetGenericArguments()[0];
                var childTableName = $"{_tableName}_{linkedProp.Name}";
                // 为简单类型链表创建包含ParentId的代理表
                if (IsSimpleType(elemType))
                {
                    CreateSimpleTypeCollectionTable(connection, childTableName, elemType);
                }
                else
                {
                    CreateTable(connection, elemType, childTableName);
                }
            }

            // 创建字典子表
            foreach (var dictProp in _dictionaryProperties.Values)
            {
                var childTableName = $"{_tableName}_{dictProp.Name}";
                // 字典子表结构：Id, ParentId, DictKey, DictValue
                CreateTable(connection, typeof(DictionaryEntrySurrogate), childTableName);
            }
        }

        /// <summary>
        /// 创建表
        /// </summary>
        private void CreateTable(SqliteConnection connection, Type type, string tableName)
        {
            var sql = new StringBuilder($"CREATE TABLE IF NOT EXISTS [{tableName}] (");
            var columns = new List<string>();

            foreach (var property in type.GetProperties().Where(p => p.CanRead && p.CanWrite))
            {
                var columnName = property.Name;
                var columnType = GetSqliteType(property.PropertyType);

                // 只处理简单类型，跳过不可映射的复杂类型（如数组、字典、集合等）
                if (!IsSimpleType(property.PropertyType) && !typeof(IDataTable).IsAssignableFrom(property.PropertyType))
                {
                    continue;
                }

                var columnDef = $"[{columnName}] {columnType}";

                // 主键处理
                if (property.Name == "Id")
                {
                    columnDef += " PRIMARY KEY";
                }

                columns.Add(columnDef);
            }

            // 防止无字段导致语法错误
            if (columns.Count == 0)
            {
                columns.Add("[Id] INTEGER PRIMARY KEY");
            }

            sql.Append(string.Join(", ", columns));
            sql.Append(")");

            using var command = new SqliteCommand(sql.ToString(), connection);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// 为简单类型集合创建包含ParentId的代理表
        /// </summary>
        private void CreateSimpleTypeCollectionTable(SqliteConnection connection, string tableName, Type elementType)
        {
            var elementSqlType = GetSqliteType(elementType);
            var sql = $@"CREATE TABLE IF NOT EXISTS [{tableName}] (
                [Id] INTEGER PRIMARY KEY AUTOINCREMENT,
                [ParentId] INTEGER NOT NULL,
                [Value] {elementSqlType}
            )";

            using var command = new SqliteCommand(sql, connection);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// 插入简单类型集合元素到代理表
        /// </summary>
        private void InsertSimpleTypeCollectionElement(SqliteConnection connection, SqliteTransaction transaction, string tableName, int parentId, object value)
        {
            var sql = $"INSERT INTO [{tableName}] ([ParentId], [Value]) VALUES (@parentId, @value)";
            using var command = new SqliteCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@parentId", parentId);
            command.Parameters.AddWithValue("@value", value ?? DBNull.Value);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// 获取 SQLite 对应的数据类型
        /// </summary>
        private string GetSqliteType(Type? type)
        {
            if (type == null) return "TEXT";

            // 处理可空类型
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                type = Nullable.GetUnderlyingType(type);
            }

            if (type == null) return "TEXT";

            return type.Name switch
            {
                nameof(String) => "TEXT",
                nameof(Int32) => "INTEGER",
                nameof(Int64) => "INTEGER",
                nameof(Boolean) => "INTEGER",
                nameof(DateTime) => "TEXT",
                nameof(Decimal) => "REAL",
                nameof(Double) => "REAL",
                nameof(Single) => "REAL",
                _ => "TEXT"
            };
        }

        /// <summary>
        /// 将实体推送到数据库
        /// </summary>
        /// <param name="entity">要保存的实体</param>
        public void Push(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            // 如果ID为0，生成新的ID
            if (entity.Id == 0)
            {
                entity.Id = (int)(DateTime.UtcNow.Ticks % int.MaxValue);
            }

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                // 插入主表数据
                InsertMainEntity(connection, transaction, entity);

                // 新增：同步一对一子表被 [Publish] 标记且同名字段到主表
                foreach (var dataTableProp in _dataTableProperties)
                {
                    var childEntity = dataTableProp.Value.GetValue(entity) as IDataTable;
                    if (childEntity != null)
                    {
                        // 遍历子表属性，查找被 Publish 标记且主表同名字段
                        foreach (var prop in childEntity.GetType().GetProperties())
                        {
                            if (prop.CanRead && prop.CanWrite &&
                                prop.GetCustomAttribute(typeof(PublishAttribute)) != null &&
                                _properties.ContainsKey(prop.Name))
                            {
                                var value = prop.GetValue(childEntity);
                                _properties[prop.Name].SetValue(entity, value);
                            }
                        }
                        childEntity.ParentId = entity.Id;
                        InsertChildEntity(connection, transaction, childEntity, $"{_tableName}_{dataTableProp.Key}");
                    }
                }

                // 处理关联表（一对多）
                foreach (var dataTableListProp in _dataTableListProperties)
                {
                    var childList = dataTableListProp.Value.GetValue(entity) as IEnumerable;
                    if (childList != null)
                    {
                        // 新增：同步一对多子表被 [Publish] 标记且同名字段到主表（取最后一个子表值）
                        object? lastPublishValue = null;
                        string? publishFieldName = null;
                        foreach (IDataTable childEntity in childList)
                        {
                            if (childEntity != null)
                            {
                                foreach (var prop in childEntity.GetType().GetProperties())
                                {
                                    if (prop.CanRead && prop.CanWrite &&
                                        prop.GetCustomAttribute(typeof(PublishAttribute)) != null &&
                                        _properties.ContainsKey(prop.Name))
                                    {
                                        lastPublishValue = prop.GetValue(childEntity);
                                        publishFieldName = prop.Name;
                                    }
                                }
                            }
                        }
                        if (publishFieldName != null && lastPublishValue != null)
                        {
                            _properties[publishFieldName].SetValue(entity, lastPublishValue);
                        }

                        var tableName = $"{_tableName}_{dataTableListProp.Key}";
                        // 先删除现有的子表数据
                        var deleteSql = $"DELETE FROM [{tableName}] WHERE [ParentId] = @parentId";
                        using var deleteCommand = new SqliteCommand(deleteSql, connection, transaction);
                        deleteCommand.Parameters.AddWithValue("@parentId", entity.Id);
                        deleteCommand.ExecuteNonQuery();
                        // 插入新的子表数据
                        foreach (IDataTable childEntity in childList)
                        {
                            if (childEntity != null)
                            {
                                childEntity.ParentId = entity.Id;
                                InsertChildEntity(connection, transaction, childEntity, tableName);
                            }
                        }
                    }
                }

                // 新增：处理数组属性
                foreach (var arrayProp in _arrayProperties)
                {
                    var arr = arrayProp.Value.GetValue(entity) as Array;
                    if (arr != null)
                    {
                        var tableName = $"{_tableName}_{arrayProp.Key}";
                        // 先删除现有的子表数据
                        var deleteSql = $"DELETE FROM [{tableName}] WHERE [ParentId] = @parentId";
                        using var deleteCommand = new SqliteCommand(deleteSql, connection, transaction);
                        deleteCommand.Parameters.AddWithValue("@parentId", entity.Id);
                        deleteCommand.ExecuteNonQuery();
                        // 插入新数组元素
                        foreach (var elem in arr)
                        {
                            if (elem != null)
                            {
                                // 若为复杂类型，需有ParentId属性
                                if (IsComplexType(elem.GetType()))
                                {
                                    var pi = elem.GetType().GetProperty("ParentId");
                                    if (pi != null) pi.SetValue(elem, entity.Id);
                                }
                                if (elem is IDataTable dtElem)
                                {
                                    InsertChildEntity(connection, transaction, dtElem, tableName);
                                }
                                else
                                {
                                    // 基础类型或未实现IDataTable，序列化为字符串存储
                                    var surrogate = new DictionaryEntrySurrogate
                                    {
                                        ParentId = entity.Id,
                                        DictKey = "",
                                        DictValue = elem?.ToString() ?? ""
                                    };
                                    InsertChildEntity(connection, transaction, surrogate, tableName);
                                }
                            }
                        }
                    }
                }

                // 新增：处理链表属性
                foreach (var linkedProp in _linkedListProperties)
                {
                    var linked = linkedProp.Value.GetValue(entity) as System.Collections.IEnumerable;
                    if (linked != null)
                    {
                        var tableName = $"{_tableName}_{linkedProp.Key}";
                        var elemType = linkedProp.Value.PropertyType.GetGenericArguments()[0];

                        // 先删除现有的子表数据
                        var deleteSql = $"DELETE FROM [{tableName}] WHERE [ParentId] = @parentId";
                        using var deleteCommand = new SqliteCommand(deleteSql, connection, transaction);
                        deleteCommand.Parameters.AddWithValue("@parentId", entity.Id);
                        deleteCommand.ExecuteNonQuery();

                        foreach (var elem in linked)
                        {
                            if (elem != null)
                            {
                                if (IsSimpleType(elemType))
                                {
                                    // 简单类型元素，插入到代理表
                                    InsertSimpleTypeCollectionElement(connection, transaction, tableName, entity.Id, elem);
                                }
                                else if (elem is IDataTable dtElem)
                                {
                                    // 复杂类型且实现IDataTable
                                    dtElem.ParentId = entity.Id;
                                    InsertChildEntity(connection, transaction, dtElem, tableName);
                                }
                                else if (IsComplexType(elem.GetType()))
                                {
                                    // 复杂类型但未实现IDataTable，尝试设置ParentId属性
                                    var pi = elem.GetType().GetProperty("ParentId");
                                    if (pi != null) pi.SetValue(elem, entity.Id);
                                    // 这里需要更复杂的处理逻辑，暂时跳过
                                }
                            }
                        }
                    }
                }

                // 新增：处理字典属性
                foreach (var dictProp in _dictionaryProperties)
                {
                    var dict = dictProp.Value.GetValue(entity);
                    if (dict is System.Collections.IDictionary idict)
                    {
                        var tableName = $"{_tableName}_{dictProp.Key}";
                        // 先删除现有的子表数据
                        var deleteSql = $"DELETE FROM [{tableName}] WHERE [ParentId] = @parentId";
                        using var deleteCommand = new SqliteCommand(deleteSql, connection, transaction);
                        deleteCommand.Parameters.AddWithValue("@parentId", entity.Id);
                        deleteCommand.ExecuteNonQuery();
                        foreach (System.Collections.DictionaryEntry entry in idict)
                        {
                            var surrogate = new DictionaryEntrySurrogate
                            {
                                ParentId = entity.Id,
                                DictKey = entry.Key?.ToString() ?? "",
                                DictValue = entry.Value?.ToString() ?? ""
                            };
                            InsertChildEntity(connection, transaction, surrogate, tableName);

                            // 若值为复杂类型，递归插入
                            if (entry.Value != null && IsComplexType(entry.Value.GetType()))
                            {
                                var valueTableName = $"{tableName}_Value";
                                if (entry.Value.GetType().GetProperty("ParentId") != null)
                                {
                                    entry.Value.GetType().GetProperty("ParentId")?.SetValue(entry.Value, entity.Id);
                                }
                                if (entry.Value is IDataTable dtVal)
                                {
                                    InsertChildEntity(connection, transaction, dtVal, valueTableName);
                                }
                                else
                                {
                                    var surrogateVal = new DictionaryEntrySurrogate
                                    {
                                        ParentId = entity.Id,
                                        DictKey = entry.Key?.ToString() ?? "",
                                        DictValue = entry.Value?.ToString() ?? ""
                                    };
                                    InsertChildEntity(connection, transaction, surrogateVal, valueTableName);
                                }
                            }
                        }
                    }
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// 插入主实体数据
        /// </summary>
        private void InsertMainEntity(SqliteConnection connection, SqliteTransaction transaction, T entity)
        {
            var mainProperties = _properties.Where(kvp =>
                !_dataTableProperties.ContainsKey(kvp.Key) &&
                !_dataTableListProperties.ContainsKey(kvp.Key) &&
                !_arrayProperties.ContainsKey(kvp.Key) &&
                !_dictionaryProperties.ContainsKey(kvp.Key) &&
                !_linkedListProperties.ContainsKey(kvp.Key) &&
                IsSimpleType(kvp.Value.PropertyType)); // 只处理简单类型

            var columns = string.Join(", ", mainProperties.Select(kvp => $"[{kvp.Key}]"));
            var parameters = string.Join(", ", mainProperties.Select(kvp => $"@{kvp.Key}"));

            var sql = $"INSERT OR REPLACE INTO [{_tableName}] ({columns}) VALUES ({parameters})";

            using var command = new SqliteCommand(sql, connection, transaction);

            var type = typeof(T);
            var getterDict = GetterCache[type];
            foreach (var prop in mainProperties)
            {
                var rawValue = getterDict[prop.Key](entity);
                var value = ToDbDateTimeString(rawValue, prop.Value.PropertyType) ?? DBNull.Value;
                command.Parameters.AddWithValue($"@{prop.Key}", value);
            }

            command.ExecuteNonQuery();
        }

        /// <summary>
        /// 插入子表数据
        /// </summary>
        private void InsertChildEntity(SqliteConnection connection, SqliteTransaction transaction, IDataTable childEntity, string tableName)
        {
            var childType = childEntity.GetType();

            // 检查子实体是否有Id属性，如果有且为0，则生成新的Id
            var idProperty = childType.GetProperty("Id");
            if (idProperty != null && idProperty.CanRead && idProperty.CanWrite)
            {
                var currentId = idProperty.GetValue(childEntity);
                if (currentId is int intId && intId == 0)
                {
                    var newId = (int)(DateTime.UtcNow.Ticks % int.MaxValue);
                    idProperty.SetValue(childEntity, newId);
                }
            }

            // 使用缓存的属性数组，避免每次都通过反射枚举
            var properties = ChildTypePropertiesCache.GetOrAdd(childType, t => t.GetProperties().Where(p => p.CanRead && p.CanWrite).ToArray());

            var columns = string.Join(", ", properties.Select(p => $"[{p.Name}]"));
            var parameters = string.Join(", ", properties.Select(p => $"@{p.Name}"));

            var sql = $"INSERT INTO [{tableName}] ({columns}) VALUES ({parameters})";

            using var command = new SqliteCommand(sql, connection, transaction);

            // 确保 getter/setter 已缓存
            if (!GetterCache.ContainsKey(childType)) CacheAccessors(childType);
            var getterDict = GetterCache[childType];

            foreach (var prop in properties)
            {
                var rawValue = getterDict[prop.Name](childEntity);
                var value = ToDbDateTimeString(rawValue, prop.PropertyType) ?? DBNull.Value;
                command.Parameters.AddWithValue($"@{prop.Name}", value);
            }

            command.ExecuteNonQuery();
        }

        /// <summary>
        /// 查询数据
        /// </summary>
        /// <param name="propertyName">要查询的属性名</param>
        /// <param name="propertyValue">要查询的属性值</param>
        /// <returns>查询结果列表</returns>
        public List<T> Query(string propertyName, object propertyValue)
        {
            if (string.IsNullOrEmpty(propertyName)) throw new ArgumentNullException(nameof(propertyName));
            if (!_properties.ContainsKey(propertyName))
                throw new ArgumentException($"属性 {propertyName} 不存在于类型 {typeof(T).Name} 中");

            var results = new List<T>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var sql = $"SELECT * FROM [{_tableName}] WHERE [{propertyName}] = @value";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@value", propertyValue ?? DBNull.Value);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var entity = CreateEntityFromReader(reader);

                // 加载关联表数据
                LoadChildEntities(connection, entity);

                results.Add(entity);
            }

            return results;
        }

        /// <summary>
        /// 根据ID查询单个实体
        /// </summary>
        /// <param name="id">实体ID</param>
        /// <returns>查询到的实体，如果不存在则返回null</returns>
        public T? QueryById(int id)
        {
            var results = Query("Id", id);
            return results.FirstOrDefault();
        }

        /// <summary>
        /// 更新实体（等同于Push方法）
        /// </summary>
        /// <param name="entity">要更新的实体</param>
        public void Update(T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (entity.Id == 0) throw new ArgumentException("实体ID不能为0");

            // 更新操作与Push操作相同，使用INSERT OR REPLACE
            Push(entity);
        }

        /// <summary>
        /// 按指定 ID 编辑实体（不会修改 Id 字段）
        /// </summary>
        public void EditById(int id, T entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            // 构建只更新主表的简单类型字段（不包含子表和 Id）
            var mainProperties = _properties.Where(kvp =>
                !_dataTableProperties.ContainsKey(kvp.Key) &&
                !_dataTableListProperties.ContainsKey(kvp.Key) &&
                !_arrayProperties.ContainsKey(kvp.Key) &&
                !_dictionaryProperties.ContainsKey(kvp.Key) &&
                !_linkedListProperties.ContainsKey(kvp.Key) &&
                IsSimpleType(kvp.Value.PropertyType) &&
                kvp.Key != "Id");

            if (!mainProperties.Any()) return;
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();
            try
            {
                var sets = string.Join(", ", mainProperties.Select(kvp => $"[{kvp.Key}] = @{kvp.Key}"));
                var sql = $"UPDATE [{_tableName}] SET {sets} WHERE [Id] = @id";
                using (var cmd = new SqliteCommand(sql, connection, transaction))
                {
                    // 使用缓存 getter 获取传入实体的值（但不允许修改 Id）
                    var getterDict = GetterCache[typeof(T)];
                    foreach (var prop in mainProperties)
                    {
                        var rawValue = getterDict[prop.Key](entity);
                        var value = ToDbDateTimeString(rawValue, prop.Value.PropertyType) ?? DBNull.Value;
                        cmd.Parameters.AddWithValue($"@{prop.Key}", value);
                    }
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }

                // 同步一对一子表（删除旧的再插入）
                foreach (var dataTableProp in _dataTableProperties)
                {
                    var childTable = $"{_tableName}_{dataTableProp.Key}";
                    var deleteSql = $"DELETE FROM [{childTable}] WHERE [ParentId] = @parentId";
                    using (var deleteCmd = new SqliteCommand(deleteSql, connection, transaction))
                    {
                        deleteCmd.Parameters.AddWithValue("@parentId", id);
                        deleteCmd.ExecuteNonQuery();
                    }

                    var childEntity = dataTableProp.Value.GetValue(entity) as IDataTable;
                    if (childEntity != null)
                    {
                        childEntity.ParentId = id;
                        InsertChildEntity(connection, transaction, childEntity, childTable);
                    }
                }

                // 同步一对多子表
                foreach (var dataTableListProp in _dataTableListProperties)
                {
                    var childList = dataTableListProp.Value.GetValue(entity) as IEnumerable;
                    if (childList != null)
                    {
                        var tableName = $"{_tableName}_{dataTableListProp.Key}";
                        var deleteSql = $"DELETE FROM [{tableName}] WHERE [ParentId] = @parentId";
                        using (var deleteCommand = new SqliteCommand(deleteSql, connection, transaction))
                        {
                            deleteCommand.Parameters.AddWithValue("@parentId", id);
                            deleteCommand.ExecuteNonQuery();
                        }

                        foreach (IDataTable childEntity in childList)
                        {
                            if (childEntity != null)
                            {
                                childEntity.ParentId = id;
                                InsertChildEntity(connection, transaction, childEntity, tableName);
                            }
                        }
                    }
                }

                // 处理数组属性
                foreach (var arrayProp in _arrayProperties)
                {
                    var arr = arrayProp.Value.GetValue(entity) as Array;
                    if (arr != null)
                    {
                        var tableName = $"{_tableName}_{arrayProp.Key}";
                        var deleteSql = $"DELETE FROM [{tableName}] WHERE [ParentId] = @parentId";
                        using (var deleteCommand = new SqliteCommand(deleteSql, connection, transaction))
                        {
                            deleteCommand.Parameters.AddWithValue("@parentId", id);
                            deleteCommand.ExecuteNonQuery();
                        }

                        foreach (var elem in arr)
                        {
                            if (elem != null)
                            {
                                if (IsComplexType(elem.GetType()))
                                {
                                    var pi = elem.GetType().GetProperty("ParentId");
                                    if (pi != null) pi.SetValue(elem, id);
                                }
                                if (elem is IDataTable dtElem)
                                {
                                    InsertChildEntity(connection, transaction, dtElem, tableName);
                                }
                                else
                                {
                                    var surrogate = new DictionaryEntrySurrogate
                                    {
                                        ParentId = id,
                                        DictKey = "",
                                        DictValue = elem?.ToString() ?? ""
                                    };
                                    InsertChildEntity(connection, transaction, surrogate, tableName);
                                }
                            }
                        }
                    }
                }

                // 处理链表属性
                foreach (var linkedProp in _linkedListProperties)
                {
                    var linked = linkedProp.Value.GetValue(entity) as System.Collections.IEnumerable;
                    if (linked != null)
                    {
                        var tableName = $"{_tableName}_{linkedProp.Key}";
                        var elemType = linkedProp.Value.PropertyType.GetGenericArguments()[0];

                        var deleteSql = $"DELETE FROM [{tableName}] WHERE [ParentId] = @parentId";
                        using (var deleteCommand = new SqliteCommand(deleteSql, connection, transaction))
                        {
                            deleteCommand.Parameters.AddWithValue("@parentId", id);
                            deleteCommand.ExecuteNonQuery();
                        }

                        foreach (var elem in linked)
                        {
                            if (elem != null)
                            {
                                if (IsSimpleType(elemType))
                                {
                                    InsertSimpleTypeCollectionElement(connection, transaction, tableName, id, elem);
                                }
                                else if (elem is IDataTable dtElem)
                                {
                                    dtElem.ParentId = id;
                                    InsertChildEntity(connection, transaction, dtElem, tableName);
                                }
                                else if (IsComplexType(elem.GetType()))
                                {
                                    var pi = elem.GetType().GetProperty("ParentId");
                                    if (pi != null) pi.SetValue(elem, id);
                                }
                            }
                        }
                    }
                }

                // 处理字典属性
                foreach (var dictProp in _dictionaryProperties)
                {
                    var dict = dictProp.Value.GetValue(entity);
                    if (dict is System.Collections.IDictionary idict)
                    {
                        var tableName = $"{_tableName}_{dictProp.Key}";
                        var deleteSql = $"DELETE FROM [{tableName}] WHERE [ParentId] = @parentId";
                        using (var deleteCommand = new SqliteCommand(deleteSql, connection, transaction))
                        {
                            deleteCommand.Parameters.AddWithValue("@parentId", id);
                            deleteCommand.ExecuteNonQuery();
                        }

                        foreach (System.Collections.DictionaryEntry entry in idict)
                        {
                            var surrogate = new DictionaryEntrySurrogate
                            {
                                ParentId = id,
                                DictKey = entry.Key?.ToString() ?? "",
                                DictValue = entry.Value?.ToString() ?? ""
                            };
                            InsertChildEntity(connection, transaction, surrogate, tableName);

                            if (entry.Value != null && IsComplexType(entry.Value.GetType()))
                            {
                                var valueTableName = $"{tableName}_Value";
                                if (entry.Value.GetType().GetProperty("ParentId") != null)
                                {
                                    entry.Value.GetType().GetProperty("ParentId")?.SetValue(entry.Value, id);
                                }
                                if (entry.Value is IDataTable dtVal)
                                {
                                    InsertChildEntity(connection, transaction, dtVal, valueTableName);
                                }
                                else
                                {
                                    var surrogateVal = new DictionaryEntrySurrogate
                                    {
                                        ParentId = id,
                                        DictKey = entry.Key?.ToString() ?? "",
                                        DictValue = entry.Value?.ToString() ?? ""
                                    };
                                    InsertChildEntity(connection, transaction, surrogateVal, valueTableName);
                                }
                            }
                        }
                    }
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// 根据属性名和值更新所有匹配的实体（不修改 Id 字段）
        /// </summary>
        public int EditWhere(string propertyName, object propertyValue, T entity)
        {
            if (string.IsNullOrEmpty(propertyName)) throw new ArgumentNullException(nameof(propertyName));
            if (!_properties.ContainsKey(propertyName))
                throw new ArgumentException($"属性 {propertyName} 不存在于类型 {typeof(T).Name} 中");

            // 查找符合条件的 Id 列表，然后对每个 Id 执行 EditById（保证子表一致性）
            var ids = new List<int>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var sql = $"SELECT [Id] FROM [{_tableName}] WHERE [{propertyName}] = @value";
                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@value", propertyValue ?? DBNull.Value);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    if (reader[0] != DBNull.Value)
                        ids.Add(Convert.ToInt32(reader[0]));
                }
            }

            foreach (var id in ids)
            {
                EditById(id, entity);
            }

            return ids.Count;
        }

        /// <summary>
        /// 根据属性名和值异步更新所有匹配的实体（不修改 Id 字段）
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <param name="propertyValue">属性值</param>
        /// <param name="entity">用于更新的实体对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>受影响的实体数量</returns>
        public async Task<int> EditWhereAsync(string propertyName, object propertyValue, T entity, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(propertyName)) throw new ArgumentNullException(nameof(propertyName));
            if (!_properties.ContainsKey(propertyName))
                throw new ArgumentException($"属性 {propertyName} 不存在于类型 {typeof(T).Name} 中");

            var ids = new List<int>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                var sql = $"SELECT [Id] FROM [{_tableName}] WHERE [{propertyName}] = @value";
                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@value", propertyValue ?? DBNull.Value);
                using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (reader[0] != DBNull.Value)
                        ids.Add(Convert.ToInt32(reader[0]));
                }
            }

            foreach (var id in ids)
            {
                await EditByIdAsync(id, entity, cancellationToken).ConfigureAwait(false);
            }

            return ids.Count;
        }

        /// <summary>
        /// 根据任意 SQL 条件异步更新所有匹配的实体（不修改 Id 字段）
        /// 注意：condition 是 SQL WHERE 子句（不包含 WHERE 关键字），请确保参数安全以防注入
        /// </summary>
        /// <param name="condition">SQL 条件（不含 WHERE）</param>
        /// <param name="entity">用于更新的实体对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>受影响的实体数量</returns>
        public async Task<int> EditWhereAsync(string condition, T entity, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(condition)) throw new ArgumentNullException(nameof(condition));

            var ids = new List<int>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                var sql = $"SELECT [Id] FROM [{_tableName}] WHERE {condition}";
                using var cmd = new SqliteCommand(sql, connection);
                using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (reader[0] != DBNull.Value)
                        ids.Add(Convert.ToInt32(reader[0]));
                }
            }

            foreach (var id in ids)
            {
                await EditByIdAsync(id, entity, cancellationToken).ConfigureAwait(false);
            }

            return ids.Count;
        }

        /// <summary>
        /// 根据 Linq 表达式异步更新所有匹配的实体（不修改 Id 字段）
        /// </summary>
        /// <param name="predicate">匹配条件表达式</param>
        /// <param name="entity">用于更新的实体对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>受影响的实体数量</returns>
        public async Task<int> EditWhereAsync(Expression<Func<T, bool>> predicate, T entity, CancellationToken cancellationToken = default)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            if (TryTranslatePredicate(predicate.Body, out var whereClause, out var parameters))
            {
                var ids = new List<int>();
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                    var sql = $"SELECT [Id] FROM [{_tableName}] WHERE {whereClause}";
                    using var cmd = new SqliteCommand(sql, connection);
                    foreach (var kv in parameters)
                    {
                        cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);
                    }
                    using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        if (reader[0] != DBNull.Value)
                            ids.Add(Convert.ToInt32(reader[0]));
                    }
                }

                foreach (var id in ids)
                {
                    await EditByIdAsync(id, entity, cancellationToken).ConfigureAwait(false);
                }
                return ids.Count;
            }
            else
            {
                // 回退到内存过滤
                var all = await GetAllAsync(cancellationToken).ConfigureAwait(false);
                var func = predicate.Compile();
                var matchingIds = all.Where(func).Select(e => e.Id).ToList();
                foreach (var id in matchingIds)
                {
                    await EditByIdAsync(id, entity, cancellationToken).ConfigureAwait(false);
                }
                return matchingIds.Count;
            }
        }

        /// <summary>
        /// 按指定 ID 异步编辑实体（不会修改 Id 字段）
        /// </summary>
        /// <param name="id">实体ID</param>
        /// <param name="entity">用于更新的实体对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        public async Task EditByIdAsync(int id, T entity, CancellationToken cancellationToken = default)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            var mainProperties = _properties.Where(kvp =>
                !_dataTableProperties.ContainsKey(kvp.Key) &&
                !_dataTableListProperties.ContainsKey(kvp.Key) &&
                !_arrayProperties.ContainsKey(kvp.Key) &&
                !_dictionaryProperties.ContainsKey(kvp.Key) &&
                !_linkedListProperties.ContainsKey(kvp.Key) &&
                IsSimpleType(kvp.Value.PropertyType) &&
                kvp.Key != "Id");

            if (!mainProperties.Any()) return;

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var transactionObj = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var transaction = transactionObj as SqliteTransaction ?? throw new InvalidOperationException("SqliteTransaction 获取失败");
            try
            {
                var sets = string.Join(", ", mainProperties.Select(kvp => $"[{kvp.Key}] = @{kvp.Key}"));
                var sql = $"UPDATE [{_tableName}] SET {sets} WHERE [Id] = @id";
                await using (var cmd = new SqliteCommand(sql, connection, transaction))
                {
                    var getterDict = GetterCache[typeof(T)];
                    foreach (var prop in mainProperties)
                    {
                        var rawValue = getterDict[prop.Key](entity);
                        var value = ToDbDateTimeString(rawValue, prop.Value.PropertyType) ?? DBNull.Value;
                        cmd.Parameters.AddWithValue($"@{prop.Key}", value);
                    }
                    cmd.Parameters.AddWithValue("@id", id);
                    await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                // 同步一对一子表（先删除旧的，再插入新的，确保一致性）
                foreach (var dataTableProp in _dataTableProperties)
                {
                    var childTable = $"{_tableName}_{dataTableProp.Key}";
                    // 删除旧记录
                    var deleteSql = $"DELETE FROM [{childTable}] WHERE [ParentId] = @parentId";
                    await using (var deleteCmd = new SqliteCommand(deleteSql, connection, transaction))
                    {
                        deleteCmd.Parameters.AddWithValue("@parentId", id);
                        await deleteCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }

                    var childEntity = dataTableProp.Value.GetValue(entity) as IDataTable;
                    if (childEntity != null)
                    {
                        childEntity.ParentId = id;
                        await InsertChildEntityAsync(connection, transaction, childEntity, childTable, cancellationToken).ConfigureAwait(false);
                    }
                }

                // 同步一对多子表（删除旧的再插入）
                foreach (var dataTableListProp in _dataTableListProperties)
                {
                    var childList = dataTableListProp.Value.GetValue(entity) as IEnumerable;
                    if (childList != null)
                    {
                        var tableName = $"{_tableName}_{dataTableListProp.Key}";
                        var deleteSql = $"DELETE FROM [{tableName}] WHERE [ParentId] = @parentId";
                        await using (var deleteCommand = new SqliteCommand(deleteSql, connection, transaction))
                        {
                            deleteCommand.Parameters.AddWithValue("@parentId", id);
                            await deleteCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                        }

                        foreach (IDataTable childEntity in childList)
                        {
                            if (childEntity != null)
                            {
                                childEntity.ParentId = id;
                                await InsertChildEntityAsync(connection, transaction, childEntity, tableName, cancellationToken).ConfigureAwait(false);
                            }
                        }
                    }
                }

                // 处理数组属性（同样删除旧的再插入）
                foreach (var arrayProp in _arrayProperties)
                {
                    var arr = arrayProp.Value.GetValue(entity) as Array;
                    if (arr != null)
                    {
                        var tableName = $"{_tableName}_{arrayProp.Key}";
                        var deleteSql = $"DELETE FROM [{tableName}] WHERE [ParentId] = @parentId";
                        await using (var deleteCommand = new SqliteCommand(deleteSql, connection, transaction))
                        {
                            deleteCommand.Parameters.AddWithValue("@parentId", id);
                            await deleteCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                        }

                        foreach (var elem in arr)
                        {
                            if (elem != null)
                            {
                                if (IsComplexType(elem.GetType()))
                                {
                                    var pi = elem.GetType().GetProperty("ParentId");
                                    if (pi != null) pi.SetValue(elem, id);
                                }
                                if (elem is IDataTable dtElem)
                                {
                                    await InsertChildEntityAsync(connection, transaction, dtElem, tableName, cancellationToken).ConfigureAwait(false);
                                }
                                else
                                {
                                    // 基础类型或未实现IDataTable，使用代理表插入
                                    InsertSimpleTypeCollectionElement(connection, transaction, tableName, id, elem);
                                }
                            }
                        }
                    }
                }

                // 处理链表属性
                foreach (var linkedProp in _linkedListProperties)
                {
                    var linked = linkedProp.Value.GetValue(entity) as System.Collections.IEnumerable;
                    if (linked != null)
                    {
                        var tableName = $"{_tableName}_{linkedProp.Key}";
                        var elemType = linkedProp.Value.PropertyType.GetGenericArguments()[0];

                        var deleteSql = $"DELETE FROM [{tableName}] WHERE [ParentId] = @parentId";
                        await using (var deleteCommand = new SqliteCommand(deleteSql, connection, transaction))
                        {
                            deleteCommand.Parameters.AddWithValue("@parentId", id);
                            await deleteCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                        }

                        foreach (var elem in linked)
                        {
                            if (elem != null)
                            {
                                if (IsSimpleType(elemType))
                                {
                                    InsertSimpleTypeCollectionElement(connection, transaction, tableName, id, elem);
                                }
                                else if (elem is IDataTable dtElem)
                                {
                                    dtElem.ParentId = id;
                                    await InsertChildEntityAsync(connection, transaction, dtElem, tableName, cancellationToken).ConfigureAwait(false);
                                }
                                else if (IsComplexType(elem.GetType()))
                                {
                                    var pi = elem.GetType().GetProperty("ParentId");
                                    if (pi != null) pi.SetValue(elem, id);
                                }
                            }
                        }
                    }
                }

                // 处理字典属性
                foreach (var dictProp in _dictionaryProperties)
                {
                    var dict = dictProp.Value.GetValue(entity);
                    if (dict is System.Collections.IDictionary idict)
                    {
                        var tableName = $"{_tableName}_{dictProp.Key}";
                        var deleteSql = $"DELETE FROM [{tableName}] WHERE [ParentId] = @parentId";
                        await using (var deleteCommand = new SqliteCommand(deleteSql, connection, transaction))
                        {
                            deleteCommand.Parameters.AddWithValue("@parentId", id);
                            await deleteCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                        }

                        foreach (System.Collections.DictionaryEntry entry in idict)
                        {
                            var surrogate = new DictionaryEntrySurrogate
                            {
                                ParentId = id,
                                DictKey = entry.Key?.ToString() ?? "",
                                DictValue = entry.Value?.ToString() ?? ""
                            };
                            InsertChildEntity(connection, transaction, surrogate, tableName);

                            if (entry.Value != null && IsComplexType(entry.Value.GetType()))
                            {
                                var valueTableName = $"{tableName}_Value";
                                if (entry.Value.GetType().GetProperty("ParentId") != null)
                                {
                                    entry.Value.GetType().GetProperty("ParentId")?.SetValue(entry.Value, id);
                                }
                                if (entry.Value is IDataTable dtVal)
                                {
                                    await InsertChildEntityAsync(connection, transaction, dtVal, valueTableName, cancellationToken).ConfigureAwait(false);
                                }
                                else
                                {
                                    var surrogateVal = new DictionaryEntrySurrogate
                                    {
                                        ParentId = id,
                                        DictKey = entry.Key?.ToString() ?? "",
                                        DictValue = entry.Value?.ToString() ?? ""
                                    };
                                    InsertChildEntity(connection, transaction, surrogateVal, valueTableName);
                                }
                            }
                        }
                    }
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }
        /// <summary>
        /// 根据任意 SQL 条件更新所有匹配的实体（不修改 Id 字段）
        /// 注意：condition 是 SQL WHERE 子句（不包含 WHERE 关键字），请确保参数安全以防注入
        /// </summary>
        public int EditWhere(string condition, T entity)
        {
            if (string.IsNullOrEmpty(condition)) throw new ArgumentNullException(nameof(condition));

            var ids = new List<int>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var sql = $"SELECT [Id] FROM [{_tableName}] WHERE {condition}";
                using var cmd = new SqliteCommand(sql, connection);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    if (reader[0] != DBNull.Value)
                        ids.Add(Convert.ToInt32(reader[0]));
                }
            }

            foreach (var id in ids)
            {
                EditById(id, entity);
            }

            return ids.Count;
        }

        /// <summary>
        /// 根据 Linq 表达式更新所有匹配的实体（不修改 Id 字段）
        /// </summary>
        /// <param name="predicate">匹配条件表达式</param>
        /// <param name="entity">用于更新的实体对象</param>
        /// <returns>受影响的实体数量</returns>
        public int EditWhere(Expression<Func<T, bool>> predicate, T entity)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            if (TryTranslatePredicate(predicate.Body, out var whereClause, out var parameters))
            {
                var ids = new List<int>();
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    var sql = $"SELECT [Id] FROM [{_tableName}] WHERE {whereClause}";
                    using var cmd = new SqliteCommand(sql, connection);
                    foreach (var kv in parameters)
                    {
                        cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);
                    }
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        if (reader[0] != DBNull.Value)
                            ids.Add(Convert.ToInt32(reader[0]));
                    }
                }

                foreach (var id in ids)
                {
                    EditById(id, entity);
                }
                return ids.Count;
            }
            else
            {
                // 回退到内存过滤
                var all = GetAll();
                var func = predicate.Compile();
                var matchingIds = all.Where(func).Select(e => e.Id).ToList();
                foreach (var id in matchingIds)
                {
                    EditById(id, entity);
                }
                return matchingIds.Count;
            }
        }

        /// <summary>
        /// 根据 Id 删除实体（包装已有 Delete 方法，保持语义）
        /// </summary>
        public bool DeleteById(int id)
        {
            return Delete(id);
        }

        /// <summary>
        /// 安全地删除子表中符合特定条件的记录（条件由内部构建，防止 SQL 注入）
        /// 通过参数化查询和白名单字段来构建删除条件
        /// </summary>
        /// <param name="childTableName">子表名称（必须是已知的表名）</param>
        /// <param name="fieldName">字段名（必须是已知的字段）</param>
        /// <param name="operatorType">操作符：=, <>, <, >, <=, >=, IN</param>
        /// <param name="values">条件值</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>删除的记录数</returns>
        private async Task<int> DeleteChildRecordsInternalAsync(string childTableName, string fieldName,
            string operatorType, object?[] values, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(childTableName))
                throw new ArgumentNullException(nameof(childTableName));
            if (string.IsNullOrWhiteSpace(fieldName))
                throw new ArgumentNullException(nameof(fieldName));
            if (values == null || values.Length == 0)
                throw new ArgumentNullException(nameof(values));

            // 验证标识符（防止注入）
            static bool IsValidIdentifier(string s) => !string.IsNullOrEmpty(s) && System.Text.RegularExpressions.Regex.IsMatch(s, "^[A-Za-z_][A-Za-z0-9_]*$");
            if (!IsValidIdentifier(childTableName) || !IsValidIdentifier(fieldName))
                throw new ArgumentException("表名或字段名包含非法字符");

            // 白名单验证：确保操作符合法（支持 NOT IN）
            var validOperators = new[] { "=", "<>", "<", ">", "<=", ">=", "IN", "NOT IN" };
            if (!validOperators.Contains(operatorType))
                throw new ArgumentException($"操作符 '{operatorType}' 无效");

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            string sql;
            using var cmd = new SqliteCommand { Connection = connection };

            // 处理 IN / NOT IN 的空数组边界情况
            if ((operatorType == "IN" || operatorType == "NOT IN") && (values == null || values.Length == 0))
            {
                // IN () 应匹配无记录 -> 不删除任何记录
                // NOT IN () 应匹配所有记录 -> 删除全部记录
                if (operatorType == "IN")
                {
                    sql = $"DELETE FROM [{childTableName}] WHERE 1=0"; // 删除0条
                }
                else // NOT IN && values.Length == 0
                {
                    sql = $"DELETE FROM [{childTableName}] WHERE 1=1"; // 删除全部
                }
                cmd.CommandText = sql;
                return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            if (operatorType == "IN" || operatorType == "NOT IN")
            {
                // IN/NOT IN 操作符需要逐个参数化
                var placeholders = string.Join(",", Enumerable.Range(0, values.Length).Select(i => $"@p{i}"));
                sql = $"DELETE FROM [{childTableName}] WHERE [{fieldName}] {operatorType} ({placeholders})";
                for (int i = 0; i < values.Length; i++)
                    cmd.Parameters.AddWithValue($"@p{i}", values[i] ?? DBNull.Value);
            }
            else
            {
                // 其他操作符只需单个值
                sql = $"DELETE FROM [{childTableName}] WHERE [{fieldName}] {operatorType} @val";
                cmd.Parameters.AddWithValue("@val", values[0] ?? DBNull.Value);
            }

            cmd.CommandText = sql;
            return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 安全删除子表记录：通过字段匹配条件（参数化防注入）
        /// 用于特定场景清理，如清除过期/无效/已删除的子表数据
        /// </summary>
        /// <param name="childTableName">子表名称</param>
        /// <param name="fieldName">子表字段名</param>
        /// <param name="operatorType">操作符（=, <>, <, >, <=, >=, IN）</param>
        /// <param name="values">条件值数组</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>删除的记录数</returns>
        public async Task<int> DeleteChildRecordsAsync(string childTableName, string fieldName,
            string operatorType, object?[] values, CancellationToken cancellationToken = default)
        {
            return await DeleteChildRecordsInternalAsync(childTableName, fieldName, operatorType, values, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 根据属性名和值删除所有匹配的实体及其关联数据
        /// </summary>
        public int DeleteWhere(string propertyName, object propertyValue)
        {
            if (string.IsNullOrEmpty(propertyName)) throw new ArgumentNullException(nameof(propertyName));
            if (!_properties.ContainsKey(propertyName))
                throw new ArgumentException($"属性 {propertyName} 不存在于类型 {typeof(T).Name} 中");

            var ids = new List<int>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var sql = $"SELECT [Id] FROM [{_tableName}] WHERE [{propertyName}] = @value";
                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@value", propertyValue ?? DBNull.Value);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    if (reader[0] != DBNull.Value)
                        ids.Add(Convert.ToInt32(reader[0]));
                }
            }

            var deleted = 0;
            foreach (var id in ids)
            {
                if (Delete(id)) deleted++;
            }
            return deleted;
        }

        /// <summary>
        /// 根据属性名和值异步删除所有匹配的实体及其关联数据
        /// </summary>
        /// <param name="propertyName">属性名</param>
        /// <param name="propertyValue">属性值</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>删除的实体数量</returns>
        public async Task<int> DeleteWhereAsync(string propertyName, object propertyValue, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(propertyName)) throw new ArgumentNullException(nameof(propertyName));
            if (!_properties.ContainsKey(propertyName))
                throw new ArgumentException($"属性 {propertyName} 不存在于类型 {typeof(T).Name} 中");

            var ids = new List<int>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                var sql = $"SELECT [Id] FROM [{_tableName}] WHERE [{propertyName}] = @value";
                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@value", propertyValue ?? DBNull.Value);
                using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (reader[0] != DBNull.Value)
                        ids.Add(Convert.ToInt32(reader[0]));
                }
            }

            var deleted = 0;
            foreach (var id in ids)
            {
                if (await DeleteAsync(id, cancellationToken).ConfigureAwait(false)) deleted++;
            }
            return deleted;
        }

        /// <summary>
        /// 根据任意 SQL 条件异步删除所有匹配的实体及其关联数据
        /// </summary>
        /// <param name="condition">SQL 条件（不含 WHERE）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>删除的实体数量</returns>
        public async Task<int> DeleteWhereAsync(string condition, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(condition)) throw new ArgumentNullException(nameof(condition));

            var ids = new List<int>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                var sql = $"SELECT [Id] FROM [{_tableName}] WHERE {condition}";
                using var cmd = new SqliteCommand(sql, connection);
                using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (reader[0] != DBNull.Value)
                        ids.Add(Convert.ToInt32(reader[0]));
                }
            }

            var deleted = 0;
            foreach (var id in ids)
            {
                if (await DeleteAsync(id, cancellationToken).ConfigureAwait(false)) deleted++;
            }
            return deleted;
        }

        /// <summary>
        /// 根据 Linq 表达式异步删除所有匹配的实体及其关联数据
        /// </summary>
        /// <param name="predicate">匹配条件表达式</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>删除的实体数量</returns>
        public async Task<int> DeleteWhereAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            if (TryTranslatePredicate(predicate.Body, out var whereClause, out var parameters))
            {
                var ids = new List<int>();
                using (var connection = new SqliteConnection(_connectionString))
                {
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                    var sql = $"SELECT [Id] FROM [{_tableName}] WHERE {whereClause}";
                    using var cmd = new SqliteCommand(sql, connection);
                    foreach (var kv in parameters)
                    {
                        cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);
                    }
                    using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        if (reader[0] != DBNull.Value)
                            ids.Add(Convert.ToInt32(reader[0]));
                    }
                }

                var deleted = 0;
                foreach (var id in ids)
                {
                    if (await DeleteAsync(id, cancellationToken).ConfigureAwait(false)) deleted++;
                }
                return deleted;
            }
            else
            {
                // 回退到内存过滤
                var all = await GetAllAsync(cancellationToken).ConfigureAwait(false);
                var func = predicate.Compile();
                var matchingIds = all.Where(func).Select(e => e.Id).ToList();
                var deleted = 0;
                foreach (var id in matchingIds)
                {
                    if (await DeleteAsync(id, cancellationToken).ConfigureAwait(false)) deleted++;
                }
                return deleted;
            }
        }

        /// <summary>
        /// 根据 Linq 表达式删除所有匹配的实体及其关联数据
        /// </summary>
        /// <param name="predicate">匹配条件表达式</param>
        /// <returns>删除的实体数量</returns>
        public int DeleteWhere(Expression<Func<T, bool>> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            if (TryTranslatePredicate(predicate.Body, out var whereClause, out var parameters))
            {
                var ids = new List<int>();
                using (var connection = new SqliteConnection(_connectionString))
                {
                    connection.Open();
                    var sql = $"SELECT [Id] FROM [{_tableName}] WHERE {whereClause}";
                    using var cmd = new SqliteCommand(sql, connection);
                    foreach (var kv in parameters)
                    {
                        cmd.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);
                    }
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        if (reader[0] != DBNull.Value)
                            ids.Add(Convert.ToInt32(reader[0]));
                    }
                }

                var deleted = 0;
                foreach (var id in ids)
                {
                    if (Delete(id)) deleted++;
                }
                return deleted;
            }
            else
            {
                // 回退到内存过滤
                var all = GetAll();
                var func = predicate.Compile();
                var matchingIds = all.Where(func).Select(e => e.Id).ToList();
                var deleted = 0;
                foreach (var id in matchingIds)
                {
                    if (Delete(id)) deleted++;
                }
                return deleted;
            }
        }

        /// <summary>
        /// 从 SqliteDataReader 创建实体
        /// </summary>
        private T CreateEntityFromReader(SqliteDataReader reader)
        {
            var entity = new T();

            var type = typeof(T);
            var setterDict = SetterCache[type];
            // 预先获取 reader 的列集合，避免在循环内重复调用 GetName
            var readerColumns = GetReaderColumns(reader);

            foreach (var prop in _properties.Where(kvp =>
                !_dataTableProperties.ContainsKey(kvp.Key) &&
                !_dataTableListProperties.ContainsKey(kvp.Key) &&
                !_arrayProperties.ContainsKey(kvp.Key) &&
                !_dictionaryProperties.ContainsKey(kvp.Key) &&
                !_linkedListProperties.ContainsKey(kvp.Key) &&
                IsSimpleType(kvp.Value.PropertyType))) // 只处理简单类型
            {
                var columnName = prop.Key;
                if (readerColumns.Contains(columnName))
                {
                    var value = reader[columnName];
                    if (value != DBNull.Value)
                    {
                        // 类型转换
                        var convertedValue = ConvertValue(value, prop.Value.PropertyType);
                        setterDict[prop.Key](entity, convertedValue);
                    }
                }
            }

            return entity;
        }

        /// <summary>
        /// 加载子表数据
        /// </summary>
        private void LoadChildEntities(SqliteConnection connection, T entity)
        {
            // 加载一对一关系
            foreach (var dataTableProp in _dataTableProperties)
            {
                var childTableName = $"{_tableName}_{dataTableProp.Key}";
                var childType = dataTableProp.Value.PropertyType;

                var sql = $"SELECT * FROM [{childTableName}] WHERE [ParentId] = @parentId";
                using var command = new SqliteCommand(sql, connection);
                command.Parameters.AddWithValue("@parentId", entity.Id);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    // 缓存工厂用于快速实例化
                    var childFactory = ChildTypeFactoryCache.GetOrAdd(childType, t =>
                    {
                        var ctor = t.GetConstructor(Type.EmptyTypes);
                        if (ctor == null) throw new InvalidOperationException($"类型 {t.FullName} 缺少无参构造函数");
                        var exp = System.Linq.Expressions.Expression.Lambda<Func<object>>(System.Linq.Expressions.Expression.New(ctor));
                        return exp.Compile();
                    });

                    var childEntity = childFactory() as IDataTable;
                    if (childEntity != null)
                    {
                        // 预取列集合和缓存属性列表
                        var readerColumns = GetReaderColumns(reader);
                        var childProperties = ChildTypePropertiesCache.GetOrAdd(childType, t => t.GetProperties().Where(p => p.CanRead && p.CanWrite).ToArray());

                        foreach (var prop in childProperties)
                        {
                            if (readerColumns.Contains(prop.Name))
                            {
                                var value = reader[prop.Name];
                                if (value != DBNull.Value)
                                {
                                    var convertedValue = ConvertValue(value, prop.PropertyType);
                                    prop.SetValue(childEntity, convertedValue);
                                }
                            }
                        }

                        // 加载被 [Publish] 标记且同名字段覆盖主表字段（已支持一对一）
                        foreach (var prop in childProperties)
                        {
                            if (prop.CanRead && prop.CanWrite &&
                                prop.GetCustomAttribute(typeof(PublishAttribute)) != null &&
                                _properties.ContainsKey(prop.Name))
                            {
                                var value = prop.GetValue(childEntity);
                                _properties[prop.Name].SetValue(entity, value);
                            }
                        }

                        // 用 setter 委托赋值主表属性
                        var setterDict = SetterCache[entity.GetType()];
                        setterDict[dataTableProp.Key](entity, childEntity);
                    }
                }
            }

            // 加载一对多关系，自动加载所有主表映射字段的最新子表值（覆盖主表同名字段，取最后一个值）
            foreach (var dataTableListProp in _dataTableListProperties)
            {
                var childTableName = $"{_tableName}_{dataTableListProp.Key}";
                var childType = GetDataTableListElementType(dataTableListProp.Value.PropertyType);

                var sql = $"SELECT * FROM [{childTableName}] WHERE [ParentId] = @parentId";
                using var command = new SqliteCommand(sql, connection);
                command.Parameters.AddWithValue("@parentId", entity.Id);

                var childList = Activator.CreateInstance(dataTableListProp.Value.PropertyType) as IList;
                // 用于记录每个字段的最后一个值
                var lastPublishValues = new Dictionary<string, object?>();

                if (childList != null)
                {
                    using var reader = command.ExecuteReader();
                    // 预取列集合、实例工厂与属性缓存
                    var readerColumns = GetReaderColumns(reader);
                    var childFactory = ChildTypeFactoryCache.GetOrAdd(childType, t =>
                    {
                        var ctor = t.GetConstructor(Type.EmptyTypes);
                        if (ctor == null) throw new InvalidOperationException($"类型 {t.FullName} 缺少无参构造函数");
                        var exp = System.Linq.Expressions.Expression.Lambda<Func<object>>(System.Linq.Expressions.Expression.New(ctor));
                        return exp.Compile();
                    });
                    var childProperties = ChildTypePropertiesCache.GetOrAdd(childType, t => t.GetProperties().Where(p => p.CanRead && p.CanWrite).ToArray());

                    while (reader.Read())
                    {
                        var childEntity = childFactory() as IDataTable;
                        if (childEntity != null)
                        {
                            foreach (var prop in childProperties)
                            {
                                if (readerColumns.Contains(prop.Name))
                                {
                                    var value = reader[prop.Name];
                                    if (value != DBNull.Value)
                                    {
                                        var convertedValue = ConvertValue(value, prop.PropertyType);
                                        prop.SetValue(childEntity, convertedValue);
                                    }
                                }
                            }

                            // 收集被 [Publish] 标记且主表同名字段的最新值
                            foreach (var prop in childProperties)
                            {
                                if (prop.CanRead && prop.CanWrite &&
                                    prop.GetCustomAttribute(typeof(PublishAttribute)) != null &&
                                    _properties.ContainsKey(prop.Name))
                                {
                                    lastPublishValues[prop.Name] = prop.GetValue(childEntity);
                                }
                            }

                            childList.Add(childEntity);
                        }
                    }

                    // 覆盖主表同名字段（多表取最后一个值）
                    foreach (var kv in lastPublishValues)
                    {
                        _properties[kv.Key].SetValue(entity, kv.Value);
                    }

                    dataTableListProp.Value.SetValue(entity, childList);
                }
            }
        }

        /// <summary>
        /// 检查 reader 是否包含指定列
        /// </summary>
        private bool HasColumn(SqliteDataReader reader, string columnName)
        {
            // 兼容旧调用：构建字段集合并检查
            var cols = GetReaderColumns(reader);
            return cols.Contains(columnName);
        }

        /// <summary>
        /// 将 reader 的字段名构建为 HashSet（忽略大小写），调用方应尽量复用返回值以提升性能
        /// </summary>
        private HashSet<string> GetReaderColumns(SqliteDataReader reader)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                set.Add(reader.GetName(i));
            }
            return set;
        }

        /// <summary>
        /// 值类型转换（支持 DateTime 字段反序列化）
        /// </summary>
        private object? ConvertValue(object value, Type targetType)
        {
            if (value == null || value == DBNull.Value)
                return null;

            // 处理可空类型
            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                var underlyingType = Nullable.GetUnderlyingType(targetType);
                if (underlyingType != null)
                {
                    targetType = underlyingType;
                }
            }

            // 特殊处理 DateTime
            if ((targetType == typeof(DateTime) || targetType == typeof(DateTime?)))
            {
                return ParseDbDateTimeString(value, targetType);
            }

            // 特殊处理 Boolean
            if (targetType == typeof(bool) && value is long longValue)
            {
                return longValue != 0;
            }

            // 特殊处理枚举类型
            if (targetType.IsEnum)
            {
                var str = value.ToString();
                if (string.IsNullOrEmpty(str))
                    throw new ArgumentException($"无法将空值转换为枚举类型 {targetType.Name}");
                return Enum.Parse(targetType, str);
            }

            return Convert.ChangeType(value, targetType);
        }

        /// <summary>
        /// 将数据库中的字符串安全转换为 DateTime/DateTime?
        /// </summary>
        private static object? ParseDbDateTimeString(object value, Type targetType)
        {
            if (value == null || value == DBNull.Value)
                return targetType == typeof(DateTime) ? DateTime.MinValue : null;

            var str = value.ToString();
            if (string.IsNullOrWhiteSpace(str))
                return targetType == typeof(DateTime) ? DateTime.MinValue : null;

            if (DateTime.TryParse(str, out var dt))
                return dt;

            return targetType == typeof(DateTime) ? DateTime.MinValue : null;
        }

        /// <summary>
        /// 将 DateTime/DateTime? 转为数据库存储字符串
        /// </summary>
        private static object? ToDbDateTimeString(object? value, Type type)
        {
            if (type == typeof(DateTime) || type == typeof(DateTime?))
            {
                if (value == null)
                    return null;
                var dt = (DateTime)value;
                // 若为默认值，存 null
                if (dt == DateTime.MinValue)
                    return null;
                return dt.ToString("yyyy-MM-dd HH:mm:ss.fffffff");
            }
            return value;
        }

        /// <summary>
        /// 删除实体
        /// </summary>
        /// <param name="id">要删除的实体ID</param>
        /// <returns>是否删除成功</returns>
        public bool Delete(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                var affectedRows = 0;

                // 删除关联表数据（一对一）
                foreach (var dataTableProp in _dataTableProperties)
                {
                    var childTableName = $"{_tableName}_{dataTableProp.Key}";
                    var deleteSql = $"DELETE FROM [{childTableName}] WHERE [ParentId] = @id";
                    using var deleteCommand = new SqliteCommand(deleteSql, connection, transaction);
                    deleteCommand.Parameters.AddWithValue("@id", id);
                    deleteCommand.ExecuteNonQuery();
                }

                // 删除关联表数据（一对多）
                foreach (var dataTableListProp in _dataTableListProperties)
                {
                    var childTableName = $"{_tableName}_{dataTableListProp.Key}";
                    var deleteSql = $"DELETE FROM [{childTableName}] WHERE [ParentId] = @id";
                    using var deleteCommand = new SqliteCommand(deleteSql, connection, transaction);
                    deleteCommand.Parameters.AddWithValue("@id", id);
                    deleteCommand.ExecuteNonQuery();
                }

                // 删除主表数据
                var mainDeleteSql = $"DELETE FROM [{_tableName}] WHERE [Id] = @id";
                using var mainDeleteCommand = new SqliteCommand(mainDeleteSql, connection, transaction);
                mainDeleteCommand.Parameters.AddWithValue("@id", id);
                affectedRows = mainDeleteCommand.ExecuteNonQuery();

                transaction.Commit();
                return affectedRows > 0;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<IEnumerable<T>> QueryAllAsync(CancellationToken cancellationToken = default)
        {
            return await GetAllAsync(cancellationToken);
        }

        public List<T> QueryAll()
        {
            return GetAll();
        }

        /// <summary>
        /// 获取所有数据
        /// </summary>
        /// <returns>所有实体列表</returns>
        public List<T> GetAll()
        {
            var results = new List<T>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var sql = $"SELECT * FROM [{_tableName}]";
            using var command = new SqliteCommand(sql, connection);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var entity = CreateEntityFromReader(reader);

                // 加载关联表数据
                LoadChildEntities(connection, entity);

                results.Add(entity);
            }

            return results;
        }

        /// <summary>
        /// 检查类型是否为 List<IDataTable> 类型
        /// </summary>
        private bool IsDataTableList(Type propertyType)
        {
            if (!propertyType.IsGenericType) return false;

            var genericTypeDef = propertyType.GetGenericTypeDefinition();
            if (genericTypeDef != typeof(List<>) && genericTypeDef != typeof(IList<>) &&
                genericTypeDef != typeof(ICollection<>) && genericTypeDef != typeof(IEnumerable<>))
                return false;

            var genericArg = propertyType.GetGenericArguments()[0];
            return typeof(IDataTable).IsAssignableFrom(genericArg);
        }

        // 新增：判断是否为数组属性
        private bool IsArrayProperty(Type propertyType)
        {
            return propertyType.IsArray && IsComplexType(propertyType.GetElementType());
        }

        // 新增：判断是否为字典属性
        private bool IsDictionaryProperty(Type propertyType)
        {
            if (!propertyType.IsGenericType) return false;
            var genericTypeDef = propertyType.GetGenericTypeDefinition();
            if (genericTypeDef != typeof(Dictionary<,>)) return false;
            var args = propertyType.GetGenericArguments();
            // 只支持Key为基础类型，Value为复杂类型或基础类型
            return IsSimpleType(args[0]) && (IsSimpleType(args[1]) || IsComplexType(args[1]));
        }

        // 新增：判断是否为链表属性
        private bool IsLinkedListProperty(Type propertyType)
        {
            if (!propertyType.IsGenericType) return false;
            var genericTypeDef = propertyType.GetGenericTypeDefinition();
            return genericTypeDef == typeof(LinkedList<>);
        }

        // 新增：判断是否为复杂类型（非基础类型、非string）
        private bool IsComplexType(Type? type)
        {
            if (type == null) return false;
            return !(type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime));
        }

        // 新增：判断是否为简单类型
        private bool IsSimpleType(Type? type)
        {
            if (type == null) return false;

            // 处理可空类型
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                type = Nullable.GetUnderlyingType(type);
                if (type == null) return false;
            }

            return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime);
        }

        // 新增：字典子表结构代理
        private class DictionaryEntrySurrogate : IDataTable
        {
            public int Id { get; set; }
            public int ParentId { get; set; }
            public string DictKey { get; set; }
            public string DictValue { get; set; }
            public string TableName => "DictionaryEntry";
        }

        /// <summary>
        /// 获取List<IDataTable>中的元素类型
        /// </summary>
        private Type GetDataTableListElementType(Type listType)
        {
            return listType.GetGenericArguments()[0];
        }

        #region Async Methods

        /// <summary>
        /// 异步将实体推送到数据库
        /// </summary>
        public async Task PushAsync(T entity, CancellationToken cancellationToken = default)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (entity.Id == 0)
            {
                entity.Id = (int)(DateTime.UtcNow.Ticks % int.MaxValue);
            }

            // 新增：同步一对一子表被 [Publish] 标记且同名字段到主表
            foreach (var dataTableProp in _dataTableProperties)
            {
                var childEntity = dataTableProp.Value.GetValue(entity) as IDataTable;
                if (childEntity != null)
                {
                    foreach (var prop in childEntity.GetType().GetProperties())
                    {
                        if (prop.CanRead && prop.CanWrite &&
                            prop.GetCustomAttribute(typeof(PublishAttribute)) != null &&
                            _properties.ContainsKey(prop.Name))
                        {
                            var value = prop.GetValue(childEntity);
                            _properties[prop.Name].SetValue(entity, value);
                        }
                    }
                }
            }
            // 新增：同步一对多子表被 [Publish] 标记且同名字段到主表（取最后一个子表值）
            foreach (var dataTableListProp in _dataTableListProperties)
            {
                var childList = dataTableListProp.Value.GetValue(entity) as IEnumerable;
                if (childList != null)
                {
                    object? lastPublishValue = null;
                    string? publishFieldName = null;
                    foreach (IDataTable childEntity in childList)
                    {
                        if (childEntity != null)
                        {
                            foreach (var prop in childEntity.GetType().GetProperties())
                            {
                                if (prop.CanRead && prop.CanWrite &&
                                    prop.GetCustomAttribute(typeof(PublishAttribute)) != null &&
                                    _properties.ContainsKey(prop.Name))
                                {
                                    lastPublishValue = prop.GetValue(childEntity);
                                    publishFieldName = prop.Name;
                                }
                            }
                        }
                    }
                    if (publishFieldName != null && lastPublishValue != null)
                    {
                        _properties[publishFieldName].SetValue(entity, lastPublishValue);
                    }
                }
            }

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transactionObj = await connection.BeginTransactionAsync(cancellationToken);
            var transaction = transactionObj as SqliteTransaction ?? throw new InvalidOperationException("SqliteTransaction 获取失败");
            try
            {
                await InsertMainEntityAsync(connection, transaction, entity, cancellationToken);
                // 一对一
                foreach (var dataTableProp in _dataTableProperties)
                {
                    var childEntity = dataTableProp.Value.GetValue(entity) as IDataTable;
                    if (childEntity != null)
                    {
                        childEntity.ParentId = entity.Id;
                        await InsertChildEntityAsync(connection, transaction, childEntity, $"{_tableName}_{dataTableProp.Key}", cancellationToken);
                    }
                }
                // 一对多
                foreach (var dataTableListProp in _dataTableListProperties)
                {
                    var childList = dataTableListProp.Value.GetValue(entity) as IEnumerable;
                    if (childList != null)
                    {
                        var tableName = $"{_tableName}_{dataTableListProp.Key}";
                        var deleteSql = $"DELETE FROM [{tableName}] WHERE [ParentId] = @parentId";
                        await using (var deleteCommand = new SqliteCommand(deleteSql, connection, transaction))
                        {
                            deleteCommand.Parameters.AddWithValue("@parentId", entity.Id);
                            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
                        }
                        foreach (IDataTable childEntity in childList)
                        {
                            if (childEntity != null)
                            {
                                childEntity.ParentId = entity.Id;
                                await InsertChildEntityAsync(connection, transaction, childEntity, tableName, cancellationToken);
                            }
                        }
                    }
                }
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        /// <summary>
        /// 异步插入主实体数据
        /// </summary>
        private async Task InsertMainEntityAsync(SqliteConnection connection, SqliteTransaction transaction, T entity, CancellationToken cancellationToken)
        {
            var mainProperties = _properties.Where(kvp =>
                !_dataTableProperties.ContainsKey(kvp.Key) &&
                !_dataTableListProperties.ContainsKey(kvp.Key) &&
                !_arrayProperties.ContainsKey(kvp.Key) &&
                !_dictionaryProperties.ContainsKey(kvp.Key) &&
                !_linkedListProperties.ContainsKey(kvp.Key) &&
                IsSimpleType(kvp.Value.PropertyType)); // 只处理简单类型

            var columns = string.Join(", ", mainProperties.Select(kvp => $"[{kvp.Key}]"));
            var parameters = string.Join(", ", mainProperties.Select(kvp => $"@{kvp.Key}"));
            var sql = $"INSERT OR REPLACE INTO [{_tableName}] ({columns}) VALUES ({parameters})";
            await using var command = new SqliteCommand(sql, connection, transaction);
            foreach (var prop in mainProperties)
            {
                var rawValue = prop.Value.GetValue(entity);
                var value = ToDbDateTimeString(rawValue, prop.Value.PropertyType) ?? DBNull.Value;
                command.Parameters.AddWithValue($"@{prop.Key}", value);
            }
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>
        /// 异步插入子表数据
        /// </summary>
        private async Task InsertChildEntityAsync(SqliteConnection connection, SqliteTransaction transaction, IDataTable childEntity, string tableName, CancellationToken cancellationToken)
        {
            var childType = childEntity.GetType();

            // 检查子实体是否有Id属性，如果有且为0，则生成新的Id
            var idProperty = childType.GetProperty("Id");
            if (idProperty != null && idProperty.CanRead && idProperty.CanWrite)
            {
                var currentId = idProperty.GetValue(childEntity);
                if (currentId is int intId && intId == 0)
                {
                    var newId = (int)(DateTime.UtcNow.Ticks % int.MaxValue);
                    idProperty.SetValue(childEntity, newId);
                }
            }

            var properties = childType.GetProperties().Where(p => p.CanRead && p.CanWrite);
            var columns = string.Join(", ", properties.Select(p => $"[{p.Name}]"));
            var parameters = string.Join(", ", properties.Select(p => $"@{p.Name}"));
            var sql = $"INSERT OR REPLACE INTO [{tableName}] ({columns}) VALUES ({parameters})";
            await using var command = new SqliteCommand(sql, connection, transaction);

            // 确保子类型的 getter/setter 已被缓存
            if (!GetterCache.ContainsKey(childType))
            {
                CacheAccessors(childType);
            }

            var getterDict = GetterCache[childType];
            foreach (var prop in properties)
            {
                var rawValue = getterDict[prop.Name](childEntity);
                var value = ToDbDateTimeString(rawValue, prop.PropertyType) ?? DBNull.Value;
                command.Parameters.AddWithValue($"@{prop.Name}", value);
            }
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        /// <summary>
        /// 异步查询数据
        /// </summary>
        public async Task<List<T>> QueryAsync(string propertyName, object propertyValue, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(propertyName)) throw new ArgumentNullException(nameof(propertyName));
            if (!_properties.ContainsKey(propertyName))
                throw new ArgumentException($"属性 {propertyName} 不存在于类型 {typeof(T).Name} 中");
            var results = new List<T>();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            var sql = $"SELECT * FROM [{_tableName}] WHERE [{propertyName}] = @value";
            await using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@value", propertyValue ?? DBNull.Value);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var entity = CreateEntityFromReader(reader);
                await LoadChildEntitiesAsync(connection, entity, cancellationToken);
                results.Add(entity);
            }
            return results;
        }

        /// <summary>
        /// 异步根据ID查询单个实体
        /// </summary>
        public async Task<T?> QueryByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var results = await QueryAsync("Id", id, cancellationToken);
            return results.FirstOrDefault();
        }

        /// <summary>
        /// 异步更新实体（等同于PushAsync）
        /// </summary>
        public async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (entity.Id == 0) throw new ArgumentException("实体ID不能为0");
            await PushAsync(entity, cancellationToken);
        }

        /// <summary>
        /// 异步加载子表数据
        /// </summary>
        private async Task LoadChildEntitiesAsync(SqliteConnection connection, T entity, CancellationToken cancellationToken)
        {
            // 一对一
            foreach (var dataTableProp in _dataTableProperties)
            {
                var childTableName = $"{_tableName}_{dataTableProp.Key}";
                var childType = dataTableProp.Value.PropertyType;
                var sql = $"SELECT * FROM [{childTableName}] WHERE [ParentId] = @parentId";
                await using var command = new SqliteCommand(sql, connection);
                command.Parameters.AddWithValue("@parentId", entity.Id);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    // 优先用缓存的无参构造委托
                    var childFactory = ChildTypeFactoryCache.GetOrAdd(childType, t =>
                    {
                        var ctor = t.GetConstructor(Type.EmptyTypes);
                        if (ctor == null) throw new InvalidOperationException($"类型 {t.FullName} 缺少无参构造函数");
                        var exp = System.Linq.Expressions.Expression.Lambda<Func<object>>(System.Linq.Expressions.Expression.New(ctor));
                        return exp.Compile();
                    });
                    var childEntity = childFactory() as IDataTable;
                    if (childEntity != null)
                    {
                        // 优先用缓存的属性列表
                        var childProperties = ChildTypePropertiesCache.GetOrAdd(childType, t => t.GetProperties().Where(p => p.CanRead && p.CanWrite).ToArray());
                        foreach (var prop in childProperties)
                        {
                            if (HasColumn(reader, prop.Name))
                            {
                                var value = reader[prop.Name];
                                if (value != DBNull.Value)
                                {
                                    var convertedValue = ConvertValue(value, prop.PropertyType);
                                    prop.SetValue(childEntity, convertedValue);
                                }
                            }
                        }
                        dataTableProp.Value.SetValue(entity, childEntity);
                    }
                }
            }
            // 一对多
            foreach (var dataTableListProp in _dataTableListProperties)
            {
                var childTableName = $"{_tableName}_{dataTableListProp.Key}";
                var childType = GetDataTableListElementType(dataTableListProp.Value.PropertyType);
                var sql = $"SELECT * FROM [{childTableName}] WHERE [ParentId] = @parentId";
                await using var command = new SqliteCommand(sql, connection);
                command.Parameters.AddWithValue("@parentId", entity.Id);
                var childList = Activator.CreateInstance(dataTableListProp.Value.PropertyType) as IList;
                if (childList != null)
                {
                    await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                    // 缓存工厂和属性
                    var childFactory = ChildTypeFactoryCache.GetOrAdd(childType, t =>
                    {
                        var ctor = t.GetConstructor(Type.EmptyTypes);
                        if (ctor == null) throw new InvalidOperationException($"类型 {t.FullName} 缺少无参构造函数");
                        var exp = System.Linq.Expressions.Expression.Lambda<Func<object>>(System.Linq.Expressions.Expression.New(ctor));
                        return exp.Compile();
                    });
                    var childProperties = ChildTypePropertiesCache.GetOrAdd(childType, t => t.GetProperties().Where(p => p.CanRead && p.CanWrite).ToArray());
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var childEntity = childFactory() as IDataTable;
                        if (childEntity != null)
                        {
                            foreach (var prop in childProperties)
                            {
                                if (HasColumn(reader, prop.Name))
                                {
                                    var value = reader[prop.Name];
                                    if (value != DBNull.Value)
                                    {
                                        var convertedValue = ConvertValue(value, prop.PropertyType);
                                        prop.SetValue(childEntity, convertedValue);
                                    }
                                }
                            }
                            childList.Add(childEntity);
                        }
                    }
                    dataTableListProp.Value.SetValue(entity, childList);
                }
            }
        }

        /// <summary>
        /// 异步删除实体
        /// </summary>
        public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transactionObj = await connection.BeginTransactionAsync(cancellationToken);
            var transaction = transactionObj as SqliteTransaction ?? throw new InvalidOperationException("SqliteTransaction 获取失败");
            try
            {
                var affectedRows = 0;
                // 一对一
                foreach (var dataTableProp in _dataTableProperties)
                {
                    var childTableName = $"{_tableName}_{dataTableProp.Key}";
                    var deleteSql = $"DELETE FROM [{childTableName}] WHERE [ParentId] = @id";
                    await using (var deleteCommand = new SqliteCommand(deleteSql, connection, transaction))
                    {
                        deleteCommand.Parameters.AddWithValue("@id", id);
                        await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
                    }
                }
                // 一对多
                foreach (var dataTableListProp in _dataTableListProperties)
                {
                    var childTableName = $"{_tableName}_{dataTableListProp.Key}";
                    var deleteSql = $"DELETE FROM [{childTableName}] WHERE [ParentId] = @id";
                    await using (var deleteCommand = new SqliteCommand(deleteSql, connection, transaction))
                    {
                        deleteCommand.Parameters.AddWithValue("@id", id);
                        await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
                    }
                }
                // 主表
                var mainDeleteSql = $"DELETE FROM [{_tableName}] WHERE [Id] = @id";
                await using (var mainDeleteCommand = new SqliteCommand(mainDeleteSql, connection, transaction))
                {
                    mainDeleteCommand.Parameters.AddWithValue("@id", id);
                    affectedRows = await mainDeleteCommand.ExecuteNonQueryAsync(cancellationToken);
                }
                await transaction.CommitAsync(cancellationToken);
                return affectedRows > 0;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        /// <summary>
        /// 异步获取所有数据
        /// </summary>
        public async Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var results = new List<T>();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            var sql = $"SELECT * FROM [{_tableName}]";
            await using var command = new SqliteCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var entity = CreateEntityFromReader(reader);
                await LoadChildEntitiesAsync(connection, entity, cancellationToken);
                results.Add(entity);
            }
            return results;
        }

        /// <summary>
        /// 异步清空数据库表中的所有数据。
        /// </summary>
        /// <param name="cancellationToken">用于取消操作的令牌。</param>
        /// <returns>表示异步操作的任务。</returns>
        /// <exception cref="InvalidOperationException">当无法获取有效的 SqliteTransaction 时抛出。</exception>
        /// <remarks>
        /// 此方法会打开数据库连接，启动事务，执行删除操作，并在成功时提交事务，失败时回滚事务。
        /// </remarks>
        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transactionObj = await connection.BeginTransactionAsync(cancellationToken);
            var transaction = transactionObj as SqliteTransaction ?? throw new InvalidOperationException("SqliteTransaction 获取失败");
            try
            {
                var deleteSql = $"DELETE FROM [{_tableName}]";
                await using (var deleteCommand = new SqliteCommand(deleteSql, connection, transaction))
                {
                    await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
                }
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        /// <summary>
        /// 异步查询并返回匹配的第一个实体（不会返回列表）。
        /// </summary>
        /// <param name="propertyName">主表要匹配的属性名</param>
        /// <param name="propertyValue">要匹配的属性值</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>匹配到的实体或 null</returns>
        public async Task<T?> QueryFirstAsync(string propertyName, object propertyValue, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(propertyName)) throw new ArgumentNullException(nameof(propertyName));
            if (!_properties.ContainsKey(propertyName))
                throw new ArgumentException($"属性 {propertyName} 不存在于类型 {typeof(T).Name} 中");

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // 使用 LIMIT 1 优化，只读取第一条匹配记录
            var sql = $"SELECT * FROM [{_tableName}] WHERE [{propertyName}] = @value LIMIT 1";
            await using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@value", propertyValue ?? DBNull.Value);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var entity = CreateEntityFromReader(reader);
                // 加载关联表数据（一对一/一对多等）
                await LoadChildEntitiesAsync(connection, entity, cancellationToken);
                return entity;
            }

            return null;
        }

        /// <summary>
        /// 支持传入表达式的异步查询（尝试将简单表达式翻译为 SQL，否则回退到内存过滤）
        /// </summary>
        public async Task<T?> QueryFirstAsync(System.Linq.Expressions.Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            // 尝试翻译表达式为 SQL 条件和参数
            if (TryTranslatePredicate(predicate.Body, out var whereClause, out var parameters))
            {
                await using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync(cancellationToken);
                var sql = $"SELECT * FROM [{_tableName}] WHERE {whereClause} LIMIT 1";
                await using var command = new SqliteCommand(sql, connection);
                foreach (var kv in parameters)
                {
                    command.Parameters.AddWithValue(kv.Key, kv.Value ?? DBNull.Value);
                }
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    var entity = CreateEntityFromReader(reader);
                    await LoadChildEntitiesAsync(connection, entity, cancellationToken);
                    return entity;
                }
                return null;
            }

            // 无法翻译为 SQL：回退到加载所有数据并使用表达式在内存中过滤
            var all = await GetAllAsync(cancellationToken);
            var func = predicate.Compile();
            return all.FirstOrDefault(func);
        }

        /// <summary>
        /// 尝试把表达式树翻译为 SQL WHERE 子句（支持简单的 等于/不等 与 &&/|| 组合，右侧可为捕获变量）
        /// 返回 where 子句和参数字典（参数名以 @pN 形式）
        /// </summary>
        private bool TryTranslatePredicate(System.Linq.Expressions.Expression expr, out string whereClause, out Dictionary<string, object?> parameters)
        {
            whereClause = string.Empty;
            parameters = new Dictionary<string, object?>();
            try
            {
                var sb = new System.Text.StringBuilder();
                int paramIndex = 0;
                if (!BuildClause(expr, sb, ref paramIndex, parameters))
                {
                    return false;
                }
                whereClause = sb.ToString();
                return true;
            }
            catch
            {
                whereClause = string.Empty;
                parameters = new Dictionary<string, object?>();
                return false;
            }
        }

        /// <summary>
        /// 递归构建 SQL 子句，支持 Equal, NotEqual, AndAlso, OrElse
        /// </summary>
        private bool BuildClause(System.Linq.Expressions.Expression expr, System.Text.StringBuilder sb, ref int paramIndex, Dictionary<string, object?> parameters)
        {
            if (expr is System.Linq.Expressions.BinaryExpression be)
            {
                if (be.NodeType == System.Linq.Expressions.ExpressionType.AndAlso || be.NodeType == System.Linq.Expressions.ExpressionType.OrElse)
                {
                    sb.Append('(');
                    if (!BuildClause(be.Left, sb, ref paramIndex, parameters)) return false;
                    sb.Append(be.NodeType == System.Linq.Expressions.ExpressionType.AndAlso ? " AND " : " OR ");
                    if (!BuildClause(be.Right, sb, ref paramIndex, parameters)) return false;
                    sb.Append(')');
                    return true;
                }

                if (be.NodeType == System.Linq.Expressions.ExpressionType.Equal || be.NodeType == System.Linq.Expressions.ExpressionType.NotEqual)
                {
                    // 支持 左为 param.Property, 右为 常量/捕获变量，或反过来
                    var left = be.Left;
                    var right = be.Right;
                    string column = null;
                    object? value = null;
                    // helper to evaluate expression to object
                    object? Eval(System.Linq.Expressions.Expression e)
                    {
                        var lambda = System.Linq.Expressions.Expression.Lambda<Func<object>>(System.Linq.Expressions.Expression.Convert(e, typeof(object)));
                        var func = lambda.Compile();
                        return func();
                    }

                    if (IsMemberAccessToParameter(left, out var leftName))
                    {
                        column = leftName;
                        value = Eval(right);
                    }
                    else if (IsMemberAccessToParameter(right, out var rightName))
                    {
                        column = rightName;
                        value = Eval(left);
                    }
                    else
                    {
                        return false; // 无法识别的格式
                    }

                    if (column == null) return false;
                    var paramName = $"@p{paramIndex++}";
                    parameters[paramName] = value;
                    sb.Append($"[{column}] ");
                    sb.Append(be.NodeType == System.Linq.Expressions.ExpressionType.Equal ? "= " : "<> ");
                    sb.Append(paramName);
                    return true;
                }
            }

            return false; // 其它类型暂不支持
        }

        /// <summary>
        /// 判断表达式是否为参数的成员访问（例如 u.UserName），若是返回属性名
        /// </summary>
        private bool IsMemberAccessToParameter(System.Linq.Expressions.Expression expr, out string? propertyName)
        {
            propertyName = null;
            if (expr is System.Linq.Expressions.MemberExpression me)
            {
                // 要求 MemberExpression.Expression 是参数或经过 Convert 的参数
                var inner = me.Expression;
                if (inner is System.Linq.Expressions.ParameterExpression)
                {
                    propertyName = me.Member.Name;
                    return true;
                }
                if (inner is System.Linq.Expressions.UnaryExpression ue && ue.Operand is System.Linq.Expressions.ParameterExpression)
                {
                    propertyName = me.Member.Name;
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region 直接字段操作方法（无需序列化对象）

        /// <summary>
        /// 同步从主表获取指定ID的单个字段值
        /// </summary>
        /// <typeparam name="TField">字段值的类型</typeparam>
        /// <param name="id">实体ID</param>
        /// <param name="fieldName">字段名</param>
        /// <returns>字段值，如果不存在返回null</returns>
        public TField? GetField<TField>(int id, string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName))
                throw new ArgumentNullException(nameof(fieldName));
            if (!_properties.ContainsKey(fieldName))
                throw new ArgumentException($"属性 {fieldName} 不存在于类型 {typeof(T).Name} 中");

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var sql = $"SELECT [{fieldName}] FROM [{_tableName}] WHERE [Id] = @id LIMIT 1";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@id", id);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var value = reader[0];
                return value == DBNull.Value ? default : (TField?)ConvertValue(value, typeof(TField));
            }

            return default;
        }

        /// <summary>
        /// 异步从主表获取指定ID的单个字段值
        /// </summary>
        /// <typeparam name="TField">字段值的类型</typeparam>
        /// <param name="id">实体ID</param>
        /// <param name="fieldName">字段名</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>字段值，如果不存在返回null</returns>
        public async Task<TField?> GetFieldAsync<TField>(int id, string fieldName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(fieldName))
                throw new ArgumentNullException(nameof(fieldName));
            if (!_properties.ContainsKey(fieldName))
                throw new ArgumentException($"属性 {fieldName} 不存在于类型 {typeof(T).Name} 中");

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var sql = $"SELECT [{fieldName}] FROM [{_tableName}] WHERE [Id] = @id LIMIT 1";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@id", id);

            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var value = reader[0];
                return value == DBNull.Value ? default : (TField?)ConvertValue(value, typeof(TField));
            }

            return default;
        }

        /// <summary>
        /// 同步按条件获取第一个匹配实体的字段值
        /// </summary>
        /// <typeparam name="TField">字段值的类型</typeparam>
        /// <param name="propertyName">查询条件的属性名</param>
        /// <param name="propertyValue">查询条件的属性值</param>
        /// <param name="fieldName">要获取的字段名</param>
        /// <returns>字段值，如果不存在返回null</returns>
        public TField? GetFieldWhere<TField>(string propertyName, object propertyValue, string fieldName)
        {
            if (string.IsNullOrEmpty(propertyName))
                throw new ArgumentNullException(nameof(propertyName));
            if (string.IsNullOrEmpty(fieldName))
                throw new ArgumentNullException(nameof(fieldName));
            if (!_properties.ContainsKey(propertyName))
                throw new ArgumentException($"属性 {propertyName} 不存在于类型 {typeof(T).Name} 中");
            if (!_properties.ContainsKey(fieldName))
                throw new ArgumentException($"属性 {fieldName} 不存在于类型 {typeof(T).Name} 中");

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var sql = $"SELECT [{fieldName}] FROM [{_tableName}] WHERE [{propertyName}] = @value LIMIT 1";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@value", propertyValue ?? DBNull.Value);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var value = reader[0];
                return value == DBNull.Value ? default : (TField?)ConvertValue(value, typeof(TField));
            }

            return default;
        }

        /// <summary>
        /// 异步按条件获取第一个匹配实体的字段值
        /// </summary>
        /// <typeparam name="TField">字段值的类型</typeparam>
        /// <param name="propertyName">查询条件的属性名</param>
        /// <param name="propertyValue">查询条件的属性值</param>
        /// <param name="fieldName">要获取的字段名</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>字段值，如果不存在返回null</returns>
        public async Task<TField?> GetFieldWhereAsync<TField>(string propertyName, object propertyValue, string fieldName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(propertyName))
                throw new ArgumentNullException(nameof(propertyName));
            if (string.IsNullOrEmpty(fieldName))
                throw new ArgumentNullException(nameof(fieldName));
            if (!_properties.ContainsKey(propertyName))
                throw new ArgumentException($"属性 {propertyName} 不存在于类型 {typeof(T).Name} 中");
            if (!_properties.ContainsKey(fieldName))
                throw new ArgumentException($"属性 {fieldName} 不存在于类型 {typeof(T).Name} 中");

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var sql = $"SELECT [{fieldName}] FROM [{_tableName}] WHERE [{propertyName}] = @value LIMIT 1";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@value", propertyValue ?? DBNull.Value);

            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var value = reader[0];
                return value == DBNull.Value ? default : (TField?)ConvertValue(value, typeof(TField));
            }

            return default;
        }

        /// <summary>
        /// 同步更新指定ID的单个字段值
        /// </summary>
        /// <param name="id">实体ID</param>
        /// <param name="fieldName">字段名</param>
        /// <param name="value">要设置的值</param>
        /// <returns>是否更新成功</returns>
        public bool UpdateField(int id, string fieldName, object? value)
        {
            if (string.IsNullOrEmpty(fieldName))
                throw new ArgumentNullException(nameof(fieldName));
            if (!_properties.ContainsKey(fieldName))
                throw new ArgumentException($"属性 {fieldName} 不存在于类型 {typeof(T).Name} 中");
            if (fieldName == "Id")
                throw new ArgumentException("不能修改 Id 字段");

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var sql = $"UPDATE [{_tableName}] SET [{fieldName}] = @value WHERE [Id] = @id";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@value", value ?? DBNull.Value);
            command.Parameters.AddWithValue("@id", id);

            return command.ExecuteNonQuery() > 0;
        }

        /// <summary>
        /// 异步更新指定ID的单个字段值
        /// </summary>
        /// <param name="id">实体ID</param>
        /// <param name="fieldName">字段名</param>
        /// <param name="value">要设置的值</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否更新成功</returns>
        public async Task<bool> UpdateFieldAsync(int id, string fieldName, object? value, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(fieldName))
                throw new ArgumentNullException(nameof(fieldName));
            if (!_properties.ContainsKey(fieldName))
                throw new ArgumentException($"属性 {fieldName} 不存在于类型 {typeof(T).Name} 中");
            if (fieldName == "Id")
                throw new ArgumentException("不能修改 Id 字段");

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var sql = $"UPDATE [{_tableName}] SET [{fieldName}] = @value WHERE [Id] = @id";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@value", value ?? DBNull.Value);
            command.Parameters.AddWithValue("@id", id);

            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
        }

        /// <summary>
        /// 同步批量更新满足条件的实体的单个字段
        /// </summary>
        /// <param name="conditionProperty">条件属性名</param>
        /// <param name="conditionValue">条件值</param>
        /// <param name="fieldName">要更新的字段名</param>
        /// <param name="value">新值</param>
        /// <returns>受影响的实体数量</returns>
        public int UpdateFieldWhere(string conditionProperty, object conditionValue, string fieldName, object? value)
        {
            if (string.IsNullOrEmpty(conditionProperty))
                throw new ArgumentNullException(nameof(conditionProperty));
            if (string.IsNullOrEmpty(fieldName))
                throw new ArgumentNullException(nameof(fieldName));
            if (!_properties.ContainsKey(conditionProperty))
                throw new ArgumentException($"属性 {conditionProperty} 不存在于类型 {typeof(T).Name} 中");
            if (!_properties.ContainsKey(fieldName))
                throw new ArgumentException($"属性 {fieldName} 不存在于类型 {typeof(T).Name} 中");
            if (fieldName == "Id")
                throw new ArgumentException("不能修改 Id 字段");

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var sql = $"UPDATE [{_tableName}] SET [{fieldName}] = @value WHERE [{conditionProperty}] = @condition";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@value", value ?? DBNull.Value);
            command.Parameters.AddWithValue("@condition", conditionValue ?? DBNull.Value);

            return command.ExecuteNonQuery();
        }

        /// <summary>
        /// 异步批量更新满足条件的实体的单个字段
        /// </summary>
        /// <param name="conditionProperty">条件属性名</param>
        /// <param name="conditionValue">条件值</param>
        /// <param name="fieldName">要更新的字段名</param>
        /// <param name="value">新值</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>受影响的实体数量</returns>
        public async Task<int> UpdateFieldWhereAsync(string conditionProperty, object conditionValue, string fieldName, object? value, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(conditionProperty))
                throw new ArgumentNullException(nameof(conditionProperty));
            if (string.IsNullOrEmpty(fieldName))
                throw new ArgumentNullException(nameof(fieldName));
            if (!_properties.ContainsKey(conditionProperty))
                throw new ArgumentException($"属性 {conditionProperty} 不存在于类型 {typeof(T).Name} 中");
            if (!_properties.ContainsKey(fieldName))
                throw new ArgumentException($"属性 {fieldName} 不存在于类型 {typeof(T).Name} 中");
            if (fieldName == "Id")
                throw new ArgumentException("不能修改 Id 字段");

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var sql = $"UPDATE [{_tableName}] SET [{fieldName}] = @value WHERE [{conditionProperty}] = @condition";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@value", value ?? DBNull.Value);
            command.Parameters.AddWithValue("@condition", conditionValue ?? DBNull.Value);

            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 同步获取表中的记录数
        /// </summary>
        /// <returns>记录数</returns>
        public int Count()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var sql = $"SELECT COUNT(*) FROM [{_tableName}]";
            using var command = new SqliteCommand(sql, connection);

            var result = command.ExecuteScalar();
            return result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
        }

        /// <summary>
        /// 异步获取表中的记录数
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>记录数</returns>
        public async Task<int> CountAsync(CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var sql = $"SELECT COUNT(*) FROM [{_tableName}]";
            using var command = new SqliteCommand(sql, connection);

            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
        }

        /// <summary>
        /// 同步按条件计数
        /// </summary>
        /// <param name="propertyName">条件属性名</param>
        /// <param name="propertyValue">条件值</param>
        /// <returns>匹配条件的记录数</returns>
        public int CountWhere(string propertyName, object propertyValue)
        {
            if (string.IsNullOrEmpty(propertyName))
                throw new ArgumentNullException(nameof(propertyName));
            if (!_properties.ContainsKey(propertyName))
                throw new ArgumentException($"属性 {propertyName} 不存在于类型 {typeof(T).Name} 中");

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var sql = $"SELECT COUNT(*) FROM [{_tableName}] WHERE [{propertyName}] = @value";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@value", propertyValue ?? DBNull.Value);

            var result = command.ExecuteScalar();
            return result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
        }

        /// <summary>
        /// 异步按条件计数
        /// </summary>
        /// <param name="propertyName">条件属性名</param>
        /// <param name="propertyValue">条件值</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>匹配条件的记录数</returns>
        public async Task<int> CountWhereAsync(string propertyName, object propertyValue, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(propertyName))
                throw new ArgumentNullException(nameof(propertyName));
            if (!_properties.ContainsKey(propertyName))
                throw new ArgumentException($"属性 {propertyName} 不存在于类型 {typeof(T).Name} 中");

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var sql = $"SELECT COUNT(*) FROM [{_tableName}] WHERE [{propertyName}] = @value";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@value", propertyValue ?? DBNull.Value);

            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
        }

        /// <summary>
        /// 同步检查是否存在指定ID的实体
        /// </summary>
        /// <param name="id">实体ID</param>
        /// <returns>是否存在</returns>
        public bool Exists(int id)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var sql = $"SELECT 1 FROM [{_tableName}] WHERE [Id] = @id LIMIT 1";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@id", id);

            using var reader = command.ExecuteReader();
            return reader.Read();
        }

        /// <summary>
        /// 异步检查是否存在指定ID的实体
        /// </summary>
        /// <param name="id">实体ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否存在</returns>
        public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var sql = $"SELECT 1 FROM [{_tableName}] WHERE [Id] = @id LIMIT 1";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@id", id);

            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 同步按条件检查是否存在符合条件的实体
        /// </summary>
        /// <param name="propertyName">条件属性名</param>
        /// <param name="propertyValue">条件值</param>
        /// <returns>是否存在</returns>
        public bool ExistsWhere(string propertyName, object propertyValue)
        {
            if (string.IsNullOrEmpty(propertyName))
                throw new ArgumentNullException(nameof(propertyName));
            if (!_properties.ContainsKey(propertyName))
                throw new ArgumentException($"属性 {propertyName} 不存在于类型 {typeof(T).Name} 中");

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var sql = $"SELECT 1 FROM [{_tableName}] WHERE [{propertyName}] = @value LIMIT 1";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@value", propertyValue ?? DBNull.Value);

            using var reader = command.ExecuteReader();
            return reader.Read();
        }

        /// <summary>
        /// 异步按条件检查是否存在符合条件的实体
        /// </summary>
        /// <param name="propertyName">条件属性名</param>
        /// <param name="propertyValue">条件值</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否存在</returns>
        public async Task<bool> ExistsWhereAsync(string propertyName, object propertyValue, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(propertyName))
                throw new ArgumentNullException(nameof(propertyName));
            if (!_properties.ContainsKey(propertyName))
                throw new ArgumentException($"属性 {propertyName} 不存在于类型 {typeof(T).Name} 中");

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var sql = $"SELECT 1 FROM [{_tableName}] WHERE [{propertyName}] = @value LIMIT 1";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@value", propertyValue ?? DBNull.Value);

            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 同步获取多个字段的值（返回 Dictionary）
        /// </summary>
        /// <param name="id">实体ID</param>
        /// <param name="fieldNames">字段名集合</param>
        /// <returns>字段名到值的映射字典</returns>
        public Dictionary<string, object?> GetFields(int id, params string[] fieldNames)
        {
            if (fieldNames == null || fieldNames.Length == 0)
                throw new ArgumentNullException(nameof(fieldNames));

            var result = new Dictionary<string, object?>();
            foreach (var fieldName in fieldNames)
            {
                if (string.IsNullOrEmpty(fieldName))
                    throw new ArgumentException("字段名不能为空");
                if (!_properties.ContainsKey(fieldName))
                    throw new ArgumentException($"属性 {fieldName} 不存在于类型 {typeof(T).Name} 中");
            }

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var fields = string.Join(", ", fieldNames.Select(f => $"[{f}]"));
            var sql = $"SELECT {fields} FROM [{_tableName}] WHERE [Id] = @id LIMIT 1";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@id", id);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                for (int i = 0; i < fieldNames.Length; i++)
                {
                    var value = reader[i];
                    result[fieldNames[i]] = value == DBNull.Value ? null : value;
                }
            }

            return result;
        }

        /// <summary>
        /// 异步获取多个字段的值（返回 Dictionary）
        /// </summary>
        /// <param name="id">实体ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="fieldNames">字段名集合</param>
        /// <returns>字段名到值的映射字典</returns>
        public async Task<Dictionary<string, object?>> GetFieldsAsync(int id, CancellationToken cancellationToken = default, params string[] fieldNames)
        {
            if (fieldNames == null || fieldNames.Length == 0)
                throw new ArgumentNullException(nameof(fieldNames));

            var result = new Dictionary<string, object?>();
            foreach (var fieldName in fieldNames)
            {
                if (string.IsNullOrEmpty(fieldName))
                    throw new ArgumentException("字段名不能为空");
                if (!_properties.ContainsKey(fieldName))
                    throw new ArgumentException($"属性 {fieldName} 不存在于类型 {typeof(T).Name} 中");
            }

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var fields = string.Join(", ", fieldNames.Select(f => $"[{f}]"));
            var sql = $"SELECT {fields} FROM [{_tableName}] WHERE [Id] = @id LIMIT 1";
            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@id", id);

            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                for (int i = 0; i < fieldNames.Length; i++)
                {
                    var value = reader[i];
                    result[fieldNames[i]] = value == DBNull.Value ? null : value;
                }
            }

            return result;
        }

        /// <summary>
        /// 同步查询子表（按 ParentId）——返回列名->值 的字典列表（表级操作，无需反序列化）
        /// 子表命名约定：`{MainTableName}_{PropertyName}`，子表通常包含 `ParentId` 列。
        /// </summary>
        public List<Dictionary<string, object?>> QueryChildByParentId(string childTableName, int parentId)
        {
            if (string.IsNullOrWhiteSpace(childTableName)) throw new ArgumentNullException(nameof(childTableName));
            // 验证表名（防注入）
            if (!System.Text.RegularExpressions.Regex.IsMatch(childTableName, "^[A-Za-z_][A-Za-z0-9_]*$"))
                throw new ArgumentException("childTableName 包含非法字符");

            var results = new List<Dictionary<string, object?>>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var sql = $"SELECT * FROM [{childTableName}] WHERE [ParentId] = @parentId";
            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@parentId", parentId);

            using var reader = cmd.ExecuteReader();
            var cols = new List<string>();
            for (int i = 0; i < reader.FieldCount; i++) cols.Add(reader.GetName(i));

            while (reader.Read())
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < cols.Count; i++)
                {
                    var v = reader.GetValue(i);
                    row[cols[i]] = v == DBNull.Value ? null : v;
                }
                results.Add(row);
            }

            return results;
        }

        /// <summary>
        /// 异步查询子表（按 ParentId）
        /// </summary>
        public async Task<List<Dictionary<string, object?>>> QueryChildByParentIdAsync(string childTableName, int parentId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(childTableName)) throw new ArgumentNullException(nameof(childTableName));
            if (!System.Text.RegularExpressions.Regex.IsMatch(childTableName, "^[A-Za-z_][A-Za-z0-9_]*$"))
                throw new ArgumentException("childTableName 包含非法字符");

            var results = new List<Dictionary<string, object?>>();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var sql = $"SELECT * FROM [{childTableName}] WHERE [ParentId] = @parentId";
            await using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@parentId", parentId);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var cols = new List<string>();
            for (int i = 0; i < reader.FieldCount; i++) cols.Add(reader.GetName(i));

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < cols.Count; i++)
                {
                    var v = reader.GetValue(i);
                    row[cols[i]] = v == DBNull.Value ? null : v;
                }
                results.Add(row);
            }

            return results;
        }

        /// <summary>
        /// 同步按子表字段查询（等值匹配），返回行字典列表（表级操作）
        /// </summary>
        public List<Dictionary<string, object?>> QueryChildWhere(string childTableName, string fieldName, object? value)
        {
            if (string.IsNullOrWhiteSpace(childTableName)) throw new ArgumentNullException(nameof(childTableName));
            if (string.IsNullOrWhiteSpace(fieldName)) throw new ArgumentNullException(nameof(fieldName));
            if (!System.Text.RegularExpressions.Regex.IsMatch(childTableName, "^[A-Za-z_][A-Za-z0-9_]*$"))
                throw new ArgumentException("childTableName 包含非法字符");
            if (!System.Text.RegularExpressions.Regex.IsMatch(fieldName, "^[A-Za-z_][A-Za-z0-9_]*$"))
                throw new ArgumentException("fieldName 包含非法字符");

            var results = new List<Dictionary<string, object?>>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var sql = $"SELECT * FROM [{childTableName}] WHERE [{fieldName}] = @val";
            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@val", value ?? DBNull.Value);

            using var reader = cmd.ExecuteReader();
            var cols = new List<string>();
            for (int i = 0; i < reader.FieldCount; i++) cols.Add(reader.GetName(i));

            while (reader.Read())
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < cols.Count; i++)
                {
                    var v = reader.GetValue(i);
                    row[cols[i]] = v == DBNull.Value ? null : v;
                }
                results.Add(row);
            }

            return results;
        }

        /// <summary>
        /// 异步按子表字段查询（等值匹配）
        /// </summary>
        public async Task<List<Dictionary<string, object?>>> QueryChildWhereAsync(string childTableName, string fieldName, object? value, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(childTableName)) throw new ArgumentNullException(nameof(childTableName));
            if (string.IsNullOrWhiteSpace(fieldName)) throw new ArgumentNullException(nameof(fieldName));
            if (!System.Text.RegularExpressions.Regex.IsMatch(childTableName, "^[A-Za-z_][A-Za-z0-9_]*$"))
                throw new ArgumentException("childTableName 包含非法字符");
            if (!System.Text.RegularExpressions.Regex.IsMatch(fieldName, "^[A-Za-z_][A-Za-z0-9_]*$"))
                throw new ArgumentException("fieldName 包含非法字符");

            var results = new List<Dictionary<string, object?>>();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var sql = $"SELECT * FROM [{childTableName}] WHERE [{fieldName}] = @val";
            await using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@val", value ?? DBNull.Value);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var cols = new List<string>();
            for (int i = 0; i < reader.FieldCount; i++) cols.Add(reader.GetName(i));

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < cols.Count; i++)
                {
                    var v = reader.GetValue(i);
                    row[cols[i]] = v == DBNull.Value ? null : v;
                }
                results.Add(row);
            }

            return results;
        }

        /// <summary>
        /// 同步统计子表中某 ParentId 的记录数
        /// </summary>
        public int CountChildByParentId(string childTableName, int parentId)
        {
            if (string.IsNullOrWhiteSpace(childTableName)) throw new ArgumentNullException(nameof(childTableName));
            if (!System.Text.RegularExpressions.Regex.IsMatch(childTableName, "^[A-Za-z_][A-Za-z0-9_]*$"))
                throw new ArgumentException("childTableName 包含非法字符");

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var sql = $"SELECT COUNT(*) FROM [{childTableName}] WHERE [ParentId] = @parentId";
            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@parentId", parentId);
            var res = cmd.ExecuteScalar();
            return res != null && res != DBNull.Value ? Convert.ToInt32(res) : 0;
        }

        /// <summary>
        /// 异步统计子表中某 ParentId 的记录数
        /// </summary>
        public async Task<int> CountChildByParentIdAsync(string childTableName, int parentId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(childTableName)) throw new ArgumentNullException(nameof(childTableName));
            if (!System.Text.RegularExpressions.Regex.IsMatch(childTableName, "^[A-Za-z_][A-Za-z0-9_]*$"))
                throw new ArgumentException("childTableName 包含非法字符");

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            var sql = $"SELECT COUNT(*) FROM [{childTableName}] WHERE [ParentId] = @parentId";
            await using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@parentId", parentId);
            var res = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return res != null && res != DBNull.Value ? Convert.ToInt32(res) : 0;
        }

        /// <summary>
        /// 同步检查子表中是否存在匹配 ParentId 的记录
        /// </summary>
        public bool ExistsChildByParentId(string childTableName, int parentId)
        {
            if (string.IsNullOrWhiteSpace(childTableName)) throw new ArgumentNullException(nameof(childTableName));
            if (!System.Text.RegularExpressions.Regex.IsMatch(childTableName, "^[A-Za-z_][A-Za-z0-9_]*$"))
                throw new ArgumentException("childTableName 包含非法字符");

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var sql = $"SELECT 1 FROM [{childTableName}] WHERE [ParentId] = @parentId LIMIT 1";
            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@parentId", parentId);
            using var reader = cmd.ExecuteReader();
            return reader.Read();
        }

        /// <summary>
        /// 异步检查子表中是否存在匹配 ParentId 的记录
        /// </summary>
        public async Task<bool> ExistsChildByParentIdAsync(string childTableName, int parentId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(childTableName)) throw new ArgumentNullException(nameof(childTableName));
            if (!System.Text.RegularExpressions.Regex.IsMatch(childTableName, "^[A-Za-z_][A-Za-z0-9_]*$"))
                throw new ArgumentException("childTableName 包含非法字符");

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            var sql = $"SELECT 1 FROM [{childTableName}] WHERE [ParentId] = @parentId LIMIT 1";
            await using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@parentId", parentId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }

        #region 子表字段读取与更新（表级操作，无需序列化）

        // 辅助：检查表中是否存在某列（同步）
        private bool DoesTableHaveColumn(string tableName, string columnName)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (string.IsNullOrWhiteSpace(columnName)) throw new ArgumentNullException(nameof(columnName));
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var cmd = new SqliteCommand($"PRAGMA table_info([{tableName}])", connection);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var nm = reader["name"] as string;
                if (string.Equals(nm, columnName, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        // 辅助：检查表中是否存在某列（异步）
        private async Task<bool> DoesTableHaveColumnAsync(string tableName, string columnName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (string.IsNullOrWhiteSpace(columnName)) throw new ArgumentNullException(nameof(columnName));
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var cmd = new SqliteCommand($"PRAGMA table_info([{tableName}])", connection);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var nm = reader["name"] as string;
                if (string.Equals(nm, columnName, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>
        /// 同步读取子表中某条（或第一条）记录的单个字段值（按 ParentId 可选按 childId 过滤）
        /// </summary>
        public TField? GetChildFieldValue<TField>(string childTableName, int parentId, string fieldName, int? childId = null)
        {
            if (string.IsNullOrWhiteSpace(childTableName)) throw new ArgumentNullException(nameof(childTableName));
            if (string.IsNullOrWhiteSpace(fieldName)) throw new ArgumentNullException(nameof(fieldName));
            if (!System.Text.RegularExpressions.Regex.IsMatch(childTableName, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new ArgumentException("childTableName 包含非法字符");
            if (!System.Text.RegularExpressions.Regex.IsMatch(fieldName, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new ArgumentException("fieldName 包含非法字符");

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            if (!DoesTableHaveColumn(childTableName, fieldName))
                throw new ArgumentException($"字段 {fieldName} 不存在于子表 {childTableName} 中");

            var sql = childId.HasValue
                ? $"SELECT [{fieldName}] FROM [{childTableName}] WHERE [ParentId] = @parentId AND [Id] = @childId LIMIT 1"
                : $"SELECT [{fieldName}] FROM [{childTableName}] WHERE [ParentId] = @parentId LIMIT 1";

            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@parentId", parentId);
            if (childId.HasValue) cmd.Parameters.AddWithValue("@childId", childId.Value);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                var v = reader[0];
                return v == DBNull.Value ? default : (TField?)ConvertValue(v, typeof(TField));
            }

            return default;
        }

        /// <summary>
        /// 异步读取子表中某条（或第一条）记录的单个字段值（按 ParentId 可选按 childId 过滤）
        /// </summary>
        public async Task<TField?> GetChildFieldValueAsync<TField>(string childTableName, int parentId, string fieldName, int? childId = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(childTableName)) throw new ArgumentNullException(nameof(childTableName));
            if (string.IsNullOrWhiteSpace(fieldName)) throw new ArgumentNullException(nameof(fieldName));
            if (!System.Text.RegularExpressions.Regex.IsMatch(childTableName, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new ArgumentException("childTableName 包含非法字符");
            if (!System.Text.RegularExpressions.Regex.IsMatch(fieldName, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new ArgumentException("fieldName 包含非法字符");

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            if (!await DoesTableHaveColumnAsync(childTableName, fieldName, cancellationToken).ConfigureAwait(false))
                throw new ArgumentException($"字段 {fieldName} 不存在于子表 {childTableName} 中");

            var sql = childId.HasValue
                ? $"SELECT [{fieldName}] FROM [{childTableName}] WHERE [ParentId] = @parentId AND [Id] = @childId LIMIT 1"
                : $"SELECT [{fieldName}] FROM [{childTableName}] WHERE [ParentId] = @parentId LIMIT 1";

            await using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@parentId", parentId);
            if (childId.HasValue) cmd.Parameters.AddWithValue("@childId", childId.Value);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var v = reader[0];
                return v == DBNull.Value ? default : (TField?)ConvertValue(v, typeof(TField));
            }

            return default;
        }

        /// <summary>
        /// 同步读取子表中某父记录的指定字段的所有值（按 ParentId）
        /// </summary>
        public List<TField?> GetChildFieldValues<TField>(string childTableName, int parentId, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(childTableName)) throw new ArgumentNullException(nameof(childTableName));
            if (string.IsNullOrWhiteSpace(fieldName)) throw new ArgumentNullException(nameof(fieldName));
            if (!System.Text.RegularExpressions.Regex.IsMatch(childTableName, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new ArgumentException("childTableName 包含非法字符");
            if (!System.Text.RegularExpressions.Regex.IsMatch(fieldName, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new ArgumentException("fieldName 包含非法字符");

            var results = new List<TField?>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            if (!DoesTableHaveColumn(childTableName, fieldName))
                throw new ArgumentException($"字段 {fieldName} 不存在于子表 {childTableName} 中");

            var sql = $"SELECT [{fieldName}] FROM [{childTableName}] WHERE [ParentId] = @parentId";
            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@parentId", parentId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var v = reader[0];
                results.Add(v == DBNull.Value ? default : (TField?)ConvertValue(v, typeof(TField)));
            }

            return results;
        }

        /// <summary>
        /// 异步读取子表中某父记录的指定字段的所有值（按 ParentId）
        /// </summary>
        public async Task<List<TField?>> GetChildFieldValuesAsync<TField>(string childTableName, int parentId, string fieldName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(childTableName)) throw new ArgumentNullException(nameof(childTableName));
            if (string.IsNullOrWhiteSpace(fieldName)) throw new ArgumentNullException(nameof(fieldName));
            if (!System.Text.RegularExpressions.Regex.IsMatch(childTableName, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new ArgumentException("childTableName 包含非法字符");
            if (!System.Text.RegularExpressions.Regex.IsMatch(fieldName, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new ArgumentException("fieldName 包含非法字符");

            var results = new List<TField?>();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            if (!await DoesTableHaveColumnAsync(childTableName, fieldName, cancellationToken).ConfigureAwait(false))
                throw new ArgumentException($"字段 {fieldName} 不存在于子表 {childTableName} 中");

            var sql = $"SELECT [{fieldName}] FROM [{childTableName}] WHERE [ParentId] = @parentId";
            await using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@parentId", parentId);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var v = reader[0];
                results.Add(v == DBNull.Value ? default : (TField?)ConvertValue(v, typeof(TField)));
            }

            return results;
        }

        /// <summary>
        /// 同步按 ParentId 更新子表的单个字段（返回受影响行数）
        /// </summary>
        public int UpdateChildFieldByParentId(string childTableName, int parentId, string fieldName, object? value)
        {
            if (string.IsNullOrWhiteSpace(childTableName)) throw new ArgumentNullException(nameof(childTableName));
            if (string.IsNullOrWhiteSpace(fieldName)) throw new ArgumentNullException(nameof(fieldName));
            if (!System.Text.RegularExpressions.Regex.IsMatch(childTableName, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new ArgumentException("childTableName 包含非法字符");
            if (!System.Text.RegularExpressions.Regex.IsMatch(fieldName, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new ArgumentException("fieldName 包含非法字符");

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            if (!DoesTableHaveColumn(childTableName, fieldName))
                throw new ArgumentException($"字段 {fieldName} 不存在于子表 {childTableName} 中");

            var sql = $"UPDATE [{childTableName}] SET [{fieldName}] = @val WHERE [ParentId] = @parentId";
            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@val", value ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@parentId", parentId);
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// 异步按 ParentId 更新子表的单个字段（返回受影响行数）
        /// </summary>
        public async Task<int> UpdateChildFieldByParentIdAsync(string childTableName, int parentId, string fieldName, object? value, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(childTableName)) throw new ArgumentNullException(nameof(childTableName));
            if (string.IsNullOrWhiteSpace(fieldName)) throw new ArgumentNullException(nameof(fieldName));
            if (!System.Text.RegularExpressions.Regex.IsMatch(childTableName, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new ArgumentException("childTableName 包含非法字符");
            if (!System.Text.RegularExpressions.Regex.IsMatch(fieldName, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new ArgumentException("fieldName 包含非法字符");

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            if (!await DoesTableHaveColumnAsync(childTableName, fieldName, cancellationToken).ConfigureAwait(false))
                throw new ArgumentException($"字段 {fieldName} 不存在于子表 {childTableName} 中");

            var sql = $"UPDATE [{childTableName}] SET [{fieldName}] = @val WHERE [ParentId] = @parentId";
            await using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@val", value ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@parentId", parentId);
            return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 同步按 ChildId 更新子表的单个字段（返回是否更新成功）
        /// </summary>
        public bool UpdateChildFieldByChildId(string childTableName, int childId, string fieldName, object? value)
        {
            if (string.IsNullOrWhiteSpace(childTableName)) throw new ArgumentNullException(nameof(childTableName));
            if (string.IsNullOrWhiteSpace(fieldName)) throw new ArgumentNullException(nameof(fieldName));
            if (!System.Text.RegularExpressions.Regex.IsMatch(childTableName, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new ArgumentException("childTableName 包含非法字符");
            if (!System.Text.RegularExpressions.Regex.IsMatch(fieldName, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new ArgumentException("fieldName 包含非法字符");

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            if (!DoesTableHaveColumn(childTableName, fieldName))
                throw new ArgumentException($"字段 {fieldName} 不存在于子表 {childTableName} 中");

            var sql = $"UPDATE [{childTableName}] SET [{fieldName}] = @val WHERE [Id] = @childId";
            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@val", value ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@childId", childId);
            return cmd.ExecuteNonQuery() > 0;
        }

        /// <summary>
        /// 异步按 ChildId 更新子表的单个字段（返回是否更新成功）
        /// </summary>
        public async Task<bool> UpdateChildFieldByChildIdAsync(string childTableName, int childId, string fieldName, object? value, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(childTableName)) throw new ArgumentNullException(nameof(childTableName));
            if (string.IsNullOrWhiteSpace(fieldName)) throw new ArgumentNullException(nameof(fieldName));
            if (!System.Text.RegularExpressions.Regex.IsMatch(childTableName, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new ArgumentException("childTableName 包含非法字符");
            if (!System.Text.RegularExpressions.Regex.IsMatch(fieldName, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new ArgumentException("fieldName 包含非法字符");

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            if (!await DoesTableHaveColumnAsync(childTableName, fieldName, cancellationToken).ConfigureAwait(false))
                throw new ArgumentException($"字段 {fieldName} 不存在于子表 {childTableName} 中");

            var sql = $"UPDATE [{childTableName}] SET [{fieldName}] = @val WHERE [Id] = @childId";
            await using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@val", value ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@childId", childId);
            return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
        }

        /// <summary>
        /// 同步按任意子表字段条件更新子表的单个字段（返回受影响行数）
        /// </summary>
        public int UpdateChildFieldWhere(string childTableName, string conditionField, object? conditionValue, string fieldName, object? value)
        {
            if (string.IsNullOrWhiteSpace(childTableName)) throw new ArgumentNullException(nameof(childTableName));
            if (string.IsNullOrWhiteSpace(conditionField)) throw new ArgumentNullException(nameof(conditionField));
            if (string.IsNullOrWhiteSpace(fieldName)) throw new ArgumentNullException(nameof(fieldName));
            if (!System.Text.RegularExpressions.Regex.IsMatch(childTableName, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new ArgumentException("childTableName 包含非法字符");
            if (!System.Text.RegularExpressions.Regex.IsMatch(conditionField, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new ArgumentException("conditionField 包含非法字符");
            if (!System.Text.RegularExpressions.Regex.IsMatch(fieldName, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new ArgumentException("fieldName 包含非法字符");

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            if (!DoesTableHaveColumn(childTableName, fieldName))
                throw new ArgumentException($"字段 {fieldName} 不存在于子表 {childTableName} 中");
            if (!DoesTableHaveColumn(childTableName, conditionField))
                throw new ArgumentException($"字段 {conditionField} 不存在于子表 {childTableName} 中");

            var sql = $"UPDATE [{childTableName}] SET [{fieldName}] = @val WHERE [{conditionField}] = @cond";
            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@val", value ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@cond", conditionValue ?? DBNull.Value);
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// 异步按任意子表字段条件更新子表的单个字段（返回受影响行数）
        /// </summary>
        public async Task<int> UpdateChildFieldWhereAsync(string childTableName, string conditionField, object? conditionValue, string fieldName, object? value, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(childTableName)) throw new ArgumentNullException(nameof(childTableName));
            if (string.IsNullOrWhiteSpace(conditionField)) throw new ArgumentNullException(nameof(conditionField));
            if (string.IsNullOrWhiteSpace(fieldName)) throw new ArgumentNullException(nameof(fieldName));
            if (!System.Text.RegularExpressions.Regex.IsMatch(childTableName, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new ArgumentException("childTableName 包含非法字符");
            if (!System.Text.RegularExpressions.Regex.IsMatch(conditionField, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new ArgumentException("conditionField 包含非法字符");
            if (!System.Text.RegularExpressions.Regex.IsMatch(fieldName, "^[A-Za-z_][A-Za-z0-9_]*$")) throw new ArgumentException("fieldName 包含非法字符");

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            if (!await DoesTableHaveColumnAsync(childTableName, fieldName, cancellationToken).ConfigureAwait(false))
                throw new ArgumentException($"字段 {fieldName} 不存在于子表 {childTableName} 中");
            if (!await DoesTableHaveColumnAsync(childTableName, conditionField, cancellationToken).ConfigureAwait(false))
                throw new ArgumentException($"字段 {conditionField} 不存在于子表 {childTableName} 中");

            var sql = $"UPDATE [{childTableName}] SET [{fieldName}] = @val WHERE [{conditionField}] = @cond";
            await using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@val", value ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@cond", conditionValue ?? DBNull.Value);
            return await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        #endregion
        #endregion
    }
}
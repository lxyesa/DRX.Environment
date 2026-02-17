using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Drx.Sdk.Network.DataBase.Sqlite
{
    /// <summary>
    /// 高效的数据库子表集合类
    /// 
    /// 用内存换 CPU 效率：
    /// - 内部使用 Dictionary&lt;string, T&gt; 实现 O(1) 查询
    /// - 支持完整 LINQ 操作
    /// - 立即同步到数据库，保证一致性
    /// - 自动追踪添加/删除/修改的项目用于智能合并
    /// - 没有分页机制，8000 条数据全部加载到内存
    /// </summary>
    public class TableList<T> : IEnumerable<T>, IDisposable where T : class, IDataTableV2, new()
    {
        #region 内部状态

        /// <summary>
        /// 主内存存储：String ID -> 实体对象
        /// </summary>
        private readonly Dictionary<string, T> _items = new(StringComparer.Ordinal);

        /// <summary>
        /// 追踪被添加的项目（用于智能同步）
        /// </summary>
        private readonly HashSet<string> _addedItems = new(StringComparer.Ordinal);

        /// <summary>
        /// 追踪被修改的项目（用于智能同步）
        /// </summary>
        private readonly HashSet<string> _modifiedItems = new(StringComparer.Ordinal);

        /// <summary>
        /// 追踪被删除的项目（用于智能同步）
        /// </summary>
        private readonly HashSet<string> _deletedItems = new(StringComparer.Ordinal);

        /// <summary>
        /// 数据库连接字符串
        /// </summary>
        private string? _connectionString;

        /// <summary>
        /// 父表主键 ID
        /// </summary>
        private int _parentId;

        /// <summary>
        /// 子表在数据库中的表名
        /// </summary>
        private string? _tableName;

        /// <summary>
        /// 是否已初始化
        /// </summary>
        private bool _isInitialized;

        #endregion

        #region 属性

        /// <summary>
        /// 集合中的项目数
        /// </summary>
        public int Count => _items.Count;

        /// <summary>
        /// 是否为空
        /// </summary>
        public bool IsEmpty => _items.Count == 0;

        #endregion

        #region 初始化与连接

        /// <summary>
        /// 内部初始化方法，由 SqliteV2 调用
        /// </summary>
        internal void Initialize(string connectionString, int parentId, Type itemType)
        {
            if (_isInitialized) return;

            _connectionString = connectionString;
            _parentId = parentId;

            // 确定表名 - 使用约定 ParentTable_PropertyName
            // 由于我们不知道父表名或属性名，这里使用 itemType.Name 作为临时值
            // 调用者应该在知道完整表名后重新调用此方法，或直接设置
            _tableName = itemType.Name;
            if (_tableName == null) throw new InvalidOperationException($"无法确定 {typeof(T).Name} 的表名");

            _isInitialized = true;
        }

        /// <summary>
        /// 使用完整表名初始化 TableList（方便 LoadChildDataSync 或 InsertBatchAsync）
        /// </summary>
        internal void InitializeWithTableName(string connectionString, int parentId, string fullTableName)
        {
            if (_isInitialized) return;

            _connectionString = connectionString;
            _parentId = parentId;
            _tableName = fullTableName;  // 直接使用提供的完整表名

            _isInitialized = true;
        }

        #endregion

        #region 集合操作

        /// <summary>
        /// 添加单个项目
        /// 标记为已添加，待调用 SyncChanges() 时才写入数据库
        /// </summary>
        public void Add(T item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            // 生成唯一 ID（如果未设置）
            if (string.IsNullOrEmpty(item.Id))
                item.Id = Guid.NewGuid().ToString();

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            item.CreatedAt = now;
            item.UpdatedAt = now;
            item.ParentId = _parentId;

            // 添加到内存
            _items[item.Id] = item;
            _addedItems.Add(item.Id);
            // 不再立即同步，等待 SyncChanges() 调用
        }

        /// <summary>
        /// 批量添加多个项目
        /// 标记为已添加，待调用 SyncChanges() 时合并为单个 INSERT SQL
        /// </summary>
        public void AddRange(IEnumerable<T> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));

            var itemList = items.ToList();
            if (itemList.Count == 0) return;

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // 先添加到内存
            foreach (var item in itemList)
            {
                if (item == null) continue;

                if (string.IsNullOrEmpty(item.Id))
                    item.Id = Guid.NewGuid().ToString();

                item.CreatedAt = now;
                item.UpdatedAt = now;
                item.ParentId = _parentId;

                _items[item.Id] = item;
                _addedItems.Add(item.Id);
            }
            // 不再立即同步，等待 SyncChanges() 调用
        }

        /// <summary>
        /// 从数据库加载项目（内部方法）
        /// 直接添加到 _items，不标记为已添加，用于 SelectAll() 加载现有数据
        /// </summary>
        internal void LoadFromDatabase(T item)
        {
            if (item == null || string.IsNullOrEmpty(item.Id))
                return;

            // 直接添加到内存，不标记为已添加
            _items[item.Id] = item;
            // 注意：不会被标记在 _addedItems 中，因为这是从数据库加载的现有数据
        }

        /// <summary>
        /// 删除单个项目
        /// 标记为已删除，待调用 SyncChanges() 时才从数据库删除
        /// </summary>
        public bool Remove(T item)
        {
            if (item == null || string.IsNullOrEmpty(item.Id))
                return false;

            if (_items.Remove(item.Id))
            {
                // 标记为删除
                _addedItems.Remove(item.Id);
                _modifiedItems.Remove(item.Id);
                _deletedItems.Add(item.Id);
                // 不再立即从数据库删除，等待 SyncChanges() 调用

                return true;
            }

            return false;
        }

        /// <summary>
        /// 按 ID 删除项目
        /// </summary>
        public bool RemoveById(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;

            if (_items.TryGetValue(id, out var item))
            {
                return Remove(item);
            }

            return false;
        }

        /// <summary>
        /// 删除所有项目
        /// 标记所有项目为已删除，待调用 SyncChanges() 时才从数据库删除
        /// </summary>
        public void Clear()
        {
            // 标记所有项目为删除
            var idsToDelete = _items.Keys.ToList();
            _deletedItems.UnionWith(idsToDelete);
            _items.Clear();
            _addedItems.Clear();
            _modifiedItems.Clear();
            // 不再立即从数据库删除，等待 SyncChanges() 调用
        }

        /// <summary>
        /// 更新单个项目
        /// 标记为已修改，待调用 SyncChanges() 时才写入数据库
        /// </summary>
        public void Update(T item)
        {
            if (item == null || string.IsNullOrEmpty(item.Id))
                throw new ArgumentException("项目必须有有效的 ID");

            if (!_items.ContainsKey(item.Id))
                throw new KeyNotFoundException($"未找到 ID 为 '{item.Id}' 的项目");

            item.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            _items[item.Id] = item;

            // 标记为已修改（如果不是新添加的）
            if (!_addedItems.Contains(item.Id))
            {
                _modifiedItems.Add(item.Id);
            }
            // 不再立即同步，等待 SyncChanges() 调用
        }

        /// <summary>
        /// 按 ID 查询单个项目
        /// </summary>
        public T? GetById(string id)
        {
            return string.IsNullOrEmpty(id) ? null : (_items.TryGetValue(id, out var item) ? item : null);
        }

        #endregion

        #region LINQ 支持

        /// <summary>
        /// 返回满足条件的项目
        /// </summary>
        public IEnumerable<T> Where(Func<T, bool> predicate)
        {
            return _items.Values.Where(predicate);
        }

        /// <summary>
        /// 返回第一个满足条件的项目，如果不存在则返回 null
        /// </summary>
        public T? FirstOrDefault(Func<T, bool>? predicate = null)
        {
            return predicate == null 
                ? _items.Values.FirstOrDefault() 
                : _items.Values.FirstOrDefault(predicate);
        }

        /// <summary>
        /// 检查是否存在满足条件的项目
        /// </summary>
        public bool Any(Func<T, bool>? predicate = null)
        {
            return predicate == null 
                ? _items.Count > 0 
                : _items.Values.Any(predicate);
        }

        /// <summary>
        /// 按指定键对项目进行分组
        /// </summary>
        public IEnumerable<IGrouping<TKey, T>> GroupBy<TKey>(Func<T, TKey> keySelector) where TKey : notnull
        {
            return _items.Values.GroupBy(keySelector);
        }

        /// <summary>
        /// 按指定选择器映射项目
        /// </summary>
        public IEnumerable<TResult> Select<TResult>(Func<T, TResult> selector)
        {
            return _items.Values.Select(selector);
        }

        /// <summary>
        /// 排序项目
        /// </summary>
        public IEnumerable<T> OrderBy<TKey>(Func<T, TKey> keySelector)
        {
            return _items.Values.OrderBy(keySelector);
        }

        /// <summary>
        /// 倒序排列
        /// </summary>
        public IEnumerable<T> OrderByDescending<TKey>(Func<T, TKey> keySelector)
        {
            return _items.Values.OrderByDescending(keySelector);
        }

        #endregion

        #region 智能同步方法

        /// <summary>
        /// 同步所有变更到数据库
        /// 由 SqliteV2 的 Update 方法调用，在主表更新的同一个事务中完成
        /// 采用智能合并策略：仅同步 added/modified/deleted 的项目
        /// </summary>
        public void SyncChanges(SqliteConnection connection, SqliteTransaction transaction)
        {
            if (!_isInitialized || string.IsNullOrEmpty(_connectionString))
                return;

            try
            {
                // 1. 批量插入新添加的项目
                if (_addedItems.Count > 0)
                {
                    var itemsToAdd = _addedItems.Select(id => _items[id]).ToList();
                    var sql = BuildInsertSql(itemsToAdd);
                    using var cmd = new SqliteCommand(sql, connection);
                    cmd.Transaction = transaction;

                    int paramIndex = 0;
                    foreach (var item in itemsToAdd)
                    {
                        BindInsertParametersBatch(cmd, item, paramIndex);
                        paramIndex++;
                    }

                    cmd.ExecuteNonQuery();
                    _addedItems.Clear();
                }

                // 2. 批量更新已修改的项目
                if (_modifiedItems.Count > 0)
                {
                    var sql = BuildUpdateSql();
                    using var cmd = new SqliteCommand(sql, connection);
                    cmd.Transaction = transaction;

                    var itemsToUpdate = _modifiedItems.Select(id => _items[id]).ToList();
                    foreach (var item in itemsToUpdate)
                    {
                        BindUpdateParameters(cmd, item);
                        cmd.ExecuteNonQuery();
                    }

                    _modifiedItems.Clear();
                }

                // 3. 批量删除已删除的项目
                if (_deletedItems.Count > 0)
                {
                    var idsToDelete = _deletedItems.ToList();
                    var placeholders = string.Join(",", Enumerable.Range(0, idsToDelete.Count).Select((_, i) => $"@id{i}"));
                    var sql = $"DELETE FROM [{_tableName}] WHERE [Id] IN ({placeholders})";

                    using var cmd = new SqliteCommand(sql, connection);
                    cmd.Transaction = transaction;

                    for (int i = 0; i < idsToDelete.Count; i++)
                    {
                        cmd.Parameters.AddWithValue($"@id{i}", idsToDelete[i]);
                    }

                    int deleted = cmd.ExecuteNonQuery();
                    _deletedItems.Clear();
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"同步 {typeof(T).Name} 子表变更失败：{ex.Message}", ex);
            }
        }

        #endregion

        #region 数据库同步

        /// <summary>
        /// 插入单条记录到数据库
        /// </summary>
        private void InsertToDatabase(T item)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var sql = BuildInsertSql(new[] { item });
                using var cmd = new SqliteCommand(sql, connection);

                BindInsertParameters(cmd, item);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"插入子表记录失败：{ex.Message}", ex);
            }
        }

        /// <summary>
        /// 批量插入到数据库
        /// 优化：使用单个 INSERT INTO ... VALUES ... 语句
        /// </summary>
        private void InsertBatchToDatabase(List<T> items)
        {
            if (items.Count == 0) return;

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var sql = BuildInsertSql(items);
                using var cmd = new SqliteCommand(sql, connection);

                int paramIndex = 0;
                foreach (var item in items)
                {
                    BindInsertParametersBatch(cmd, item, paramIndex);
                    paramIndex++;
                }

                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"批量插入子表记录失败：{ex.Message}", ex);
            }
        }

        /// <summary>
        /// 更新单条记录
        /// </summary>
        private void UpdateInDatabase(T item)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var sql = BuildUpdateSql();
                using var cmd = new SqliteCommand(sql, connection);

                BindUpdateParameters(cmd, item);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"更新子表记录失败：{ex.Message}", ex);
            }
        }

        /// <summary>
        /// 删除单条记录
        /// </summary>
        private void DeleteFromDatabase(string id)
        {
            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var sql = $"DELETE FROM [{_tableName}] WHERE [Id] = @id";
                using var cmd = new SqliteCommand(sql, connection);

                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"删除子表记录失败：{ex.Message}", ex);
            }
        }

        /// <summary>
        /// 批量删除记录
        /// </summary>
        private void DeleteBatchFromDatabase(List<string> ids)
        {
            if (ids.Count == 0) return;

            try
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                var placeholders = string.Join(",", Enumerable.Range(0, ids.Count).Select((_, i) => $"@id{i}"));
                var sql = $"DELETE FROM [{_tableName}] WHERE [Id] IN ({placeholders})";

                using var cmd = new SqliteCommand(sql, connection);

                for (int i = 0; i < ids.Count; i++)
                {
                    cmd.Parameters.AddWithValue($"@id{i}", ids[i]);
                }

                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"批量删除子表记录失败：{ex.Message}", ex);
            }
        }

        #endregion

        #region SQL 构建

        private string BuildInsertSql(IEnumerable<T> items)
        {
            var sb = new StringBuilder();
            sb.Append($"INSERT INTO [{_tableName}] ([Id], [ParentId], [CreatedAt], [UpdatedAt]) VALUES ");

            var itemList = items.ToList();
            for (int i = 0; i < itemList.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append($"(@id{i}, @parentId{i}, @createdAt{i}, @updatedAt{i})");
            }

            return sb.ToString();
        }

        private string BuildUpdateSql()
        {
            return $"UPDATE [{_tableName}] SET [UpdatedAt] = @updatedAt WHERE [Id] = @id";
        }

        private void BindInsertParameters(SqliteCommand cmd, T item)
        {
            cmd.Parameters.AddWithValue("@id", item.Id ?? Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("@parentId", _parentId);
            cmd.Parameters.AddWithValue("@createdAt", item.CreatedAt);
            cmd.Parameters.AddWithValue("@updatedAt", item.UpdatedAt);
        }

        private void BindInsertParametersBatch(SqliteCommand cmd, T item, int index)
        {
            cmd.Parameters.AddWithValue($"@id{index}", item.Id ?? Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue($"@parentId{index}", _parentId);
            cmd.Parameters.AddWithValue($"@createdAt{index}", item.CreatedAt);
            cmd.Parameters.AddWithValue($"@updatedAt{index}", item.UpdatedAt);
        }

        private void BindUpdateParameters(SqliteCommand cmd, T item)
        {
            cmd.Parameters.AddWithValue("@id", item.Id);
            cmd.Parameters.AddWithValue("@updatedAt", item.UpdatedAt);
        }

        #endregion

        #region 枚举支持

        public IEnumerator<T> GetEnumerator()
        {
            return _items.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region 资源清理

        public void Dispose()
        {
            _items.Clear();
            _addedItems.Clear();
            _modifiedItems.Clear();
            _deletedItems.Clear();
        }

        #endregion
    }
}

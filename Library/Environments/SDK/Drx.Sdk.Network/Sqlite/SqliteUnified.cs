using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Collections;

namespace Drx.Sdk.Network.Sqlite
{
    /// <summary>
    /// 用于标记不应被保存到SQLite数据库的属性
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class SqliteIgnoreAttribute : Attribute
    {
    }

    /// <summary>
    /// 用于标记关联表的特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class SqliteRelationAttribute : Attribute
    {
        public string TableName { get; }
        public string ForeignKeyProperty { get; }

        public SqliteRelationAttribute(string tableName, string foreignKeyProperty)
        {
            TableName = tableName;
            ForeignKeyProperty = foreignKeyProperty;
        }
    }

    /// <summary>
    /// 统一的SQLite数据库操作封装类，集成了基础操作和关联表处理功能
    /// </summary>
    /// <typeparam name="T">要操作的数据类型</typeparam>
    public class SqliteUnified<T> where T : class, IDataBase, new()
    {
        private readonly string _connectionString;
        private readonly string _tableName;
        private readonly PropertyInfo[] _properties;
        private readonly PropertyInfo _primaryKeyProperty;
        private readonly Dictionary<PropertyInfo, SqliteRelationAttribute> _relationProperties;

        /// <summary>
        /// 初始化SQLite封装
        /// </summary>
        /// <param name="path">数据库文件路径</param>
        public SqliteUnified(string path)
        {
            _connectionString = $"Data Source={path}";
            _tableName = typeof(T).Name;
            _properties = typeof(T).GetProperties();
            
            // 主键将始终是IDataBase接口中的Id
            _primaryKeyProperty = typeof(T).GetProperty(nameof(IDataBase.Id))!;
            
            // 获取所有关联表属性
            _relationProperties = new Dictionary<PropertyInfo, SqliteRelationAttribute>();
            foreach (var prop in _properties)
            {
                var relationAttr = prop.GetCustomAttribute<SqliteRelationAttribute>();
                if (relationAttr != null)
                {
                    _relationProperties.Add(prop, relationAttr);
                }
            }

            Initialize();
        }

        /// <summary>
        /// 初始化数据库和表结构
        /// </summary>
        private void Initialize()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();

                // 创建表
                var columns = new List<string>();
                foreach (var prop in _properties)
                {
                    // 跳过标记为忽略的属性
                    if (prop.GetCustomAttribute<SqliteIgnoreAttribute>() != null)
                        continue;
                    
                    // 跳过关联表属性
                    if (_relationProperties.ContainsKey(prop))
                        continue;
                        
                    string sqlType = GetSqliteType(prop.PropertyType);
                    string primaryKey = (prop == _primaryKeyProperty) ? "PRIMARY KEY" : "";
                    columns.Add($"{prop.Name} {sqlType} {primaryKey}".Trim());
                }

                command.CommandText = $"CREATE TABLE IF NOT EXISTS {_tableName} ({string.Join(", ", columns)})";
                command.ExecuteNonQuery();

                // 检查并添加新列
                var existingColumns = GetTableColumns(connection);
                foreach (var prop in _properties)
                {
                    // 跳过标记为忽略的属性
                    if (prop.GetCustomAttribute<SqliteIgnoreAttribute>() != null)
                        continue;
                    
                    // 跳过关联表属性
                    if (_relationProperties.ContainsKey(prop))
                        continue;
                        
                    if (!existingColumns.Contains(prop.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        var addColumnCommand = connection.CreateCommand();
                        string sqlType = GetSqliteType(prop.PropertyType);
                        addColumnCommand.CommandText = $"ALTER TABLE {_tableName} ADD COLUMN {prop.Name} {sqlType};";
                        addColumnCommand.ExecuteNonQuery();
                    }
                }
            }
        }

        private List<string> GetTableColumns(SqliteConnection connection)
        {
            var columns = new List<string>();
            var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({_tableName})";
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    columns.Add(reader.GetString(1)); // "name" column
                }
            }
            return columns;
        }

        #region 基础数据库操作

        /// <summary>
        /// 保存对象到数据库
        /// </summary>
        /// <param name="item">要保存的对象</param>
        /// <remarks>
        /// 使用 INSERT OR REPLACE 语法，当插入的数据主键已存在时，会替换原有记录。
        /// 这确保了相同ID的数据会被更新而不是产生重复记录。
        /// </remarks>
        public void Save(T item)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        var command = connection.CreateCommand();
                        command.Transaction = transaction;

                        // 过滤掉标记为忽略的属性和关联表属性
                        var validProperties = _properties.Where(p => 
                            p.GetCustomAttribute<SqliteIgnoreAttribute>() == null && 
                            !_relationProperties.ContainsKey(p)).ToArray();
                        
                        var columns = validProperties.Select(p => p.Name);
                        var parameters = validProperties.Select(p => $"${p.Name}");

                        command.CommandText = $@"
                            INSERT OR REPLACE INTO {_tableName} 
                            ({string.Join(", ", columns)})
                            VALUES ({string.Join(", ", parameters)})";

                        foreach (var prop in validProperties)
                        {
                            var value = prop.GetValue(item);
                            if (value != null && GetSqliteType(prop.PropertyType) == "TEXT" && (prop.PropertyType.IsGenericType || prop.PropertyType.IsArray))
                            {
                                command.Parameters.AddWithValue($"${prop.Name}", JsonSerializer.Serialize(value));
                            }
                            else
                            {
                                command.Parameters.AddWithValue($"${prop.Name}", value ?? DBNull.Value);
                            }
                        }

                        command.ExecuteNonQuery();
                        
                        // 处理关联表
                        if (_relationProperties.Count > 0 && item != null)
                        {
                            // 确保主键已设置
                            var id = _primaryKeyProperty.GetValue(item);
                            if (id != null)
                            {
                                int itemId = Convert.ToInt32(id);
                                
                                // 保存每个关联表
                                foreach (var relationProp in _relationProperties)
                                {
                                    var prop = relationProp.Key;
                                    var attr = relationProp.Value;
                                    var value = prop.GetValue(item);
                                    
                                    // 如果属性是集合类型
                                    if (value != null && typeof(IEnumerable).IsAssignableFrom(prop.PropertyType) && 
                                        prop.PropertyType != typeof(string))
                                    {
                                        // 获取集合元素类型
                                        Type elementType;
                                        if (prop.PropertyType.IsGenericType)
                                        {
                                            elementType = prop.PropertyType.GetGenericArguments()[0];
                                        }
                                        else if (prop.PropertyType.IsArray)
                                        {
                                            elementType = prop.PropertyType.GetElementType();
                                        }
                                        else
                                        {
                                            continue;
                                        }
                                        
                                        // 确保元素类型实现了IDataBase接口
                                        if (!typeof(IDataBase).IsAssignableFrom(elementType))
                                        {
                                            continue;
                                        }
                                        
                                        // 将值转换为IEnumerable
                                        var items = ((IEnumerable)value).Cast<IDataBase>();
                                        
                                        // 保存关联表数据
                                        SaveRelationship(itemId, items, attr.TableName, attr.ForeignKeyProperty, elementType);
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
            }
        }

        /// <summary>
        /// 批量保存对象到数据库
        /// </summary>
        /// <param name="items">要保存的对象集合</param>
        public void SaveAll(IEnumerable<T> items)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        foreach (var item in items)
                        {
                            var command = connection.CreateCommand();
                            command.Transaction = transaction;

                            // 过滤掉标记为忽略的属性和关联表属性
                            var validProperties = _properties.Where(p => 
                                p.GetCustomAttribute<SqliteIgnoreAttribute>() == null && 
                                !_relationProperties.ContainsKey(p)).ToArray();
                            
                            var columns = validProperties.Select(p => p.Name);
                            var parameters = validProperties.Select(p => $"${p.Name}");

                            command.CommandText = $@"
                                INSERT OR REPLACE INTO {_tableName} 
                                ({string.Join(", ", columns)})
                                VALUES ({string.Join(", ", parameters)})";

                            foreach (var prop in validProperties)
                            {
                                var value = prop.GetValue(item);
                                if (value != null && GetSqliteType(prop.PropertyType) == "TEXT" && (prop.PropertyType.IsGenericType || prop.PropertyType.IsArray))
                                {
                                    command.Parameters.AddWithValue($"${prop.Name}", JsonSerializer.Serialize(value));
                                }
                                else
                                {
                                    command.Parameters.AddWithValue($"${prop.Name}", value ?? DBNull.Value);
                                }
                            }

                            command.ExecuteNonQuery();
                        }

                        transaction.Commit();
                        
                        // 处理关联表 - 对每个项目单独处理
                        if (_relationProperties.Count > 0)
                        {
                            foreach (var item in items)
                            {
                                var id = _primaryKeyProperty.GetValue(item);
                                if (id != null)
                                {
                                    int itemId = Convert.ToInt32(id);
                                    
                                    foreach (var relationProp in _relationProperties)
                                    {
                                        var prop = relationProp.Key;
                                        var attr = relationProp.Value;
                                        var value = prop.GetValue(item);
                                        
                                        if (value != null && typeof(IEnumerable).IsAssignableFrom(prop.PropertyType) && 
                                            prop.PropertyType != typeof(string))
                                        {
                                            Type elementType;
                                            if (prop.PropertyType.IsGenericType)
                                            {
                                                elementType = prop.PropertyType.GetGenericArguments()[0];
                                            }
                                            else if (prop.PropertyType.IsArray)
                                            {
                                                elementType = prop.PropertyType.GetElementType();
                                            }
                                            else
                                            {
                                                continue;
                                            }
                                            
                                            if (!typeof(IDataBase).IsAssignableFrom(elementType))
                                            {
                                                continue;
                                            }
                                            
                                            var items1 = ((IEnumerable)value).Cast<IDataBase>();
                                            SaveRelationship(itemId, items1, attr.TableName, attr.ForeignKeyProperty, elementType);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// 根据条件查询对象
        /// </summary>
        /// <param name="whereConditions">查询条件字典，键为属性名，值为匹配值</param>
        /// <returns>符合条件的对象集合</returns>
        public List<T> Read(Dictionary<string, object>? whereConditions = null)
        {
            var results = new List<T>();
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();

                string whereClause = "";
                if (whereConditions != null && whereConditions.Count > 0)
                {
                    var conditions = whereConditions.Select(kvp => $"{kvp.Key} = ${kvp.Key}");
                    whereClause = $"WHERE {string.Join(" AND ", conditions)}";
                }

                command.CommandText = $"SELECT * FROM {_tableName} {whereClause}";

                if (whereConditions != null)
                {
                    foreach (var condition in whereConditions)
                    {
                        command.Parameters.AddWithValue($"${condition.Key}", condition.Value ?? DBNull.Value);
                    }
                }

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        T item = new T();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            string columnName = reader.GetName(i);
                            var property = _properties.FirstOrDefault(p => 
                                p.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase));
                            
                            if (property != null && !reader.IsDBNull(i))
                            {
                                var dbValue = reader.GetValue(i);
                                
                                if (dbValue is string stringValue && (property.PropertyType.IsGenericType || property.PropertyType.IsArray))
                                {
                                    try
                                    {
                                        var deserializedValue = JsonSerializer.Deserialize(stringValue, property.PropertyType);
                                        property.SetValue(item, deserializedValue);
                                    }
                                    catch (JsonException)
                                    {
                                        // 如果反序列化失败，则按原样设置值（可能是普通字符串）
                                        property.SetValue(item, Convert.ChangeType(dbValue, property.PropertyType));
                                    }
                                }
                                else
                                {
                                    var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                                    object value;
                                    if (targetType.IsEnum)
                                    {
                                        if (dbValue is string s)
                                            value = Enum.Parse(targetType, s, true);
                                        else
                                            value = Enum.ToObject(targetType, dbValue);
                                    }
                                    else
                                    {
                                        value = Convert.ChangeType(dbValue, targetType);
                                    }
                                    property.SetValue(item, value);
                                }
                            }
                        }
                        
                        // 加载关联表数据
                        if (_relationProperties.Count > 0)
                        {
                            var id = _primaryKeyProperty.GetValue(item);
                            if (id != null)
                            {
                                int itemId = Convert.ToInt32(id);
                                
                                foreach (var relationProp in _relationProperties)
                                {
                                    var prop = relationProp.Key;
                                    var attr = relationProp.Value;
                                    
                                    // 获取集合元素类型
                                    Type elementType;
                                    if (prop.PropertyType.IsGenericType)
                                    {
                                        elementType = prop.PropertyType.GetGenericArguments()[0];
                                    }
                                    else if (prop.PropertyType.IsArray)
                                    {
                                        elementType = prop.PropertyType.GetElementType();
                                    }
                                    else
                                    {
                                        continue;
                                    }
                                    
                                    // 确保元素类型实现了IDataBase接口
                                    if (!typeof(IDataBase).IsAssignableFrom(elementType))
                                    {
                                        continue;
                                    }
                                    
                                    // 加载关联数据
                                    var relationItems = LoadRelationship(itemId, attr.TableName, attr.ForeignKeyProperty, elementType);
                                    
                                    // 设置关联集合
                                    if (prop.PropertyType.IsArray)
                                    {
                                        // 转换为数组
                                        var array = Array.CreateInstance(elementType, relationItems.Count);
                                        for (int i = 0; i < relationItems.Count; i++)
                                        {
                                            array.SetValue(relationItems[i], i);
                                        }
                                        prop.SetValue(item, array);
                                    }
                                    else
                                    {
                                        // 创建合适类型的集合
                                        var listType = typeof(List<>).MakeGenericType(elementType);
                                        var list = Activator.CreateInstance(listType);
                                        
                                        // 添加所有项目
                                        var addRangeMethod = listType.GetMethod("AddRange");
                                        addRangeMethod?.Invoke(list, new object[] { relationItems });
                                        
                                        prop.SetValue(item, list);
                                    }
                                }
                            }
                        }
                        
                        results.Add(item);
                    }
                }
            }
            return results;
        }

        public T ReadSingle(string where, object value)
        {
            var conditions = new Dictionary<string, object>
            {
                { where, value }
            };

            return Read(conditions).FirstOrDefault();
        }

        /// <summary>
        /// 根据主键ID查询单个对象
        /// </summary>
        /// <param name="id">主键ID值</param>
        /// <returns>对象或null</returns>
        public T FindById(int id)
        {
            if (_primaryKeyProperty == null)
                throw new InvalidOperationException("无法找到主键属性");

            var conditions = new Dictionary<string, object>
            {
                { _primaryKeyProperty.Name, id }
            };

            return Read(conditions).FirstOrDefault();
        }

        /// <summary>
        /// 删除对象
        /// </summary>
        /// <param name="item">要删除的对象</param>
        public void Delete(T item)
        {
            if (_primaryKeyProperty == null)
                throw new InvalidOperationException("无法找到主键属性");

            var id = _primaryKeyProperty.GetValue(item);
            if (id == null)
                throw new InvalidOperationException("主键值不能为null");

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = $"DELETE FROM {_tableName} WHERE {_primaryKeyProperty.Name} = $id";
                command.Parameters.AddWithValue("$id", id);
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 根据条件删除对象
        /// </summary>
        /// <param name="whereConditions">条件字典</param>
        /// <returns>删除的行数</returns>
        public int DeleteWhere(Dictionary<string, object> whereConditions)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                var command = connection.CreateCommand();

                string whereClause = "";
                if (whereConditions != null && whereConditions.Count > 0)
                {
                    var conditions = whereConditions.Select(kvp => $"{kvp.Key} = ${kvp.Key}");
                    whereClause = $"WHERE {string.Join(" AND ", conditions)}";
                }
                else
                {
                    throw new ArgumentException("删除条件不能为空");
                }

                command.CommandText = $"DELETE FROM {_tableName} {whereClause}";

                foreach (var condition in whereConditions)
                {
                    command.Parameters.AddWithValue($"${condition.Key}", condition.Value ?? DBNull.Value);
                }

                return command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 根据指定条件修复（替换）数据库中的特定条目
        /// </summary>
        /// <param name="item">包含新数据的对象</param>
        /// <param name="identifierConditions">用于识别要替换的特定条目的条件</param>
        /// <returns>true表示找到并更新了条目，false表示未找到匹配条目（此时会创建新条目）</returns>
        public bool Repair(T item, Dictionary<string, object> identifierConditions)
        {
            if (identifierConditions == null || identifierConditions.Count == 0)
            {
                throw new ArgumentException("必须提供至少一个条件来识别要替换的条目");
            }

            // 首先查找匹配的条目
            var existingItems = Read(identifierConditions);
            var existingItem = existingItems.FirstOrDefault();

            // 如果找到匹配项，设置ID以确保更新而不是插入
            if (existingItem != null)
            {
                _primaryKeyProperty.SetValue(item, _primaryKeyProperty.GetValue(existingItem));
                Save(item);
                return true;
            }
            else
            {
                // 未找到匹配项，创建新条目
                Save(item);
                return false;
            }
        }

        /// <summary>
        /// 根据单个条件修复（替换）数据库中的特定条目
        /// </summary>
        /// <param name="item">包含新数据的对象</param>
        /// <param name="propertyName">用于识别的属性名</param>
        /// <param name="propertyValue">用于识别的属性值</param>
        /// <returns>true表示找到并更新了条目，false表示未找到匹配条目（此时会创建新条目）</returns>
        public bool Repair(T item, string propertyName, object propertyValue)
        {
            var conditions = new Dictionary<string, object>
            {
                { propertyName, propertyValue }
            };
            return Repair(item, conditions);
        }

        #endregion

        #region 关联表操作

        /// <summary>
        /// 保存关联表数据
        /// </summary>
        /// <param name="parentId">父表ID</param>
        /// <param name="items">要保存的子表项目集合</param>
        /// <param name="tableName">关联表名称</param>
        /// <param name="parentKeyName">父表外键名称</param>
        /// <param name="childType">子表实体类型</param>
        private void SaveRelationship(int parentId, IEnumerable<IDataBase> items, string tableName, string parentKeyName, Type childType)
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 删除现有关联
                        var deleteCommand = connection.CreateCommand();
                        deleteCommand.Transaction = transaction;
                        deleteCommand.CommandText = $"DELETE FROM {tableName} WHERE {parentKeyName} = $parentId";
                        deleteCommand.Parameters.AddWithValue("$parentId", parentId);
                        deleteCommand.ExecuteNonQuery();

                        // 插入新的关联
                        foreach (var item in items)
                        {
                            // 确保子项有正确的父ID
                            var parentKeyProp = childType.GetProperty(parentKeyName);
                            if (parentKeyProp != null)
                            {
                                parentKeyProp.SetValue(item, parentId);
                            }

                            // 保存子项
                            var sqliteType = typeof(SqliteUnified<>).MakeGenericType(childType);
                            var sqliteInstance = Activator.CreateInstance(sqliteType, _connectionString.Replace("Data Source=", ""));
                            var saveMethod = sqliteType.GetMethod("Save");
                            saveMethod?.Invoke(sqliteInstance, new object[] { item });
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
        }

        /// <summary>
        /// 修复关联表数据 - 仅替换特定条目而不删除其他条目
        /// </summary>
        /// <param name="parentId">父表ID</param>
        /// <param name="items">要修复的子表项目集合</param>
        /// <param name="tableName">关联表名称</param>
        /// <param name="parentKeyName">父表外键名称</param>
        /// <param name="identifierProperty">用于识别特定条目的属性名（除了Id和父键外的唯一标识符）</param>
        /// <param name="childType">子表实体类型</param>
        public void RepairRelationship(int parentId, IEnumerable<IDataBase> items, string tableName, string parentKeyName, string identifierProperty, Type childType)
        {
            // 获取现有条目
            var sqliteType = typeof(SqliteUnified<>).MakeGenericType(childType);
            var sqliteInstance = Activator.CreateInstance(sqliteType, _connectionString.Replace("Data Source=", ""));
            var readMethod = sqliteType.GetMethod("Read");
            var conditions = new Dictionary<string, object>
            {
                { parentKeyName, parentId }
            };
            var existingItems = (System.Collections.IList?)readMethod?.Invoke(sqliteInstance, new object[] { conditions });
            
            if (existingItems == null) return;
            
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        foreach (var item in items)
                        {
                            // 确保子项有正确的父ID
                            var parentKeyProp = childType.GetProperty(parentKeyName);
                            if (parentKeyProp != null)
                            {
                                parentKeyProp.SetValue(item, parentId);
                            }

                            // 获取标识符属性
                            var idProp = childType.GetProperty(identifierProperty);
                            if (idProp == null)
                            {
                                throw new ArgumentException($"属性 {identifierProperty} 在类型 {childType.Name} 中不存在");
                            }

                            // 获取当前项的标识符值
                            var idValue = idProp.GetValue(item);
                            if (idValue == null)
                            {
                                throw new ArgumentException($"标识符属性 {identifierProperty} 的值不能为null");
                            }

                            // 查找匹配的现有项
                            IDataBase existingItem = null;
                            foreach (IDataBase e in existingItems)
                            {
                                if (object.Equals(idProp.GetValue(e), idValue))
                                {
                                    existingItem = e;
                                    break;
                                }
                            }

                            // 如果找到匹配项，设置ID以确保更新而不是插入
                            if (existingItem != null)
                            {
                                var idProperty = typeof(IDataBase).GetProperty(nameof(IDataBase.Id));
                                idProperty?.SetValue(item, idProperty.GetValue(existingItem));
                            }

                            // 保存/更新项
                            var saveMethod = sqliteType.GetMethod("Save");
                            saveMethod?.Invoke(sqliteInstance, new object[] { item });
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
        }

        /// <summary>
        /// 修复单个关联项 - 根据指定属性查找并替换
        /// </summary>
        /// <param name="parentId">父表ID</param>
        /// <param name="item">要修复的项目</param>
        /// <param name="parentKeyName">父表外键名称</param>
        /// <param name="identifierProperty">用于识别特定条目的属性名</param>
        /// <param name="childType">子表实体类型</param>
        public void RepairRelationshipItem(int parentId, IDataBase item, string parentKeyName, string identifierProperty, Type childType)
        {
            // 确保子项有正确的父ID
            var parentKeyProp = childType.GetProperty(parentKeyName);
            if (parentKeyProp != null)
            {
                parentKeyProp.SetValue(item, parentId);
            }

            // 获取标识符属性
            var idProp = childType.GetProperty(identifierProperty);
            if (idProp == null)
            {
                throw new ArgumentException($"属性 {identifierProperty} 在类型 {childType.Name} 中不存在");
            }

            // 获取当前项的标识符值
            var idValue = idProp.GetValue(item);
            if (idValue == null)
            {
                throw new ArgumentException($"标识符属性 {identifierProperty} 的值不能为null");
            }

            // 查找匹配的现有项
            var sqliteType = typeof(SqliteUnified<>).MakeGenericType(childType);
            var sqliteInstance = Activator.CreateInstance(sqliteType, _connectionString.Replace("Data Source=", ""));
            var readMethod = sqliteType.GetMethod("Read");
            var conditions = new Dictionary<string, object>
            {
                { parentKeyName, parentId },
                { identifierProperty, idValue }
            };
            var existingItems = (System.Collections.IList?)readMethod?.Invoke(sqliteInstance, new object[] { conditions });
            var existingItem = existingItems != null && existingItems.Count > 0 ? (IDataBase)existingItems[0] : null;

            // 如果找到匹配项，设置ID以确保更新而不是插入
            if (existingItem != null)
            {
                var idProperty = typeof(IDataBase).GetProperty(nameof(IDataBase.Id));
                idProperty?.SetValue(item, idProperty.GetValue(existingItem));
            }

            // 保存/更新项
            var saveMethod = sqliteType.GetMethod("Save");
            saveMethod?.Invoke(sqliteInstance, new object[] { item });
        }

        /// <summary>
        /// 加载关联表数据
        /// </summary>
        /// <param name="parentId">父表ID</param>
        /// <param name="tableName">关联表名称</param>
        /// <param name="parentKeyName">父表外键名称</param>
        /// <param name="childType">子表实体类型</param>
        /// <returns>子表项目集合</returns>
        private List<IDataBase> LoadRelationship(int parentId, string tableName, string parentKeyName, Type childType)
        {
            var sqliteType = typeof(SqliteUnified<>).MakeGenericType(childType);
            var sqliteInstance = Activator.CreateInstance(sqliteType, _connectionString.Replace("Data Source=", ""));
            var readMethod = sqliteType.GetMethod("Read");
            var conditions = new Dictionary<string, object>
            {
                { parentKeyName, parentId }
            };
            var result = (System.Collections.IList?)readMethod?.Invoke(sqliteInstance, new object[] { conditions });
            return result?.Cast<IDataBase>().ToList() ?? new List<IDataBase>();
        }

        /// <summary>
        /// 查询特定条件的关联项
        /// </summary>
        /// <param name="parentId">父表ID</param>
        /// <param name="conditions">额外的查询条件</param>
        /// <param name="parentKeyName">父表外键名称</param>
        /// <param name="childType">子表实体类型</param>
        /// <returns>符合条件的子表项目集合</returns>
        public List<IDataBase> QueryRelationship(int parentId, Dictionary<string, object> conditions, string parentKeyName, Type childType)
        {
            var sqliteType = typeof(SqliteUnified<>).MakeGenericType(childType);
            var sqliteInstance = Activator.CreateInstance(sqliteType, _connectionString.Replace("Data Source=", ""));
            var readMethod = sqliteType.GetMethod("Read");
            
            var allConditions = new Dictionary<string, object>(conditions)
            {
                { parentKeyName, parentId }
            };
            
            var result = (System.Collections.IList?)readMethod?.Invoke(sqliteInstance, new object[] { allConditions });
            return result?.Cast<IDataBase>().ToList() ?? new List<IDataBase>();
        }

        /// <summary>
        /// 更新单个关联项
        /// </summary>
        /// <param name="item">要更新的项目</param>
        /// <param name="childType">子表实体类型</param>
        public void UpdateRelationshipItem(IDataBase item, Type childType)
        {
            var sqliteType = typeof(SqliteUnified<>).MakeGenericType(childType);
            var sqliteInstance = Activator.CreateInstance(sqliteType, _connectionString.Replace("Data Source=", ""));
            var saveMethod = sqliteType.GetMethod("Save");
            saveMethod?.Invoke(sqliteInstance, new object[] { item });
        }

        /// <summary>
        /// 删除单个关联项
        /// </summary>
        /// <param name="item">要删除的项目</param>
        /// <param name="childType">子表实体类型</param>
        public void DeleteRelationshipItem(IDataBase item, Type childType)
        {
            var sqliteType = typeof(SqliteUnified<>).MakeGenericType(childType);
            var sqliteInstance = Activator.CreateInstance(sqliteType, _connectionString.Replace("Data Source=", ""));
            var deleteMethod = sqliteType.GetMethod("Delete");
            deleteMethod?.Invoke(sqliteInstance, new object[] { item });
        }

        /// <summary>
        /// 添加单个关联项
        /// </summary>
        /// <param name="parentId">父表ID</param>
        /// <param name="item">要添加的项目</param>
        /// <param name="parentKeyName">父表外键名称</param>
        /// <param name="childType">子表实体类型</param>
        public void AddRelationshipItem(int parentId, IDataBase item, string parentKeyName, Type childType)
        {
            var parentKeyProp = childType.GetProperty(parentKeyName);
            if (parentKeyProp != null)
            {
                parentKeyProp.SetValue(item, parentId);
            }

            var sqliteType = typeof(SqliteUnified<>).MakeGenericType(childType);
            var sqliteInstance = Activator.CreateInstance(sqliteType, _connectionString.Replace("Data Source=", ""));
            var saveMethod = sqliteType.GetMethod("Save");
            saveMethod?.Invoke(sqliteInstance, new object[] { item });
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取SQLite数据类型
        /// </summary>
        /// <param name="type">C#类型</param>
        /// <returns>对应的SQLite类型</returns>
        private string GetSqliteType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            if (type.IsEnum)
                return "TEXT";

            if (type == typeof(int) || type == typeof(long) || type == typeof(short) ||
                type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) ||
                type == typeof(byte) || type == typeof(sbyte) || type == typeof(bool))
                return "INTEGER";

            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
                return "REAL";

            if (type == typeof(DateTime))
                return "TEXT";

            if (type == typeof(Guid))
                return "TEXT";

            if (type == typeof(byte[]))
                return "BLOB";

            if (typeof(IDictionary).IsAssignableFrom(type) || 
                (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type)))
                return "TEXT";

            return "TEXT";
        }

        #endregion
    }
}

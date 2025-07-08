using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Drx.Sdk.Network.Sqlite
{
    /// <summary>
    /// 处理SQLite关联表的工具类（已弃用）
    /// </summary>
    [Obsolete("此类已被 SqliteUnified<T> 的关联表功能替代。请使用 SqliteUnified<T> 的关联表方法。此类将在未来版本中移除。", false)]
    public class SqliteRelationship
    {
        private readonly string _connectionString;

        public SqliteRelationship(string dbPath)
        {
            _connectionString = $"Data Source={dbPath}";
        }

        /// <summary>
        /// 保存关联表数据
        /// </summary>
        /// <typeparam name="TParent">父表实体类型</typeparam>
        /// <typeparam name="TChild">子表实体类型</typeparam>
        /// <param name="parentId">父表ID</param>
        /// <param name="items">要保存的子表项目集合</param>
        /// <param name="tableName">关联表名称</param>
        /// <param name="parentKeyName">父表外键名称</param>
        public void SaveRelationship<TParent, TChild>(int parentId, IEnumerable<TChild> items, string tableName, string parentKeyName)
            where TChild : class, IDataBase, new()
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
                            var parentKeyProp = typeof(TChild).GetProperty(parentKeyName);
                            if (parentKeyProp != null)
                            {
                                parentKeyProp.SetValue(item, parentId);
                            }

                            // 保存子项
                            var sqlite = new Sqlite<TChild>(_connectionString.Replace("Data Source=", ""));
                            sqlite.Save(item);
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
        /// <typeparam name="TParent">父表实体类型</typeparam>
        /// <typeparam name="TChild">子表实体类型</typeparam>
        /// <param name="parentId">父表ID</param>
        /// <param name="items">要修复的子表项目集合</param>
        /// <param name="tableName">关联表名称</param>
        /// <param name="parentKeyName">父表外键名称</param>
        /// <param name="identifierProperty">用于识别特定条目的属性名（除了Id和父键外的唯一标识符）</param>
        public void RepairRelationship<TParent, TChild>(int parentId, IEnumerable<TChild> items, string tableName, string parentKeyName, string identifierProperty)
            where TChild : class, IDataBase, new()
        {
            // 获取现有条目
            var sqlite = new Sqlite<TChild>(_connectionString.Replace("Data Source=", ""));
            var conditions = new Dictionary<string, object>
            {
                { parentKeyName, parentId }
            };
            var existingItems = sqlite.Read(conditions);
            
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
                            var parentKeyProp = typeof(TChild).GetProperty(parentKeyName);
                            if (parentKeyProp != null)
                            {
                                parentKeyProp.SetValue(item, parentId);
                            }

                            // 获取标识符属性
                            var idProp = typeof(TChild).GetProperty(identifierProperty);
                            if (idProp == null)
                            {
                                throw new ArgumentException($"属性 {identifierProperty} 在类型 {typeof(TChild).Name} 中不存在");
                            }

                            // 获取当前项的标识符值
                            var idValue = idProp.GetValue(item);
                            if (idValue == null)
                            {
                                throw new ArgumentException($"标识符属性 {identifierProperty} 的值不能为null");
                            }

                            // 查找匹配的现有项
                            var existingItem = existingItems.FirstOrDefault(e => 
                                object.Equals(idProp.GetValue(e), idValue));

                            // 如果找到匹配项，设置ID以确保更新而不是插入
                            if (existingItem != null)
                            {
                                var idProperty = typeof(IDataBase).GetProperty(nameof(IDataBase.Id));
                                idProperty?.SetValue(item, idProperty.GetValue(existingItem));
                            }

                            // 保存/更新项
                            sqlite.Save(item);
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
        /// <typeparam name="TChild">子表实体类型</typeparam>
        /// <param name="parentId">父表ID</param>
        /// <param name="item">要修复的项目</param>
        /// <param name="parentKeyName">父表外键名称</param>
        /// <param name="identifierProperty">用于识别特定条目的属性名</param>
        public void RepairRelationshipItem<TChild>(int parentId, TChild item, string parentKeyName, string identifierProperty)
            where TChild : class, IDataBase, new()
        {
            // 确保子项有正确的父ID
            var parentKeyProp = typeof(TChild).GetProperty(parentKeyName);
            if (parentKeyProp != null)
            {
                parentKeyProp.SetValue(item, parentId);
            }

            // 获取标识符属性
            var idProp = typeof(TChild).GetProperty(identifierProperty);
            if (idProp == null)
            {
                throw new ArgumentException($"属性 {identifierProperty} 在类型 {typeof(TChild).Name} 中不存在");
            }

            // 获取当前项的标识符值
            var idValue = idProp.GetValue(item);
            if (idValue == null)
            {
                throw new ArgumentException($"标识符属性 {identifierProperty} 的值不能为null");
            }

            // 查找匹配的现有项
            var sqlite = new Sqlite<TChild>(_connectionString.Replace("Data Source=", ""));
            var conditions = new Dictionary<string, object>
            {
                { parentKeyName, parentId },
                { identifierProperty, idValue }
            };
            var existingItem = sqlite.Read(conditions).FirstOrDefault();

            // 如果找到匹配项，设置ID以确保更新而不是插入
            if (existingItem != null)
            {
                var idProperty = typeof(IDataBase).GetProperty(nameof(IDataBase.Id));
                idProperty?.SetValue(item, idProperty.GetValue(existingItem));
            }

            // 保存/更新项
            sqlite.Save(item);
        }

        /// <summary>
        /// 加载关联表数据
        /// </summary>
        /// <typeparam name="TChild">子表实体类型</typeparam>
        /// <param name="parentId">父表ID</param>
        /// <param name="tableName">关联表名称</param>
        /// <param name="parentKeyName">父表外键名称</param>
        /// <returns>子表项目集合</returns>
        public List<TChild> LoadRelationship<TChild>(int parentId, string tableName, string parentKeyName)
            where TChild : class, IDataBase, new()
        {
            var sqlite = new Sqlite<TChild>(_connectionString.Replace("Data Source=", ""));
            var conditions = new Dictionary<string, object>
            {
                { parentKeyName, parentId }
            };
            return sqlite.Read(conditions);
        }

        /// <summary>
        /// 查询特定条件的关联项
        /// </summary>
        /// <typeparam name="TChild">子表实体类型</typeparam>
        /// <param name="parentId">父表ID</param>
        /// <param name="conditions">额外的查询条件</param>
        /// <param name="parentKeyName">父表外键名称</param>
        /// <returns>符合条件的子表项目集合</returns>
        public List<TChild> QueryRelationship<TChild>(int parentId, Dictionary<string, object> conditions, string parentKeyName)
            where TChild : class, IDataBase, new()
        {
            var sqlite = new Sqlite<TChild>(_connectionString.Replace("Data Source=", ""));
            
            var allConditions = new Dictionary<string, object>(conditions)
            {
                { parentKeyName, parentId }
            };
            
            return sqlite.Read(allConditions);
        }

        /// <summary>
        /// 更新单个关联项
        /// </summary>
        /// <typeparam name="TChild">子表实体类型</typeparam>
        /// <param name="item">要更新的项目</param>
        public void UpdateRelationshipItem<TChild>(TChild item)
            where TChild : class, IDataBase, new()
        {
            var sqlite = new Sqlite<TChild>(_connectionString.Replace("Data Source=", ""));
            sqlite.Save(item);
        }

        /// <summary>
        /// 删除单个关联项
        /// </summary>
        /// <typeparam name="TChild">子表实体类型</typeparam>
        /// <param name="item">要删除的项目</param>
        public void DeleteRelationshipItem<TChild>(TChild item)
            where TChild : class, IDataBase, new()
        {
            var sqlite = new Sqlite<TChild>(_connectionString.Replace("Data Source=", ""));
            sqlite.Delete(item);
        }

        /// <summary>
        /// 添加单个关联项
        /// </summary>
        /// <typeparam name="TChild">子表实体类型</typeparam>
        /// <param name="parentId">父表ID</param>
        /// <param name="item">要添加的项目</param>
        /// <param name="parentKeyName">父表外键名称</param>
        public void AddRelationshipItem<TChild>(int parentId, TChild item, string parentKeyName)
            where TChild : class, IDataBase, new()
        {
            var parentKeyProp = typeof(TChild).GetProperty(parentKeyName);
            if (parentKeyProp != null)
            {
                parentKeyProp.SetValue(item, parentId);
            }

            var sqlite = new Sqlite<TChild>(_connectionString.Replace("Data Source=", ""));
            sqlite.Save(item);
        }
    }
} 
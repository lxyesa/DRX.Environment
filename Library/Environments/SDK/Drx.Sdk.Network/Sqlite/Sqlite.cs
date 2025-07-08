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
    /// 泛型SQLite数据库操作封装类（已弃用）
    /// </summary>
    /// <typeparam name="T">要操作的数据类型</typeparam>
    [Obsolete("此类已被 SqliteUnified<T> 替代。请使用 SqliteUnified<T> 以获得更完整的功能，包括关联表处理。此类将在未来版本中移除。", false)]
    public class Sqlite<T> where T : class, IDataBase, new()
    {
        private readonly SqliteUnified<T> _unified;

        /// <summary>
        /// 初始化SQLite封装
        /// </summary>
        /// <param name="path">数据库文件路径</param>
        public Sqlite(string path)
        {
            _unified = new SqliteUnified<T>(path);
        }

        /// <summary>
        /// 保存对象到数据库
        /// </summary>
        /// <param name="item">要保存的对象</param>
        public void Save(T item) => _unified.Save(item);

        /// <summary>
        /// 批量保存对象到数据库
        /// </summary>
        /// <param name="items">要保存的对象集合</param>
        public void SaveAll(IEnumerable<T> items) => _unified.SaveAll(items);

        /// <summary>
        /// 根据条件查询对象
        /// </summary>
        /// <param name="whereConditions">查询条件字典，键为属性名，值为匹配值</param>
        /// <returns>符合条件的对象集合</returns>
        public List<T> Read(Dictionary<string, object>? whereConditions = null) => _unified.Read(whereConditions);

        /// <summary>
        /// 根据单个条件查询对象
        /// </summary>
        /// <param name="where">属性名</param>
        /// <param name="value">属性值</param>
        /// <returns>符合条件的单个对象</returns>
        public T ReadSingle(string where, object value) => _unified.ReadSingle(where, value);

        /// <summary>
        /// 根据主键ID查询单个对象
        /// </summary>
        /// <param name="id">主键ID值</param>
        /// <returns>对象或null</returns>
        public T FindById(int id) => _unified.FindById(id);

        /// <summary>
        /// 删除对象
        /// </summary>
        /// <param name="item">要删除的对象</param>
        public void Delete(T item) => _unified.Delete(item);

        /// <summary>
        /// 根据条件删除对象
        /// </summary>
        /// <param name="whereConditions">条件字典</param>
        /// <returns>删除的行数</returns>
        public int DeleteWhere(Dictionary<string, object> whereConditions) => _unified.DeleteWhere(whereConditions);

        /// <summary>
        /// 根据指定条件修复（替换）数据库中的特定条目
        /// </summary>
        /// <param name="item">包含新数据的对象</param>
        /// <param name="identifierConditions">用于识别要替换的特定条目的条件</param>
        /// <returns>true表示找到并更新了条目，false表示未找到匹配条目（此时会创建新条目）</returns>
        public bool Repair(T item, Dictionary<string, object> identifierConditions) => _unified.Repair(item, identifierConditions);

        /// <summary>
        /// 根据单个条件修复（替换）数据库中的特定条目
        /// </summary>
        /// <param name="item">包含新数据的对象</param>
        /// <param name="propertyName">用于识别的属性名</param>
        /// <param name="propertyValue">用于识别的属性值</param>
        /// <returns>true表示找到并更新了条目，false表示未找到匹配条目（此时会创建新条目）</returns>
        public bool Repair(T item, string propertyName, object propertyValue) => _unified.Repair(item, propertyName, propertyValue);
    }
} 
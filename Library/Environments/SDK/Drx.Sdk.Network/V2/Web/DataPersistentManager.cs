using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Network.DataBase.Sqlite;
using System.Reflection;

namespace Drx.Sdk.Network.V2.Web
{
    /// <summary>
    /// 数据持久化管理器（内存 + 持久化到 sqlite）
    /// 说明：此类对外提供把任意继承自 Models.DataModelBase 的实体按 id 分组的能力，
    /// 并支持把某个分组持久化为 {id}.db（使用 SqliteUnified<T> 实现）。
    /// </summary>
    internal class DataPersistentManager
    {
        // 内部桶，保存具体的元素类型与列表
        private class DataBucket
        {
            public Type ElementType { get; }
            public IList List { get; }
            public object LockObj { get; } = new object();

            public DataBucket(Type t)
            {
                ElementType = t;
                List = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(t))!;
            }
        }

        // key => DataBucket
        private readonly ConcurrentDictionary<string, DataBucket> _storage = new();

        /// <summary>
        /// 将实体添加到指定 id 的分组中（线程安全）。若该 id 尚未注册则自动创建分组。
        /// </summary>
        /// <typeparam name="T">实体类型，需继承 Models.DataModelBase</typeparam>
        /// <param name="entity">实体对象（非 null）</param>
        /// <param name="id">分组标识</param>
        public void Add<T>(T entity, string id) where T : Models.DataModelBase
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("id 不能为空", nameof(id));

            var bucket = _storage.GetOrAdd(id, _ => new DataBucket(typeof(T)));

            if (bucket.ElementType != typeof(T))
                throw new InvalidOperationException($"分组 '{id}' 的元素类型冲突：期望 {bucket.ElementType.FullName}，但尝试加入 {typeof(T).FullName}");

            lock (bucket.LockObj)
            {
                bucket.List.Add(entity);
            }
        }

        /// <summary>
        /// 获取指定 id 的分组数据。若不存在返回空列表（非 null）。
        /// </summary>
        /// <typeparam name="T">期望的元素类型</typeparam>
        /// <param name="id">分组标识</param>
        /// <returns>元素列表（浅拷贝）</returns>
        public List<T> Get<T>(string id) where T : Models.DataModelBase
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("id 不能为空", nameof(id));

            if (!_storage.TryGetValue(id, out var bucket)) return new List<T>();
            if (bucket.ElementType != typeof(T)) throw new InvalidOperationException($"请求类型与分组实际元素类型不匹配：{bucket.ElementType.FullName}");

            lock (bucket.LockObj)
            {
                // 返回浅拷贝以避免外部修改内部集合
                return ((IEnumerable<T>)bucket.List.Cast<T>()).ToList();
            }
        }

        /// <summary>
        /// 把指定 id 的分组数据持久化到磁盘，文件名为 {id}.db（位于 basePath 或当前目录）。
        /// 实现：通过反射构造 SqliteUnified&lt;T&gt;，先 ClearAsync，然后逐条 Push。
        /// 此方法为同步阻塞调用（内部会等待异步方法完成），若需要异步保存请自行封装为 Task.Run。
        /// </summary>
        /// <param name="id">分组标识</param>
        /// <param name="basePath">可选：存储基路径（传入 null 则使用当前工作目录或 SqliteUnified 的默认行为）</param>
        public void Save(string id, string? basePath = null)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("id 不能为空", nameof(id));

            if (!_storage.TryGetValue(id, out var bucket)) return; // 无数据，无操作

            // 构造 SqliteUnified<T>
            var elementType = bucket.ElementType;
            var sqliteGeneric = typeof(SqliteUnified<>).MakeGenericType(elementType);

            // 文件名使用 id.db
            var dbFileName = id.EndsWith(".db") ? id : id + ".db";

            object? sqliteInstance = null;
            try
            {
                sqliteInstance = Activator.CreateInstance(sqliteGeneric, dbFileName, basePath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"创建 SqliteUnified<{elementType.Name}> 实例失败: {ex.Message}", ex);
            }

            // 尝试先调用 ClearAsync(CancellationToken)
            try
            {
                var clearMethod = sqliteGeneric.GetMethod("ClearAsync", new Type[] { typeof(CancellationToken) });
                if (clearMethod != null)
                {
                    var task = (System.Threading.Tasks.Task)clearMethod.Invoke(sqliteInstance, new object[] { CancellationToken.None })!;
                    task.GetAwaiter().GetResult();
                }
                else
                {
                    // 没有带参数的 ClearAsync？尝试无参 ClearAsync
                    clearMethod = sqliteGeneric.GetMethod("ClearAsync", Type.EmptyTypes);
                    if (clearMethod != null)
                    {
                        var task = (System.Threading.Tasks.Task)clearMethod.Invoke(sqliteInstance, null)!;
                        task.GetAwaiter().GetResult();
                    }
                }
            }
            catch (TargetInvocationException tie)
            {
                throw new InvalidOperationException($"在调用 ClearAsync 时发生错误: {tie.InnerException?.Message ?? tie.Message}", tie.InnerException ?? tie);
            }

            // 逐条 Push
            var pushMethod = sqliteGeneric.GetMethod("Push", new Type[] { elementType });
            if (pushMethod == null)
            {
                throw new InvalidOperationException($"未在 SqliteUnified<{elementType.Name}> 中找到 Push 方法");
            }

            lock (bucket.LockObj)
            {
                foreach (var item in bucket.List)
                {
                    try
                    {
                        pushMethod.Invoke(sqliteInstance, new object[] { item });
                    }
                    catch (TargetInvocationException tie)
                    {
                        throw new InvalidOperationException($"调用 Push 时发生错误: {tie.InnerException?.Message ?? tie.Message}", tie.InnerException ?? tie);
                    }
                }
            }
        }
    }
}

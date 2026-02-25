using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Network.DataBase.Sqlite;
using System.Reflection;
using System.IO;

namespace Drx.Sdk.Network.V2.Web.Utilities
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
        // elementType => reflection cache
        private readonly ConcurrentDictionary<Type, ReflectionCacheEntry> _reflectionCache = new();

        // 反射缓存条目，缓存构造函数与方法信息以减少高频反射开销
        private class ReflectionCacheEntry
        {
            public Type SqliteGenericType { get; set; } = null!;
            public ConstructorInfo? Constructor { get; set; }
            public MethodInfo? ClearAsyncWithCt { get; set; }
            public MethodInfo? ClearAsyncNoArgs { get; set; }
            public MethodInfo? PushMethod { get; set; }
        }

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
            // 复用异步实现，避免重复反射逻辑
            SaveAsync(id, basePath, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 泛型同步版本 Save：在调用方知道实体类型时使用此方法可避免反射。
        /// </summary>
        /// <typeparam name="T">实体类型，需继承 Models.DataModelBase 且有无参构造</typeparam>
        /// <param name="id">分组标识</param>
        /// <param name="basePath">可选基路径</param>
        public void Save<T>(string id, string? basePath = null) where T : Models.DataModelBase, new()
        {
            SaveAsync<T>(id, basePath, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 从磁盘加载指定 id 的分组数据到内存（同步阻塞）。
        /// 若分组不存在则会创建分组并填充数据；若分组已存在则会替换内存中的内容。
        /// </summary>
        /// <typeparam name="T">实体类型，需继承 Models.DataModelBase 且有无参构造</typeparam>
        /// <param name="id">分组标识</param>
        /// <param name="basePath">可选基路径</param>
        public void Load<T>(string id, string? basePath = null) where T : Models.DataModelBase, new()
        {
            LoadAsync<T>(id, basePath, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 判断指定 id 的分组在持久化存储中是否存在任意记录（同步）。
        /// </summary>
        public bool Exists<T>(string id, string? basePath = null) where T : Models.DataModelBase, new()
        {
            return ExistsAsync<T>(id, basePath, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// 判断指定 id 的分组在持久化存储中是否存在任意记录（异步）。
        /// 实现：通过 SqliteUnified<T>.GetAllAsync 读取并检查是否有元素。
        /// </summary>
        public async Task<bool> ExistsAsync<T>(string id, string? basePath = null, CancellationToken ct = default) where T : Models.DataModelBase, new()
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("id 不能为空", nameof(id));
            if (!_storage.TryGetValue(id, out var bucket))
            {
                // 分组内存不存在，先检查磁盘上是否存在 db 文件，避免构造 SqliteUnified 导致创建空文件
                var dbFileName = id.EndsWith(".db") ? id : id + ".db";
                var baseDir = string.IsNullOrEmpty(basePath) ? AppDomain.CurrentDomain.BaseDirectory : basePath!;
                var fullPath = Path.Combine(baseDir, dbFileName);
                if (!File.Exists(fullPath))
                {
                    return false;
                }

                try
                {
                    var sqlite = new Drx.Sdk.Network.DataBase.Sqlite.SqliteUnified<T>(dbFileName, basePath);
                    var all = await sqlite.GetAllAsync(ct).ConfigureAwait(false);
                    return all != null && all.Count > 0;
                }
                catch
                {
                    return false;
                }
            }

            // 如果内存中已有分组，直接判断内存集合是否有元素
            if (bucket.ElementType != typeof(T))
                throw new InvalidOperationException($"分组 '{id}' 的元素类型冲突：期望 {bucket.ElementType.FullName}，但请求 {typeof(T).FullName}");

            lock (bucket.LockObj)
            {
                return bucket.List.Count > 0;
            }
        }

        /// <summary>
        /// 异步从磁盘加载指定 id 的分组数据到内存。
        /// 实现：使用 SqliteUnified<T>.GetAllAsync 读取所有实体，然后用锁替换内存桶中的集合内容。
        /// </summary>
        /// <typeparam name="T">实体类型，需继承 Models.DataModelBase 且有无参构造</typeparam>
        /// <param name="id">分组标识</param>
        /// <param name="basePath">可选基路径</param>
        /// <param name="ct">取消令牌</param>
        public async Task LoadAsync<T>(string id, string? basePath = null, CancellationToken ct = default) where T : Models.DataModelBase, new()
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("id 不能为空", nameof(id));

            // 文件名使用 id.db
            var dbFileName = id.EndsWith(".db") ? id : id + ".db";

            // 若磁盘上不存在该 db 文件，则不创建新文件，直接确保内存桶存在并返回（避免生成空的 .db）
            var baseDir = string.IsNullOrEmpty(basePath) ? AppDomain.CurrentDomain.BaseDirectory : basePath!;
            var fullPath = Path.Combine(baseDir, dbFileName);
            if (!File.Exists(fullPath))
            {
                // 确保创建空的内存桶但不触发 SqliteUnified 的构造
                _storage.GetOrAdd(id, _ => new DataBucket(typeof(T)));
                return;
            }

            // 构造 SqliteUnified<T> 并读取所有数据
            var sqlite = new Drx.Sdk.Network.DataBase.Sqlite.SqliteUnified<T>(dbFileName, basePath);

            List<T> items;
            try
            {
                items = await sqlite.GetAllAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"从数据库加载 SqliteUnified<{typeof(T).Name}> 数据失败: {ex.Message}", ex);
            }

            // 获取或创建桶
            var bucket = _storage.GetOrAdd(id, _ => new DataBucket(typeof(T)));

            if (bucket.ElementType != typeof(T))
                throw new InvalidOperationException($"分组 '{id}' 的元素类型冲突：期望 {bucket.ElementType.FullName}，但尝试加载 {typeof(T).FullName}");

            // 用锁替换内部集合内容（先清空再追加），保持外部引用不变
            lock (bucket.LockObj)
            {
                // IList 支持 Clear()
                try
                {
                    bucket.List.Clear();
                }
                catch
                {
                    // 若 Clear 不可用，创建新的 List 并替换（尽量不发生）
                    var newList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(typeof(T)))!;
                    _storage[id] = new DataBucket(typeof(T));
                }

                foreach (var it in items)
                {
                    bucket.List.Add(it);
                }
            }
        }

        /// <summary>
        /// 泛型异步版本 Save：在调用方知道实体类型时使用此方法可避免反射。
        /// </summary>
        /// <typeparam name="T">实体类型，需继承 Models.DataModelBase 且有无参构造</typeparam>
        /// <param name="id">分组标识</param>
        /// <param name="basePath">可选基路径</param>
        /// <param name="ct">取消令牌</param>
        public async Task SaveAsync<T>(string id, string? basePath = null, CancellationToken ct = default) where T : Models.DataModelBase, new()
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("id 不能为空", nameof(id));

            if (!_storage.TryGetValue(id, out var bucket)) return; // 无数据，无操作

            if (bucket.ElementType != typeof(T))
                throw new InvalidOperationException($"请求类型与分组实际元素类型不匹配：{bucket.ElementType.FullName}");

            // 文件名使用 id.db
            var dbFileName = id.EndsWith(".db") ? id : id + ".db";

            var sqlite = new SqliteUnified<T>(dbFileName, basePath);

            // 先清空目标表
            await sqlite.ClearAsync(ct).ConfigureAwait(false);

            // 复制集合以缩短锁持有时间
            List<T> items;
            lock (bucket.LockObj)
            {
                items = ((IEnumerable<T>)bucket.List.Cast<T>()).ToList();
            }

            // 逐条 Push（SqliteUnified.Push 为同步方法）
            foreach (var item in items)
            {
                sqlite.Push(item);
            }
        }

        /// <summary>
        /// 异步版本的 Save，实现反射结果缓存以减少高频反射开销。
        /// </summary>
        /// <param name="id">分组标识</param>
        /// <param name="basePath">可选基路径</param>
        /// <param name="ct">取消令牌</param>
        /// <returns>任务</returns>
        public async Task SaveAsync(string id, string? basePath = null, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("id 不能为空", nameof(id));

            if (!_storage.TryGetValue(id, out var bucket)) return; // 无数据，无操作

            var elementType = bucket.ElementType;
            // 获取或创建反射缓存条目
            var cacheEntry = _reflectionCache.GetOrAdd(elementType, t => BuildReflectionCacheEntry(t));

            // 文件名使用 id.db
            var dbFileName = id.EndsWith(".db") ? id : id + ".db";

            object? sqliteInstance = null;
            try
            {
                if (cacheEntry.Constructor != null)
                {
                    var ctorParams = cacheEntry.Constructor.GetParameters();
                    if (ctorParams.Length == 2)
                    {
                        sqliteInstance = cacheEntry.Constructor.Invoke(new object?[] { dbFileName, basePath });
                    }
                    else if (ctorParams.Length == 1)
                    {
                        sqliteInstance = cacheEntry.Constructor.Invoke(new object?[] { dbFileName });
                    }
                    else
                    {
                        sqliteInstance = cacheEntry.Constructor.Invoke(null);
                    }
                }
                else
                {
                    // 回退到 Activator.CreateInstance
                    sqliteInstance = Activator.CreateInstance(cacheEntry.SqliteGenericType, dbFileName, basePath);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"创建 SqliteUnified<{elementType.Name}> 实例失败: {ex.Message}", ex);
            }

            // 调用 ClearAsync
            try
            {
                if (cacheEntry.ClearAsyncWithCt != null)
                {
                    var task = (Task)cacheEntry.ClearAsyncWithCt.Invoke(sqliteInstance, new object[] { ct })!;
                    await task.ConfigureAwait(false);
                }
                else if (cacheEntry.ClearAsyncNoArgs != null)
                {
                    var task = (Task)cacheEntry.ClearAsyncNoArgs.Invoke(sqliteInstance, null)!;
                    await task.ConfigureAwait(false);
                }
            }
            catch (TargetInvocationException tie)
            {
                throw new InvalidOperationException($"在调用 ClearAsync 时发生错误: {tie.InnerException?.Message ?? tie.Message}", tie.InnerException ?? tie);
            }

            if (cacheEntry.PushMethod == null)
            {
                throw new InvalidOperationException($"未在 SqliteUnified<{elementType.Name}> 中找到 Push 方法");
            }

            // 复制集合以缩短锁时间
            var items = new List<object?>();
            lock (bucket.LockObj)
            {
                foreach (var it in bucket.List)
                {
                    items.Add(it);
                }
            }

            foreach (var item in items)
            {
                try
                {
                    var result = cacheEntry.PushMethod.Invoke(sqliteInstance, new object?[] { item });
                    if (result is Task t)
                    {
                        await t.ConfigureAwait(false);
                    }
                }
                catch (TargetInvocationException tie)
                {
                    throw new InvalidOperationException($"调用 Push 时发生错误: {tie.InnerException?.Message ?? tie.Message}", tie.InnerException ?? tie);
                }
            }
        }

        // 构建并返回用于特定元素类型的反射缓存条目
        private ReflectionCacheEntry BuildReflectionCacheEntry(Type elementType)
        {
            var sqliteGeneric = typeof(SqliteUnified<>).MakeGenericType(elementType);
            var entry = new ReflectionCacheEntry { SqliteGenericType = sqliteGeneric };

            // 尝试查找合适的构造函数（优先 string, string）
            var ctors = sqliteGeneric.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            entry.Constructor = ctors.OrderByDescending(c => c.GetParameters().Length)
                                     .FirstOrDefault(c => c.GetParameters().Length >= 1 && c.GetParameters()[0].ParameterType == typeof(string));

            // ClearAsync
            entry.ClearAsyncWithCt = sqliteGeneric.GetMethod("ClearAsync", new Type[] { typeof(CancellationToken) });
            if (entry.ClearAsyncWithCt == null)
            {
                entry.ClearAsyncNoArgs = sqliteGeneric.GetMethod("ClearAsync", Type.EmptyTypes);
            }

            // Push 方法
            entry.PushMethod = sqliteGeneric.GetMethod("Push", new Type[] { elementType });

            return entry;
        }
    }
}

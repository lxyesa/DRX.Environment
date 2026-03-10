using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Drx.Sdk.Shared.JavaScript.Engine
{
    /// <summary>
    /// 线程安全的模块实例缓存。
    /// 缓存键为规范化绝对路径（大小写不敏感），支持 single-flight 并发合并加载与缓存命中统计。
    /// </summary>
    public sealed class ModuleCache
    {
        private readonly ConcurrentDictionary<string, ModuleRecord> _cache =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly ConcurrentDictionary<string, Lazy<ModuleRecord>> _inflight =
            new(StringComparer.OrdinalIgnoreCase);

        private long _hitCount;
        private long _missCount;

        /// <summary>
        /// 缓存命中次数。
        /// </summary>
        public long HitCount => Interlocked.Read(ref _hitCount);

        /// <summary>
        /// 缓存未命中次数。
        /// </summary>
        public long MissCount => Interlocked.Read(ref _missCount);

        /// <summary>
        /// 当前缓存条目数。
        /// </summary>
        public int Count => _cache.Count;

        /// <summary>
        /// 生成标准化缓存键（规范化绝对路径）。
        /// </summary>
        public static string NormalizeCacheKey(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                throw new ArgumentException("缓存键路径不能为空。", nameof(absolutePath));
            }

            return Path.GetFullPath(absolutePath)
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// 尝试从缓存中获取已加载模块。
        /// </summary>
        /// <returns>命中返回 true；未命中返回 false。</returns>
        public bool TryGet(string cacheKey, out ModuleRecord? record)
        {
            if (_cache.TryGetValue(cacheKey, out record))
            {
                Interlocked.Increment(ref _hitCount);
                return true;
            }

            Interlocked.Increment(ref _missCount);
            record = null;
            return false;
        }

        /// <summary>
        /// Single-flight 获取或加载模块：对同一 cacheKey 的并发请求仅执行一次 factory，
        /// 其余等待同一结果。
        /// </summary>
        /// <param name="cacheKey">规范化缓存键。</param>
        /// <param name="factory">加载工厂（仅首次调用执行）。</param>
        /// <returns>已加载（或加载失败）的模块记录。</returns>
        public ModuleRecord GetOrLoad(string cacheKey, Func<ModuleRecord> factory)
        {
            if (_cache.TryGetValue(cacheKey, out var existing))
            {
                Interlocked.Increment(ref _hitCount);
                return existing;
            }

            Interlocked.Increment(ref _missCount);

            var lazy = _inflight.GetOrAdd(cacheKey, _ => new Lazy<ModuleRecord>(factory, LazyThreadSafetyMode.ExecutionAndPublication));

            try
            {
                var record = lazy.Value;

                // 加载完成后移入稳定缓存
                if (record.State == ModuleRecordState.Loaded || record.State == ModuleRecordState.Failed)
                {
                    _cache.TryAdd(cacheKey, record);
                }

                return record;
            }
            finally
            {
                _inflight.TryRemove(cacheKey, out _);
            }
        }

        /// <summary>
        /// 注册一个正在加载（Loading 状态）的占位记录，用于循环依赖检测。
        /// </summary>
        /// <returns>如果已存在（循环依赖或并发），返回 false 并输出已有记录。</returns>
        public bool TryRegisterLoading(string cacheKey, ModuleRecord loadingRecord, out ModuleRecord? existing)
        {
            if (_cache.TryGetValue(cacheKey, out existing))
            {
                return false;
            }

            if (_cache.TryAdd(cacheKey, loadingRecord))
            {
                existing = null;
                return true;
            }

            existing = _cache[cacheKey];
            return false;
        }

        /// <summary>
        /// 更新缓存中的模块记录（状态转换后替换）。
        /// </summary>
        public void Update(string cacheKey, ModuleRecord record)
        {
            _cache[cacheKey] = record;
        }

        /// <summary>
        /// 检查是否存在指定 cacheKey 的模块记录。
        /// </summary>
        public bool Contains(string cacheKey) => _cache.ContainsKey(cacheKey);

        /// <summary>
        /// 检查指定 cacheKey 的模块是否正在加载中（用于循环依赖检测）。
        /// </summary>
        public bool IsLoading(string cacheKey)
        {
            return _cache.TryGetValue(cacheKey, out var record) && record.State == ModuleRecordState.Loading;
        }

        /// <summary>
        /// 失效单模块：仅移除指定 cacheKey 的缓存与 in-flight 加载占位。
        /// </summary>
        /// <param name="cacheKey">目标模块缓存键。</param>
        /// <returns>若存在并成功移除缓存项返回 true；否则返回 false。</returns>
        public bool InvalidateModule(string cacheKey)
        {
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                throw new ArgumentException("缓存键不能为空。", nameof(cacheKey));
            }

            _inflight.TryRemove(cacheKey, out _);
            return _cache.TryRemove(cacheKey, out _);
        }

        /// <summary>
        /// 失效所有依赖指定模块的依赖者（直接/间接，不包含自身）。
        /// </summary>
        /// <param name="cacheKey">变更模块缓存键。</param>
        /// <returns>实际被失效的依赖者键集合。</returns>
        public IReadOnlyList<string> InvalidateDependents(string cacheKey)
        {
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                throw new ArgumentException("缓存键不能为空。", nameof(cacheKey));
            }

            var toInvalidate = CollectDependents(cacheKey, includeRoot: false);
            RemoveKeys(toInvalidate);
            return toInvalidate;
        }

        /// <summary>
        /// 失效指定模块及其所有依赖者（直接/间接）。
        /// </summary>
        /// <param name="cacheKey">变更模块缓存键。</param>
        /// <returns>实际被失效的键集合（包含自身）。</returns>
        public IReadOnlyList<string> InvalidateWithDependents(string cacheKey)
        {
            if (string.IsNullOrWhiteSpace(cacheKey))
            {
                throw new ArgumentException("缓存键不能为空。", nameof(cacheKey));
            }

            var toInvalidate = CollectDependents(cacheKey, includeRoot: true);
            RemoveKeys(toInvalidate);
            return toInvalidate;
        }

        /// <summary>
        /// 获取所有缓存条目的只读快照（用于诊断）。
        /// </summary>
        public IReadOnlyDictionary<string, ModuleRecord> GetSnapshot()
        {
            return new Dictionary<string, ModuleRecord>(_cache, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 获取缓存统计信息。
        /// </summary>
        public ModuleCacheStatistics GetStatistics()
        {
            return new ModuleCacheStatistics(
                TotalEntries: _cache.Count,
                HitCount: HitCount,
                MissCount: MissCount,
                LoadedCount: CountByState(ModuleRecordState.Loaded),
                FailedCount: CountByState(ModuleRecordState.Failed),
                LoadingCount: CountByState(ModuleRecordState.Loading));
        }

        /// <summary>
        /// 清空全部缓存。
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
            _inflight.Clear();
            Interlocked.Exchange(ref _hitCount, 0);
            Interlocked.Exchange(ref _missCount, 0);
        }

        private IReadOnlyList<string> CollectDependents(string cacheKey, bool includeRoot)
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            var reverseGraph = BuildReverseDependencyGraph();
            var visited = new HashSet<string>(comparer);
            var queue = new Queue<string>();

            if (includeRoot)
            {
                visited.Add(cacheKey);
            }

            queue.Enqueue(cacheKey);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                if (!reverseGraph.TryGetValue(current, out var dependents))
                {
                    continue;
                }

                foreach (var dependent in dependents)
                {
                    if (!visited.Add(dependent))
                    {
                        continue;
                    }

                    queue.Enqueue(dependent);
                }
            }

            return visited.Count == 0 ? Array.Empty<string>() : new List<string>(visited);
        }

        private Dictionary<string, HashSet<string>> BuildReverseDependencyGraph()
        {
            var comparer = StringComparer.OrdinalIgnoreCase;
            var graph = new Dictionary<string, HashSet<string>>(comparer);

            foreach (var item in _cache)
            {
                var dependentKey = item.Key;
                var dependencies = item.Value.Dependencies;

                foreach (var dependency in dependencies)
                {
                    if (string.IsNullOrWhiteSpace(dependency))
                    {
                        continue;
                    }

                    if (!graph.TryGetValue(dependency, out var dependents))
                    {
                        dependents = new HashSet<string>(comparer);
                        graph[dependency] = dependents;
                    }

                    dependents.Add(dependentKey);
                }
            }

            return graph;
        }

        private void RemoveKeys(IReadOnlyList<string> keys)
        {
            foreach (var key in keys)
            {
                _inflight.TryRemove(key, out _);
                _cache.TryRemove(key, out _);
            }
        }

        private int CountByState(ModuleRecordState state)
        {
            var count = 0;
            foreach (var kvp in _cache)
            {
                if (kvp.Value.State == state) count++;
            }
            return count;
        }
    }

    /// <summary>
    /// 模块缓存统计信息。
    /// </summary>
    public sealed record ModuleCacheStatistics(
        int TotalEntries,
        long HitCount,
        long MissCount,
        int LoadedCount,
        int FailedCount,
        int LoadingCount)
    {
        /// <summary>
        /// 缓存命中率（0.0 ~ 1.0）。
        /// </summary>
        public double HitRate => (HitCount + MissCount) == 0 ? 0.0 : (double)HitCount / (HitCount + MissCount);
    }
}

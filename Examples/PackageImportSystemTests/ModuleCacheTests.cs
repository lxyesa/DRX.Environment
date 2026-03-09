using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Shared.JavaScript.Engine;
using FluentAssertions;
using Xunit;

namespace PackageImportSystemTests
{
    /// <summary>
    /// ModuleCache 单元测试：覆盖 REQ-PERF-007（缓存性能）+ 线程安全 + 并发场景。
    /// </summary>
    public class ModuleCacheTests
    {
        #region 基础 TryGet / GetOrLoad

        [Fact]
        public void TryGet_NonExistentKey_ReturnsFalse()
        {
            var cache = new ModuleCache();
            cache.TryGet("nonexistent", out var record).Should().BeFalse();
            record.Should().BeNull();
        }

        [Fact]
        public void GetOrLoad_FirstCall_ExecutesFactory()
        {
            var cache = new ModuleCache();
            var key = ModuleCache.NormalizeCacheKey(TestHelper.Fixture("esm", "simple-export.mjs"));

            var record = cache.GetOrLoad(key, () =>
            {
                var r = new ModuleRecord(key, key, ModuleKind.Esm);
                r.MarkLoaded(null, new Dictionary<string, object?>(), Array.Empty<string>(), TimeSpan.FromMilliseconds(10));
                return r;
            });

            record.Should().NotBeNull();
            record.State.Should().Be(ModuleRecordState.Loaded);
        }

        [Fact]
        public void GetOrLoad_SecondCall_ReturnsCachedRecord()
        {
            var cache = new ModuleCache();
            var key = ModuleCache.NormalizeCacheKey(TestHelper.Fixture("esm", "simple-export.mjs"));
            var callCount = 0;

            var factory = () =>
            {
                Interlocked.Increment(ref callCount);
                var r = new ModuleRecord(key, key, ModuleKind.Esm);
                r.MarkLoaded(null, new Dictionary<string, object?>(), Array.Empty<string>(), TimeSpan.FromMilliseconds(10));
                return r;
            };

            cache.GetOrLoad(key, factory);
            cache.GetOrLoad(key, factory);

            callCount.Should().Be(1, "factory 应仅执行一次（single-flight）");
        }

        #endregion

        #region 缓存统计 (REQ-PERF-007)

        [Fact]
        public void Statistics_ShouldTrackHitsAndMisses()
        {
            var cache = new ModuleCache();
            var key = ModuleCache.NormalizeCacheKey(TestHelper.Fixture("esm", "simple-export.mjs"));

            // Miss
            cache.TryGet(key, out _);

            // Load
            cache.GetOrLoad(key, () =>
            {
                var r = new ModuleRecord(key, key, ModuleKind.Esm);
                r.MarkLoaded(null, new Dictionary<string, object?>(), Array.Empty<string>(), TimeSpan.Zero);
                return r;
            });

            // Hit
            cache.TryGet(key, out _);

            var stats = cache.GetStatistics();
            stats.HitCount.Should().BeGreaterThan(0, "应记录命中次数");
            stats.MissCount.Should().BeGreaterThan(0, "应记录未命中次数");
        }

        #endregion

        #region 循环依赖占位 (TryRegisterLoading)

        [Fact]
        public void TryRegisterLoading_FirstRegister_ShouldSucceed()
        {
            var cache = new ModuleCache();
            var key = "circular-test-key";
            var record = new ModuleRecord(key, key, ModuleKind.Esm);

            var success = cache.TryRegisterLoading(key, record, out var existing);
            success.Should().BeTrue();
            existing.Should().BeNull();
        }

        [Fact]
        public void TryRegisterLoading_Duplicate_ShouldReturnExisting()
        {
            var cache = new ModuleCache();
            var key = "circular-dup-key";
            var r1 = new ModuleRecord(key, key, ModuleKind.Esm);
            var r2 = new ModuleRecord(key, key, ModuleKind.Esm);

            cache.TryRegisterLoading(key, r1, out _);
            var success = cache.TryRegisterLoading(key, r2, out var existing);
            success.Should().BeFalse();
            existing.Should().BeSameAs(r1);
        }

        #endregion

        #region NormalizeCacheKey 边界

        [Fact]
        public void NormalizeCacheKey_EmptyPath_ShouldThrow()
        {
            var act = () => ModuleCache.NormalizeCacheKey("");
            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void NormalizeCacheKey_SamePath_DifferentSeparators_ShouldNormalize()
        {
            var path1 = @"C:\some\path\file.js";
            var path2 = @"C:/some/path/file.js";

            var k1 = ModuleCache.NormalizeCacheKey(path1);
            var k2 = ModuleCache.NormalizeCacheKey(path2);
            k1.Should().Be(k2, "不同分隔符的同一路径应规范化为相同缓存键");
        }

        #endregion

        #region 并发安全 (Single-Flight)

        [Fact]
        public void GetOrLoad_ConcurrentCalls_SameKey_FactoryExecutesOnce()
        {
            var cache = new ModuleCache();
            var key = "concurrent-key";
            var factoryCallCount = 0;
            var barrier = new ManualResetEventSlim(false);

            var factory = () =>
            {
                Interlocked.Increment(ref factoryCallCount);
                barrier.Wait(TimeSpan.FromSeconds(2)); // 模拟慢加载
                var r = new ModuleRecord(key, key, ModuleKind.Esm);
                r.MarkLoaded(null, new Dictionary<string, object?>(), Array.Empty<string>(), TimeSpan.Zero);
                return r;
            };

            var tasks = new Task<ModuleRecord>[10];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(() => cache.GetOrLoad(key, factory));
            }

            Thread.Sleep(100); // 让所有线程开始
            barrier.Set(); // 释放

            Task.WaitAll(tasks);
            factoryCallCount.Should().Be(1, "并发环境下 factory 仅执行一次");
        }

        #endregion

        #region Clear / GetSnapshot

        [Fact]
        public void Clear_ShouldResetAll()
        {
            var cache = new ModuleCache();
            var key = "clear-test";
            cache.GetOrLoad(key, () =>
            {
                var r = new ModuleRecord(key, key, ModuleKind.Esm);
                r.MarkLoaded(null, new Dictionary<string, object?>(), Array.Empty<string>(), TimeSpan.Zero);
                return r;
            });

            cache.Count.Should().Be(1);
            cache.Clear();
            cache.Count.Should().Be(0);
        }

        [Fact]
        public void GetSnapshot_ShouldReturnReadonlyCopy()
        {
            var cache = new ModuleCache();
            var key = "snapshot-test";
            cache.GetOrLoad(key, () =>
            {
                var r = new ModuleRecord(key, key, ModuleKind.Esm);
                r.MarkLoaded(null, new Dictionary<string, object?>(), Array.Empty<string>(), TimeSpan.Zero);
                return r;
            });

            var snapshot = cache.GetSnapshot();
            snapshot.Should().ContainKey(key);
        }

        #endregion
    }
}

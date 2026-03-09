using System;
using System.Collections.Generic;
using System.IO;
using Drx.Sdk.Shared.JavaScript.Abstractions;
using Drx.Sdk.Shared.JavaScript.Engine;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace PackageImportSystemTests
{
    /// <summary>
    /// 集成测试：CLI 选项 → ModuleRuntimeOptions → Resolver → Loader → ModuleGraph 全链路。
    /// 覆盖 REQ-ESM-001, REQ-CJS-002, REQ-DYN-003, REQ-PKG-004, REQ-COMPAT-005。
    /// </summary>
    public class ModuleLoaderIntegrationTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _tempRoot;

        public ModuleLoaderIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
            _tempRoot = Path.Combine(Path.GetTempPath(), "paperclip-int-" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_tempRoot);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempRoot, recursive: true); } catch { }
        }

        private static Func<string, string, object?> PassthroughExecutor => (path, source) => source;

        #region ESM 模块图加载 (REQ-ESM-001)

        [Fact]
        public void LoadModuleGraph_SingleEntryNoImports_ShouldLoadAndCache()
        {
            var entryPath = Path.Combine(_tempRoot, "entry.mjs");
            File.WriteAllText(entryPath, "export const x = 1;");

            var options = ModuleRuntimeOptions.CreateSecureDefaults(_tempRoot);
            var resolver = new ModuleResolver(options);
            var cache = new ModuleCache();
            var loader = new ModuleLoader(resolver, cache, options);

            var record = loader.LoadModuleGraph(entryPath, PassthroughExecutor);
            record.State.Should().Be(ModuleRecordState.Loaded);
            cache.Count.Should().BeGreaterThanOrEqualTo(1);
        }

        [Fact]
        public void LoadModuleGraph_ChainedImports_ShouldResolveAll()
        {
            File.WriteAllText(Path.Combine(_tempRoot, "a.mjs"), "import { b } from './b.mjs';\nexport const a = 'A' + b;");
            File.WriteAllText(Path.Combine(_tempRoot, "b.mjs"), "import { c } from './c.mjs';\nexport const b = 'B' + c;");
            File.WriteAllText(Path.Combine(_tempRoot, "c.mjs"), "export const c = 'C';");

            var options = ModuleRuntimeOptions.CreateSecureDefaults(_tempRoot);
            var resolver = new ModuleResolver(options);
            var cache = new ModuleCache();
            var loader = new ModuleLoader(resolver, cache, options);

            var record = loader.LoadModuleGraph(Path.Combine(_tempRoot, "a.mjs"), PassthroughExecutor);
            record.State.Should().Be(ModuleRecordState.Loaded);
            cache.Count.Should().BeGreaterThanOrEqualTo(3, "a → b → c 三个模块都应缓存");
        }

        [Fact]
        public void LoadModuleGraph_MissingDependency_ShouldFail()
        {
            File.WriteAllText(Path.Combine(_tempRoot, "bad-entry.mjs"), "import { x } from './missing.mjs';\nexport const z = x;");

            var options = ModuleRuntimeOptions.CreateSecureDefaults(_tempRoot);
            var resolver = new ModuleResolver(options);
            var cache = new ModuleCache();
            var loader = new ModuleLoader(resolver, cache, options);

            var act = () => loader.LoadModuleGraph(Path.Combine(_tempRoot, "bad-entry.mjs"), PassthroughExecutor);
            act.Should().Throw<Exception>("缺失依赖应导致加载失败");
        }

        #endregion

        #region 循环依赖 (REQ-ESM-001)

        [Fact]
        public void LoadModuleGraph_CircularDependency_ShouldNotHang()
        {
            File.WriteAllText(Path.Combine(_tempRoot, "circ-a.mjs"), "import { vb } from './circ-b.mjs';\nexport const va = 'A';");
            File.WriteAllText(Path.Combine(_tempRoot, "circ-b.mjs"), "import { va } from './circ-a.mjs';\nexport const vb = 'B';");

            var options = ModuleRuntimeOptions.CreateSecureDefaults(_tempRoot);
            var resolver = new ModuleResolver(options);
            var cache = new ModuleCache();
            var loader = new ModuleLoader(resolver, cache, options);

            // 循环依赖不应死锁——应当在合理时间内返回（可能部分值为 undefined）
            var act = () => loader.LoadModuleGraph(Path.Combine(_tempRoot, "circ-a.mjs"), PassthroughExecutor);
            act.Should().NotThrow("循环依赖不应导致栈溢出或死锁");
        }

        #endregion

        #region Dynamic Import (REQ-DYN-003)

        [Fact]
        public async Task DynamicImport_ValidModule_ShouldResolve()
        {
            File.WriteAllText(Path.Combine(_tempRoot, "dynamic-target.mjs"), "export const loaded = true;");

            var options = ModuleRuntimeOptions.CreateSecureDefaults(_tempRoot);
            var resolver = new ModuleResolver(options);
            var cache = new ModuleCache();
            var loader = new ModuleLoader(resolver, cache, options);

            var record = await loader.DynamicImportAsync(
                "./dynamic-target.mjs",
                Path.Combine(_tempRoot, "entry.mjs"),
                PassthroughExecutor);

            record.State.Should().Be(ModuleRecordState.Loaded);
        }

        [Fact]
        public async Task DynamicImport_MissingModule_ShouldThrowWithStructuredError()
        {
            var options = ModuleRuntimeOptions.CreateSecureDefaults(_tempRoot);
            var resolver = new ModuleResolver(options);
            var cache = new ModuleCache();
            var loader = new ModuleLoader(resolver, cache, options);

            Func<Task> act = async () => await loader.DynamicImportAsync(
                "./nonexistent.mjs",
                Path.Combine(_tempRoot, "entry.mjs"),
                PassthroughExecutor);

            await act.Should().ThrowAsync<Exception>("动态导入不存在的模块应失败");
        }

        [Fact]
        public async Task DynamicImport_ShouldReuseCache()
        {
            File.WriteAllText(Path.Combine(_tempRoot, "shared.mjs"), "export const v = 42;");
            var entryPath = Path.Combine(_tempRoot, "dyn-entry.mjs");
            File.WriteAllText(entryPath, "import { v } from './shared.mjs';\nexport const x = v;");

            var options = ModuleRuntimeOptions.CreateSecureDefaults(_tempRoot);
            var resolver = new ModuleResolver(options);
            var cache = new ModuleCache();
            var loader = new ModuleLoader(resolver, cache, options);

            loader.LoadModuleGraph(entryPath, PassthroughExecutor);

            // 动态导入同一个已加载模块应命中缓存
            var record = await loader.DynamicImportAsync(
                "./shared.mjs",
                entryPath,
                PassthroughExecutor);

            record.State.Should().Be(ModuleRecordState.Loaded);
            var stats = cache.GetStatistics();
            stats.HitCount.Should().BeGreaterThan(0, "动态导入应命中已有缓存");
        }

        #endregion

        #region 安全集成 (REQ-SEC-006)

        [Fact]
        public void LoadModuleGraph_WithSecurityPolicy_DeniesOutOfBoundary()
        {
            var subDir = Path.Combine(_tempRoot, "safe");
            Directory.CreateDirectory(subDir);
            var outsideFile = Path.Combine(_tempRoot, "outside.mjs");
            File.WriteAllText(outsideFile, "export const secret = true;");

            File.WriteAllText(Path.Combine(subDir, "entry.mjs"), "import { secret } from '../outside.mjs';\nexport const x = secret;");

            var options = ModuleRuntimeOptions.CreateSecureDefaults(subDir);
            var policy = new ImportSecurityPolicy(options);
            var resolver = new ModuleResolver(options, securityPolicy: policy);
            var cache = new ModuleCache();
            var loader = new ModuleLoader(resolver, cache, options, policy);

            var act = () => loader.LoadModuleGraph(Path.Combine(subDir, "entry.mjs"), PassthroughExecutor);
            act.Should().Throw<Exception>("安全策略应拒绝越界导入");
        }

        #endregion

        #region 诊断事件集成 (REQ-PERF-007)

        [Fact]
        public void LoadModuleGraph_DebugMode_ShouldCollectDiagnostics()
        {
            File.WriteAllText(Path.Combine(_tempRoot, "diag-entry.mjs"), "export const v = 1;");

            var options = ModuleRuntimeOptions.CreateSecureDefaults(_tempRoot);
            options.EnableDebugLogs = true;
            options.EnableStructuredDebugEvents = true;

            var collector = new ModuleDiagnosticCollector(enabled: true);
            var resolver = new ModuleResolver(options);
            var cache = new ModuleCache();
            var loader = new ModuleLoader(resolver, cache, options, diagnosticCollector: collector);

            loader.LoadModuleGraph(Path.Combine(_tempRoot, "diag-entry.mjs"), PassthroughExecutor);

            collector.Events.Should().NotBeEmpty("debug 模式应产出诊断事件");

            var summary = collector.GetSummary();
            _output.WriteLine($"诊断事件总数: {summary.TotalEvents}");
            _output.WriteLine(collector.ToReadableText());
        }

        #endregion

        #region Interop 集成 (REQ-CJS-002)

        [Fact]
        public void LoadModuleGraph_MixedEsmCjs_ShouldInterop()
        {
            File.WriteAllText(Path.Combine(_tempRoot, "esm-entry.mjs"), "import cjs from './lib.cjs';\nexport const val = cjs;");
            File.WriteAllText(Path.Combine(_tempRoot, "lib.cjs"), "module.exports = { answer: 42 };");

            var options = ModuleRuntimeOptions.CreateSecureDefaults(_tempRoot);
            var resolver = new ModuleResolver(options);
            var cache = new ModuleCache();
            var loader = new ModuleLoader(resolver, cache, options);

            var record = loader.LoadModuleGraph(Path.Combine(_tempRoot, "esm-entry.mjs"), PassthroughExecutor);
            record.State.Should().Be(ModuleRecordState.Loaded);
        }

        #endregion
    }
}

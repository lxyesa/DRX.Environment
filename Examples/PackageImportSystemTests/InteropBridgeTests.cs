using System;
using System.Collections.Generic;
using Drx.Sdk.Shared.JavaScript.Abstractions;
using Drx.Sdk.Shared.JavaScript.Engine;
using FluentAssertions;
using Xunit;

namespace PackageImportSystemTests
{
    /// <summary>
    /// InteropBridge 单元测试：覆盖 REQ-CJS-002（ESM↔CJS 互操作）。
    /// 至少 10 个用例：默认导出、命名导出、循环依赖场景、错误场景。
    /// </summary>
    public class InteropBridgeTests
    {
        private InteropBridge CreateBridge(bool enableDebug = false)
        {
            var options = new ModuleRuntimeOptions
            {
                ProjectRoot = TestHelper.FixturesRoot,
                EnableDebugLogs = enableDebug,
                EnableStructuredDebugEvents = enableDebug
            };
            options.ValidateAndNormalize();
            return new InteropBridge(options);
        }

        #region CJS → ESM Namespace 包装

        [Fact]
        public void WrapCjsAsEsm_ObjectExports_ShouldHaveDefaultAndNamed()
        {
            var bridge = CreateBridge();
            var cjsExports = new Dictionary<string, object?>
            {
                ["foo"] = "bar",
                ["baz"] = 42
            };

            var ns = bridge.WrapCjsAsEsmNamespace(cjsExports, null, "/test.cjs");
            ns.Should().ContainKey("default", "CJS → ESM 必须包含 default");
            ns.Should().ContainKey("foo");
            ns.Should().ContainKey("baz");
        }

        [Fact]
        public void WrapCjsAsEsm_FunctionExports_DefaultShouldBeFunction()
        {
            var bridge = CreateBridge();
            Func<int, int> doubleFunc = x => x * 2;

            var ns = bridge.WrapCjsAsEsmNamespace(doubleFunc, null, "/fn.cjs");
            ns.Should().ContainKey("default");
            ns["default"].Should().BeSameAs(doubleFunc);
        }

        [Fact]
        public void WrapCjsAsEsm_NullExports_ShouldHaveNullDefault()
        {
            var bridge = CreateBridge();
            var ns = bridge.WrapCjsAsEsmNamespace(null, null, "/null.cjs");
            ns.Should().ContainKey("default");
            ns["default"].Should().BeNull();
        }

        [Fact]
        public void WrapCjsAsEsm_PrimitiveExports_DefaultShouldBePrimitive()
        {
            var bridge = CreateBridge();
            var ns = bridge.WrapCjsAsEsmNamespace("hello", null, "/primitive.cjs");
            ns.Should().ContainKey("default");
            ns["default"].Should().Be("hello");
        }

        [Fact]
        public void WrapCjsAsEsm_WithSourceStaticAnalysis_ShouldExtractNamedExports()
        {
            var bridge = CreateBridge();
            var cjsExports = new Dictionary<string, object?>
            {
                ["helperFn"] = "fn-value"
            };
            var source = "exports.helperFn = function() {};";

            var ns = bridge.WrapCjsAsEsmNamespace(cjsExports, source, "/analyzed.cjs");
            ns.Should().ContainKey("helperFn", "静态分析应提取 exports.helperFn");
        }

        #endregion

        #region ESM → CJS Require 包装

        [Fact]
        public void WrapEsmForCjsRequire_ShouldReturnNamespaceObject()
        {
            var bridge = CreateBridge();
            var esmNs = new Dictionary<string, object?>
            {
                ["default"] = new { name = "test" },
                ["VERSION"] = "1.0"
            };

            var wrapped = bridge.WrapEsmForCjsRequire(esmNs, "/esm.mjs");
            wrapped.Should().NotBeNull();
            wrapped.Should().ContainKey("default");
        }

        [Fact]
        public void WrapEsmForCjsRequire_NullNamespace_ShouldThrowInteropException()
        {
            var bridge = CreateBridge();
            var act = () => bridge.WrapEsmForCjsRequire(null, "/null-esm.mjs");
            act.Should().Throw<InteropException>("null namespace 不隐式吞错，应抛出 InteropException");
        }

        #endregion

        #region 方向分类 (通过 ResolveInterop 间接验证)

        [Fact]
        public void ResolveInterop_EsmImportsCjs_DirectionCorrect()
        {
            var bridge = CreateBridge();
            var record = new ModuleRecord("k", "/m.cjs", ModuleKind.Cjs);
            record.MarkLoaded(null, new Dictionary<string, object?>(), Array.Empty<string>(), TimeSpan.Zero);
            bridge.ResolveInterop(ModuleKind.Esm, record, null).Direction.Should().Be(InteropDirection.EsmImportsCjs);
        }

        [Fact]
        public void ResolveInterop_CjsRequiresEsm_DirectionCorrect()
        {
            var bridge = CreateBridge();
            var record = new ModuleRecord("k", "/m.mjs", ModuleKind.Esm);
            record.MarkLoaded(null, new Dictionary<string, object?>(), Array.Empty<string>(), TimeSpan.Zero);
            bridge.ResolveInterop(ModuleKind.Cjs, record, null).Direction.Should().Be(InteropDirection.CjsRequiresEsm);
        }

        [Fact]
        public void ResolveInterop_SameKind_Esm_DirectionCorrect()
        {
            var bridge = CreateBridge();
            var record = new ModuleRecord("k", "/m.mjs", ModuleKind.Esm);
            record.MarkLoaded(null, new Dictionary<string, object?>(), Array.Empty<string>(), TimeSpan.Zero);
            bridge.ResolveInterop(ModuleKind.Esm, record, null).Direction.Should().Be(InteropDirection.SameKind);
        }

        [Fact]
        public void ResolveInterop_JsonImport_DirectionCorrect()
        {
            var bridge = CreateBridge();
            var record = new ModuleRecord("k", "/m.json", ModuleKind.Json);
            record.MarkLoaded(null, new Dictionary<string, object?>(), Array.Empty<string>(), TimeSpan.Zero);
            bridge.ResolveInterop(ModuleKind.Esm, record, null).Direction.Should().Be(InteropDirection.JsonImport);
        }

        [Fact]
        public void ResolveInterop_BuiltinImport_DirectionCorrect()
        {
            var bridge = CreateBridge();
            var record = new ModuleRecord("k", "/sdk.js", ModuleKind.Builtin);
            record.MarkLoaded(null, new Dictionary<string, object?>(), Array.Empty<string>(), TimeSpan.Zero);
            bridge.ResolveInterop(ModuleKind.Esm, record, null).Direction.Should().Be(InteropDirection.BuiltinImport);
        }

        #endregion

        #region ResolveInterop 集成

        [Fact]
        public void ResolveInterop_EsmImportsCjs_ShouldApplyWrapping()
        {
            var bridge = CreateBridge();
            var cjsRecord = new ModuleRecord("cjs-key", "/cjs.js", ModuleKind.Cjs);
            cjsRecord.MarkLoaded(
                new Dictionary<string, object?> { ["value"] = 1 },
                new Dictionary<string, object?> { ["value"] = 1 },
                Array.Empty<string>(),
                TimeSpan.FromMilliseconds(5));

            var result = bridge.ResolveInterop(ModuleKind.Esm, cjsRecord, null);
            result.Direction.Should().Be(InteropDirection.EsmImportsCjs);
            result.Applied.Should().BeTrue();
            result.Exports.Should().ContainKey("default");
        }

        [Fact]
        public void ResolveInterop_SameKind_ShouldNotApplyWrapping()
        {
            var bridge = CreateBridge();
            var esmRecord = new ModuleRecord("esm-key", "/esm.mjs", ModuleKind.Esm);
            esmRecord.MarkLoaded(
                new Dictionary<string, object?> { ["func"] = "v" },
                new Dictionary<string, object?> { ["func"] = "v" },
                Array.Empty<string>(),
                TimeSpan.FromMilliseconds(5));

            var result = bridge.ResolveInterop(ModuleKind.Esm, esmRecord, null);
            result.Direction.Should().Be(InteropDirection.SameKind);
            result.Applied.Should().BeFalse();
        }

        #endregion

        #region 诊断事件 (Debug 模式)

        [Fact]
        public void DiagnosticEvents_DebugOff_ShouldBeEmpty()
        {
            var bridge = CreateBridge(enableDebug: false);
            var cjsRecord = new ModuleRecord("key", "/m.cjs", ModuleKind.Cjs);
            cjsRecord.MarkLoaded(null, new Dictionary<string, object?>(), Array.Empty<string>(), TimeSpan.Zero);

            bridge.ResolveInterop(ModuleKind.Esm, cjsRecord, null);
            bridge.DiagnosticEvents.Should().BeEmpty("非 debug 模式不应收集诊断事件");
        }

        [Fact]
        public void DiagnosticEvents_DebugOn_ShouldCollectEvents()
        {
            var bridge = CreateBridge(enableDebug: true);
            var cjsRecord = new ModuleRecord("key", "/m.cjs", ModuleKind.Cjs);
            cjsRecord.MarkLoaded(null, new Dictionary<string, object?>(), Array.Empty<string>(), TimeSpan.Zero);

            bridge.ResolveInterop(ModuleKind.Esm, cjsRecord, null);
            bridge.DiagnosticEvents.Should().NotBeEmpty("debug 模式应收集诊断事件");
        }

        #endregion
    }
}

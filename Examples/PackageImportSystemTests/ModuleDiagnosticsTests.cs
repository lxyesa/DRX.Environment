using System;
using Drx.Sdk.Shared.JavaScript.Engine;
using FluentAssertions;
using Xunit;

namespace PackageImportSystemTests
{
    /// <summary>
    /// ModuleDiagnostics 测试：覆盖 REQ-PERF-007（可观测性、--debug 输出）。
    /// </summary>
    public class ModuleDiagnosticsTests
    {
        #region ModuleDiagnosticCollector

        [Fact]
        public void Collector_Disabled_ShouldNotCollectEvents()
        {
            var collector = new ModuleDiagnosticCollector(enabled: false);
            collector.Emit("resolve.start", DiagnosticCategory.Resolve, DiagnosticSeverity.Debug, "mod-key");

            collector.Events.Should().BeEmpty("disabled collector 不应收集事件");
        }

        [Fact]
        public void Collector_Enabled_ShouldCollectEvents()
        {
            var collector = new ModuleDiagnosticCollector(enabled: true);
            collector.EmitResolve("resolve.start", "my-module");
            collector.EmitLoad("load.start", "my-module");
            collector.EmitCache("cache.hit", "my-module");
            collector.EmitSecurity("security.check.pass", "my-module");
            collector.EmitInterop("interop.wrap", "my-module");
            collector.EmitDynamic("dynamic.import.start", "my-module");

            collector.Events.Should().HaveCount(6);
        }

        [Fact]
        public void Collector_EmitError_ShouldRecordErrorSeverity()
        {
            var collector = new ModuleDiagnosticCollector(enabled: true);
            collector.EmitError("resolve.fail", DiagnosticCategory.Resolve, "bad-module");

            collector.Events.Should().HaveCount(1);
            collector.Events[0].Severity.Should().Be(DiagnosticSeverity.Error);
        }

        [Fact]
        public void Collector_EmitWarning_ShouldRecordWarningSeverity()
        {
            var collector = new ModuleDiagnosticCollector(enabled: true);
            collector.EmitWarning("cache.evict", DiagnosticCategory.Cache, "evicted-key");

            collector.Events[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        }

        #endregion

        #region 输出格式 (JSONL + 可读文本)

        [Fact]
        public void ToJsonLines_ShouldProduceValidJson()
        {
            var collector = new ModuleDiagnosticCollector(enabled: true);
            collector.EmitResolve("resolve.start", "test-mod");
            collector.EmitResolve("resolve.end", "test-mod");

            var jsonl = collector.ToJsonLines();
            jsonl.Should().NotBeNullOrWhiteSpace();
            // 每行应是独立 JSON
            var lines = jsonl.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            lines.Should().HaveCount(2);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                trimmed.Should().StartWith("{");
                trimmed.Should().EndWith("}");
            }
        }

        [Fact]
        public void ToReadableText_ShouldBeHumanFriendly()
        {
            var collector = new ModuleDiagnosticCollector(enabled: true);
            collector.EmitResolve("resolve.start", "my-mod", new { path = "/foo.js" });

            var text = collector.ToReadableText();
            text.Should().Contain("resolve.start");
            text.Should().Contain("my-mod");
        }

        #endregion

        #region 摘要统计

        [Fact]
        public void GetSummary_ShouldReportByCategory()
        {
            var collector = new ModuleDiagnosticCollector(enabled: true);
            collector.EmitResolve("r1", "m1");
            collector.EmitResolve("r2", "m2");
            collector.EmitLoad("l1", "m1");
            collector.EmitCache("c1", "m1");
            collector.EmitSecurity("s1", "m1");

            var summary = collector.GetSummary();
            summary.TotalEvents.Should().Be(5);
            summary.ByCategory.Should().ContainKey(DiagnosticCategory.Resolve);
            summary.ByCategory[DiagnosticCategory.Resolve].Should().Be(2);
        }

        #endregion

        #region ModuleDiagnosticEvent

        [Fact]
        public void DiagnosticEvent_ToJsonLine_ShouldContainAllFields()
        {
            var evt = new ModuleDiagnosticEvent(
                "cache.hit",
                DiagnosticCategory.Cache,
                DiagnosticSeverity.Debug,
                "mod-key",
                new { hits = 5 });

            var json = evt.ToJsonLine();
            json.Should().Contain("cache.hit");
            json.Should().Contain("mod-key");
            json.Should().Contain("Cache");
        }

        [Fact]
        public void DiagnosticEvent_ToReadableString_ShouldContainCategory()
        {
            var evt = new ModuleDiagnosticEvent(
                "security.check.pass",
                DiagnosticCategory.Security,
                DiagnosticSeverity.Info,
                "/safe.js",
                null);

            var text = evt.ToReadableString();
            text.Should().Contain("[Security]");
            text.Should().Contain("security.check.pass");
        }

        #endregion

        #region 零开销原则

        [Fact]
        public void Collector_Disabled_ClearAndSummary_ShouldNotThrow()
        {
            var collector = new ModuleDiagnosticCollector(enabled: false);
            collector.EmitResolve("evt", "k");
            collector.Clear();
            var summary = collector.GetSummary();
            summary.TotalEvents.Should().Be(0);
        }

        #endregion

        #region ModuleErrorCodes 常量

        [Fact]
        public void ErrorCodes_ShouldHaveExpectedValues()
        {
            ModuleErrorCodes.ResEmptySpecifier.Should().Be("PC_RES_000");
            ModuleErrorCodes.ResPathNotFound.Should().Be("PC_RES_001");
            ModuleErrorCodes.LoadEntryFailed.Should().Be("PC_LOAD_001");
        }

        #endregion
    }
}

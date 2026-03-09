using System;
using System.Collections.Generic;
using Drx.Sdk.Shared.JavaScript.Engine;
using FluentAssertions;
using Xunit;

namespace PackageImportSystemTests
{
    /// <summary>
    /// ModuleRecord 状态机测试：Loading → Loaded / Failed，非法转换场景。
    /// </summary>
    public class ModuleRecordTests
    {
        [Fact]
        public void NewModuleRecord_ShouldBeLoading()
        {
            var record = new ModuleRecord("k", "/m.js", ModuleKind.Esm);
            record.State.Should().Be(ModuleRecordState.Loading);
            record.Namespace.Should().BeNull();
            record.Exports.Should().BeNull();
            record.Error.Should().BeNull();
        }

        [Fact]
        public void MarkLoaded_FromLoading_ShouldTransition()
        {
            var record = new ModuleRecord("k", "/m.js", ModuleKind.Esm);
            var exports = new Dictionary<string, object?> { ["default"] = "v" };

            record.MarkLoaded("ns", exports, new[] { "dep1" }, TimeSpan.FromMilliseconds(10));

            record.State.Should().Be(ModuleRecordState.Loaded);
            record.Namespace.Should().Be("ns");
            record.Exports.Should().ContainKey("default");
            record.Dependencies.Should().HaveCount(1);
            record.LoadDuration.Should().NotBeNull();
        }

        [Fact]
        public void MarkFailed_FromLoading_ShouldTransition()
        {
            var record = new ModuleRecord("k", "/m.js", ModuleKind.Cjs);
            var error = new ModuleLoadException("PC_LOAD_001", "/m.js", "load", "test error");

            record.MarkFailed(error, TimeSpan.FromMilliseconds(5));

            record.State.Should().Be(ModuleRecordState.Failed);
            record.Error.Should().NotBeNull();
            record.Error!.Code.Should().Be("PC_LOAD_001");
        }

        [Fact]
        public void MarkLoaded_FromLoaded_ShouldThrow()
        {
            var record = new ModuleRecord("k", "/m.js", ModuleKind.Esm);
            record.MarkLoaded(null, new Dictionary<string, object?>(), Array.Empty<string>(), TimeSpan.Zero);

            var act = () => record.MarkLoaded(null, new Dictionary<string, object?>(), Array.Empty<string>(), TimeSpan.Zero);
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void MarkFailed_FromLoaded_ShouldThrow()
        {
            var record = new ModuleRecord("k", "/m.js", ModuleKind.Esm);
            record.MarkLoaded(null, new Dictionary<string, object?>(), Array.Empty<string>(), TimeSpan.Zero);

            var error = new ModuleLoadException("PC_LOAD_001", "/m.js", "load", "test");
            var act = () => record.MarkFailed(error, TimeSpan.Zero);
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void MarkLoaded_FromFailed_ShouldThrow()
        {
            var record = new ModuleRecord("k", "/m.js", ModuleKind.Esm);
            record.MarkFailed(new ModuleLoadException("PC_LOAD_001", "/m.js", "load", "err"), TimeSpan.Zero);

            var act = () => record.MarkLoaded(null, new Dictionary<string, object?>(), Array.Empty<string>(), TimeSpan.Zero);
            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void ToDiagnostic_ShouldReturnStructuredInfo()
        {
            var record = new ModuleRecord("k", "/m.js", ModuleKind.Esm);
            record.MarkLoaded(null, new Dictionary<string, object?>(), Array.Empty<string>(), TimeSpan.FromMilliseconds(10));

            var diag = record.ToDiagnostic();
            diag.Should().NotBeNull();
        }

        [Theory]
        [InlineData(ModuleKind.Esm)]
        [InlineData(ModuleKind.Cjs)]
        [InlineData(ModuleKind.Json)]
        [InlineData(ModuleKind.Builtin)]
        public void ModuleKind_ShouldBePreserved(ModuleKind kind)
        {
            var record = new ModuleRecord("k", "/m", kind);
            record.Kind.Should().Be(kind);
        }
    }
}

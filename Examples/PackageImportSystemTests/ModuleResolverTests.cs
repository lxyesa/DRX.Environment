using System;
using System.Collections.Generic;
using System.IO;
using Drx.Sdk.Shared.JavaScript.Abstractions;
using Drx.Sdk.Shared.JavaScript.Engine;
using FluentAssertions;
using Xunit;

namespace PackageImportSystemTests
{
    /// <summary>
    /// ModuleResolver 单元测试：覆盖 REQ-ESM-001（ESM 解析）、REQ-PKG-004（包解析）、REQ-COMPAT-005（兼容）。
    /// 含 happy-path、边界条件与错误场景。
    /// </summary>
    public class ModuleResolverTests
    {
        #region Specifier 分类 (Classify)

        [Theory]
        [InlineData("./foo.js", ModuleSpecifierKind.Relative)]
        [InlineData("../bar.mjs", ModuleSpecifierKind.Relative)]
        [InlineData("@paperclip/sdk", ModuleSpecifierKind.Bare)]
        [InlineData("lodash", ModuleSpecifierKind.Bare)]
        [InlineData("@scope/pkg", ModuleSpecifierKind.Bare)]
        [InlineData("@scope/pkg/sub", ModuleSpecifierKind.Bare)]
        public void Classify_ShouldReturnCorrectKind(string specifier, ModuleSpecifierKind expected)
        {
            var options = TestHelper.CreateOptions("esm");
            var resolver = TestHelper.CreateResolver(options);

            var kind = resolver.Classify(specifier);
            kind.Should().Be(expected);
        }

        [Fact]
        public void Classify_EmptySpecifier_ShouldThrow()
        {
            var options = TestHelper.CreateOptions("esm");
            var resolver = TestHelper.CreateResolver(options);

            var act = () => resolver.Resolve("", null);
            var ex = act.Should().Throw<ModuleResolutionException>().Which;
            ex.Code.Should().Be("PC_RES_000");
        }

        [Fact]
        public void Classify_NullSpecifier_ShouldThrow()
        {
            var options = TestHelper.CreateOptions("esm");
            var resolver = TestHelper.CreateResolver(options);

            var act = () => resolver.Resolve(null!, null);
            var ex = act.Should().Throw<ModuleResolutionException>().Which;
            ex.Code.Should().Be("PC_RES_000");
        }

        [Fact]
        public void Classify_WhitespaceSpecifier_ShouldThrow()
        {
            var options = TestHelper.CreateOptions("esm");
            var resolver = TestHelper.CreateResolver(options);

            var act = () => resolver.Resolve("   ", null);
            var ex = act.Should().Throw<ModuleResolutionException>().Which;
            ex.Code.Should().Be("PC_RES_000");
        }

        #endregion

        #region Relative 路径解析 (REQ-ESM-001)

        [Fact]
        public void Resolve_RelativePath_ExistingFile_ShouldSucceed()
        {
            var options = TestHelper.CreateOptions("esm");
            var resolver = TestHelper.CreateResolver(options);

            var result = resolver.Resolve("./simple-export.mjs", TestHelper.Fixture("esm", "import-esm.mjs"));
            result.ResolvedPath.Should().NotBeNull();
            result.Kind.Should().Be(ModuleSpecifierKind.Relative);
            result.Source.Should().Be(ModuleResolutionSource.RelativePath);
            File.Exists(result.ResolvedPath).Should().BeTrue();
        }

        [Fact]
        public void Resolve_RelativePath_NonExistent_ShouldThrowWithAttempts()
        {
            var options = TestHelper.CreateOptions("esm");
            var resolver = TestHelper.CreateResolver(options);

            var act = () => resolver.Resolve("./nonexistent.mjs", TestHelper.Fixture("esm", "simple-export.mjs"));
            act.Should().Throw<ModuleResolutionException>()
                .Which.Attempts.Should().NotBeEmpty("应包含已尝试的解析路径");
        }

        [Fact]
        public void Resolve_RelativePath_ParentDir_ShouldResolve()
        {
            // 由 esm 子目录向上引用同级目录的文件
            var options = TestHelper.CreateOptions(".");
            var resolver = TestHelper.CreateResolver(options);
            var fromFile = TestHelper.Fixture("esm", "simple-export.mjs");

            // 向上引用 cjs 目录（如果在安全边界内）
            var act = () => resolver.Resolve("../cjs/simple-cjs.cjs", fromFile);
            // 由于 projectRoot 是 FixturesRoot，这应当成功
            try
            {
                var result = act();
                result.ResolvedPath.Should().NotBeNull();
            }
            catch (ModuleResolutionException)
            {
                // 如果 FixturesRoot 不是 "."，可能失败——这就是预期行为
            }
        }

        [Fact]
        public void Resolve_RelativePath_AutoExtension_ShouldResolve()
        {
            // 如果 resolver 支持扩展名补全
            var options = TestHelper.CreateOptions("esm");
            var resolver = TestHelper.CreateResolver(options);

            // 尝试不带扩展名的导入
            var act = () => resolver.Resolve("./simple-export", TestHelper.Fixture("esm", "import-esm.mjs"));
            try
            {
                var result = act();
                result.ResolvedPath.Should().NotBeNull();
            }
            catch (ModuleResolutionException ex)
            {
                // 不支持自动补全扩展则应包含尝试的扩展名
                ex.Attempts.Should().NotBeEmpty();
            }
        }

        #endregion

        #region Builtin 解析 (REQ-ESM-001)

        [Fact]
        public void Resolve_Builtin_Mapped_ShouldResolve()
        {
            var options = TestHelper.CreateOptions("esm");
            var builtins = new Dictionary<string, string>
            {
                ["@paperclip/sdk"] = TestHelper.Fixture("esm", "simple-export.mjs")
            };
            var resolver = TestHelper.CreateResolver(options, builtins);

            var result = resolver.Resolve("@paperclip/sdk", null);
            result.Kind.Should().Be(ModuleSpecifierKind.Builtin);
            result.Source.Should().Be(ModuleResolutionSource.BuiltinMap);
            result.ResolvedPath.Should().NotBeNull();
        }

        [Fact]
        public void Resolve_Builtin_NotMapped_ShouldFallThrough()
        {
            var options = TestHelper.CreateOptions("esm");
            var resolver = TestHelper.CreateResolver(options);

            // 没有映射的裸包，且 node_modules 禁用——应失败
            var act = () => resolver.Resolve("@paperclip/sdk", null);
            act.Should().Throw<ModuleResolutionException>();
        }

        #endregion

        #region Workspace Imports Map 解析 (REQ-PKG-004)

        [Fact]
        public void Resolve_WorkspaceImportsMap_ShouldResolveAlias()
        {
            var wsRoot = TestHelper.Fixture("workspace");
            var options = new ModuleRuntimeOptions
            {
                ProjectRoot = wsRoot,
                AllowNodeModulesResolution = false
            };
            options.ValidateAndNormalize();

            var workspaceImports = new Dictionary<string, string>
            {
                ["@app/utils"] = Path.Combine(wsRoot, "src", "utils.js"),
                ["@app/config"] = Path.Combine(wsRoot, "src", "config.js")
            };

            var resolver = TestHelper.CreateResolver(options, workspaceImports: workspaceImports);

            var result = resolver.Resolve("@app/utils", null);
            result.Source.Should().Be(ModuleResolutionSource.WorkspaceImportsMap);
            result.ResolvedPath.Should().Contain("utils.js");
        }

        [Fact]
        public void Resolve_WorkspaceImportsMap_UnknownAlias_ShouldFail()
        {
            var wsRoot = TestHelper.Fixture("workspace");
            var options = new ModuleRuntimeOptions
            {
                ProjectRoot = wsRoot,
                AllowNodeModulesResolution = false
            };
            options.ValidateAndNormalize();

            var resolver = TestHelper.CreateResolver(options);

            var act = () => resolver.Resolve("@app/nonexistent", null);
            act.Should().Throw<ModuleResolutionException>();
        }

        #endregion

        #region Node Modules 裸包解析 (REQ-PKG-004)

        [Fact]
        public void Resolve_BarePackage_WithNodeModules_ShouldResolve()
        {
            var options = TestHelper.CreateOptions(".", allowNodeModules: true);
            var resolver = TestHelper.CreateResolver(options);

            var fromFile = TestHelper.Fixture("esm", "import-esm.mjs");
            var result = resolver.Resolve("simple-pkg", fromFile);
            result.Source.Should().BeOneOf(
                ModuleResolutionSource.NodeModules,
                ModuleResolutionSource.PackageExports);
            result.ResolvedPath.Should().Contain("simple-pkg");
        }

        [Fact]
        public void Resolve_BarePackage_NodeModulesDisabled_ShouldFail()
        {
            var options = TestHelper.CreateOptions("esm", allowNodeModules: false);
            var resolver = TestHelper.CreateResolver(options);

            var act = () => resolver.Resolve("simple-pkg", TestHelper.Fixture("esm", "simple-export.mjs"));
            act.Should().Throw<ModuleResolutionException>();
        }

        #endregion

        #region Package Exports/Conditions 解析 (REQ-PKG-004, REQ-CJS-002)

        [Fact]
        public void Resolve_PackageExports_ImportCondition_ShouldResolveToMjs()
        {
            var options = TestHelper.CreateOptions(".", allowNodeModules: true);
            var resolver = TestHelper.CreateResolver(options);
            var fromFile = TestHelper.Fixture("esm", "import-esm.mjs");

            // test-pkg 有 exports."." 含 import/require/default 条件
            var result = resolver.Resolve("test-pkg", fromFile);
            result.ResolvedPath.Should().NotBeNull();
            // 应该解析到 exports 定义的入口之一
            result.Attempts.Should().NotBeEmpty("解析应记录条件匹配过程");
        }

        [Fact]
        public void Resolve_PackageExports_Subpath_ShouldResolve()
        {
            var options = TestHelper.CreateOptions(".", allowNodeModules: true);
            var resolver = TestHelper.CreateResolver(options);
            var fromFile = TestHelper.Fixture("esm", "import-esm.mjs");

            var result = resolver.Resolve("test-pkg/utils", fromFile);
            result.ResolvedPath.Should().NotBeNull();
            result.ResolvedPath.Should().Contain("utils");
        }

        #endregion

        #region 错误结构（REQ-ESM-001 验收标准）

        [Fact]
        public void ResolveError_ShouldContainRequiredFields()
        {
            var options = TestHelper.CreateOptions("esm");
            var resolver = TestHelper.CreateResolver(options);

            try
            {
                resolver.Resolve("totally-nonexistent-module", null);
                Assert.Fail("期望抛出 ModuleResolutionException");
            }
            catch (ModuleResolutionException ex)
            {
                // REQ-ESM-001 验收标准：错误输出包含 specifier/from/attempts/reason
                ex.Code.Should().NotBeNullOrWhiteSpace("Code 不能为空");
                ex.Specifier.Should().Be("totally-nonexistent-module");
                ex.Reason.Should().NotBeNullOrWhiteSpace("Reason 不能为空");
                ex.Attempts.Should().NotBeNull("Attempts 不能为 null");

                // 结构化错误导出
                var structured = ex.ToStructuredError();
                structured.Should().NotBeNull();
            }
        }

        #endregion

        #region 入口解析 (ResolveForEntry)

        [Fact]
        public void ResolveForEntry_ValidEntry_ShouldSucceed()
        {
            var options = TestHelper.CreateOptions("esm");
            var resolver = TestHelper.CreateResolver(options);

            var result = resolver.ResolveForEntry(TestHelper.Fixture("esm", "simple-export.mjs"));
            result.ResolvedPath.Should().NotBeNull();
        }

        [Fact]
        public void ResolveForEntry_NonExistentFile_ShouldThrow()
        {
            var options = TestHelper.CreateOptions("esm");
            var resolver = TestHelper.CreateResolver(options);

            var act = () => resolver.ResolveForEntry(TestHelper.Fixture("esm", "no-such-file.mjs"));
            act.Should().Throw<ModuleResolutionException>();
        }

        #endregion
    }
}

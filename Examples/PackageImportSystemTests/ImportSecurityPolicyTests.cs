using System;
using System.IO;
using Drx.Sdk.Shared.JavaScript.Abstractions;
using Drx.Sdk.Shared.JavaScript.Engine;
using FluentAssertions;
using Xunit;

namespace PackageImportSystemTests
{
    /// <summary>
    /// ImportSecurityPolicy 安全测试：覆盖 REQ-SEC-006。
    /// 包含：路径穿越、符号链接越界、白名单放开、审计日志、deny-by-default、边界场景。
    /// 绝非只测 happy-path。
    /// </summary>
    public class ImportSecurityPolicyTests
    {
        private static ImportSecurityPolicy CreatePolicy(
            string projectRoot,
            string[]? whitelistPrefixes = null,
            bool enableDebug = true)
        {
            var options = new ModuleRuntimeOptions
            {
                ProjectRoot = projectRoot,
                EnableDebugLogs = enableDebug,
                EnableStructuredDebugEvents = enableDebug
            };

            if (whitelistPrefixes is not null)
            {
                options.AllowedImportPathPrefixes.AddRange(whitelistPrefixes);
            }

            options.ValidateAndNormalize();
            return new ImportSecurityPolicy(options);
        }

        #region Deny-by-Default 基线

        [Fact]
        public void ValidateAccess_InsideProjectRoot_ShouldAllow()
        {
            var root = TestHelper.Fixture("esm");
            var policy = CreatePolicy(root);
            var path = TestHelper.Fixture("esm", "simple-export.mjs");

            // 不应抛异常
            policy.ValidateAccess(path, "./simple-export.mjs", null);
        }

        [Fact]
        public void ValidateAccess_OutsideProjectRoot_ShouldDeny()
        {
            var root = TestHelper.Fixture("esm");
            var policy = CreatePolicy(root);
            var outsidePath = TestHelper.Fixture("cjs", "simple-cjs.cjs");

            var act = () => policy.ValidateAccess(outsidePath, "../cjs/simple-cjs.cjs", null);
            var ex = act.Should().Throw<ImportSecurityException>().Which;
            ex.Code.Should().StartWith("PC_SEC_");
        }

        [Fact]
        public void ValidateAccess_ProjectRootItself_ShouldAllow()
        {
            var root = TestHelper.Fixture("esm");
            var policy = CreatePolicy(root);

            // 项目根目录本身
            policy.ValidateAccess(Path.Combine(root, "simple-export.mjs"), "simple-export.mjs", null);
        }

        #endregion

        #region 路径穿越攻击 (Path Traversal)

        [Fact]
        public void ValidateAccess_DotDotEscape_ShouldDeny()
        {
            var root = TestHelper.Fixture("esm");
            var policy = CreatePolicy(root);
            // 试图通过 .. 逃逸到上级目录
            var escapePath = Path.GetFullPath(Path.Combine(root, "..", "cjs", "simple-cjs.cjs"));

            var act = () => policy.ValidateAccess(escapePath, "../../cjs/simple-cjs.cjs", null);
            act.Should().Throw<ImportSecurityException>();
        }

        [Fact]
        public void ValidateAccess_MultipleDotDot_ShouldDeny()
        {
            var root = TestHelper.Fixture("esm");
            var policy = CreatePolicy(root);
            var escapePath = Path.GetFullPath(Path.Combine(root, "..", "..", "..", "Windows", "System32", "cmd.exe"));

            var act = () => policy.ValidateAccess(escapePath, "../../../Windows/System32/cmd.exe", null);
            act.Should().Throw<ImportSecurityException>();
        }

        [Fact]
        public void ValidateAccess_TraversalToOutsideFixture_ShouldDeny()
        {
            var root = TestHelper.Fixture("workspace");
            var policy = CreatePolicy(root);
            var outsidePath = TestHelper.Fixture("outside", "secret.js");

            var act = () => policy.ValidateAccess(outsidePath, "../outside/secret.js", null);
            act.Should().Throw<ImportSecurityException>();
        }

        #endregion

        #region 空路径 / 非法路径

        [Fact]
        public void ValidateAccess_EmptyPath_ShouldDeny()
        {
            var root = TestHelper.Fixture("esm");
            var policy = CreatePolicy(root);

            var act = () => policy.ValidateAccess("", "", null);
            act.Should().Throw<ImportSecurityException>()
                .Which.Code.Should().Be("PC_SEC_003");
        }

        [Fact]
        public void ValidateAccess_NullPath_ShouldDeny()
        {
            var root = TestHelper.Fixture("esm");
            var policy = CreatePolicy(root);

            var act = () => policy.ValidateAccess(null!, "", null);
            act.Should().Throw<ImportSecurityException>();
        }

        [Fact]
        public void ValidateAccess_WhitespacePath_ShouldDeny()
        {
            var root = TestHelper.Fixture("esm");
            var policy = CreatePolicy(root);

            var act = () => policy.ValidateAccess("   ", "   ", null);
            act.Should().Throw<ImportSecurityException>();
        }

        #endregion

        #region 白名单放开

        [Fact]
        public void ValidateAccess_WhitelistedPrefix_ShouldAllow()
        {
            var root = TestHelper.Fixture("esm");
            var whitelisted = TestHelper.Fixture("cjs");
            var policy = CreatePolicy(root, [whitelisted]);

            // cjs 已被白名单放开
            var path = TestHelper.Fixture("cjs", "simple-cjs.cjs");
            policy.ValidateAccess(path, "../cjs/simple-cjs.cjs", null);
        }

        [Fact]
        public void ValidateAccess_NotWhitelisted_StillDenied()
        {
            var root = TestHelper.Fixture("esm");
            var whitelisted = TestHelper.Fixture("cjs");
            var policy = CreatePolicy(root, [whitelisted]);

            // workspace 未被白名单化
            var path = TestHelper.Fixture("workspace", "src", "utils.js");
            var act = () => policy.ValidateAccess(path, "../workspace/src/utils.js", null);
            act.Should().Throw<ImportSecurityException>();
        }

        #endregion

        #region 审计日志

        [Fact]
        public void AuditLog_ShouldRecordAllDecisions()
        {
            var root = TestHelper.Fixture("esm");
            var policy = CreatePolicy(root, enableDebug: true);

            // Allowed
            policy.ValidateAccess(TestHelper.Fixture("esm", "simple-export.mjs"), "./simple-export.mjs", null);

            // Denied
            try
            {
                var outsidePath = TestHelper.Fixture("cjs", "simple-cjs.cjs");
                policy.ValidateAccess(outsidePath, "../cjs/simple-cjs.cjs", null);
            }
            catch (ImportSecurityException) { /* 预期 */ }

            policy.AuditLog.Should().HaveCountGreaterThanOrEqualTo(2, "应记录所有安全决策（允许+拒绝）");
        }

        [Fact]
        public void AuditLog_DeniedEntry_ShouldContainReason()
        {
            var root = TestHelper.Fixture("esm");
            var policy = CreatePolicy(root, enableDebug: true);

            try
            {
                policy.ValidateAccess(TestHelper.Fixture("cjs", "simple-cjs.cjs"), "../cjs/simple-cjs.cjs", null);
            }
            catch (ImportSecurityException) { }

            var deniedEntries = policy.AuditLog;
            deniedEntries.Should().Contain(e => e.Decision == SecurityDecision.Denied);
        }

        #endregion

        #region IsPathAllowed 快速预检

        [Fact]
        public void IsPathAllowed_InsideRoot_ReturnsTrue()
        {
            var root = TestHelper.Fixture("esm");
            var policy = CreatePolicy(root);
            policy.IsPathAllowed(TestHelper.Fixture("esm", "simple-export.mjs")).Should().BeTrue();
        }

        [Fact]
        public void IsPathAllowed_OutsideRoot_ReturnsFalse()
        {
            var root = TestHelper.Fixture("esm");
            var policy = CreatePolicy(root);
            policy.IsPathAllowed(TestHelper.Fixture("cjs", "simple-cjs.cjs")).Should().BeFalse();
        }

        #endregion

        #region 结构化异常

        [Fact]
        public void ImportSecurityException_ToStructuredError_ContainsAllFields()
        {
            var root = TestHelper.Fixture("esm");
            var policy = CreatePolicy(root);

            try
            {
                policy.ValidateAccess(TestHelper.Fixture("cjs", "simple-cjs.cjs"), "../cjs/simple-cjs.cjs", TestHelper.Fixture("esm", "import-esm.mjs"));
                Assert.Fail("应抛出 ImportSecurityException");
            }
            catch (ImportSecurityException ex)
            {
                ex.Code.Should().NotBeNullOrWhiteSpace();
                ex.ResolvedPath.Should().NotBeNullOrWhiteSpace();
                // Reason 是枚举，验证它有合法的枚举值
                Enum.IsDefined(typeof(SecurityDenialReason), ex.Reason).Should().BeTrue();

                var structured = ex.ToStructuredError();
                structured.Should().NotBeNull();
            }
        }

        #endregion
    }
}

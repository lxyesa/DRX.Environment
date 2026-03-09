using System;
using System.IO;
using Drx.Sdk.Shared.JavaScript.Abstractions;
using Drx.Sdk.Shared.JavaScript.Engine;

namespace PackageImportSystemTests
{
    /// <summary>
    /// 测试基础设施：提供 TestFixtures 路径、常用工厂方法与测试上下文构建。
    /// </summary>
    public static class TestHelper
    {
        /// <summary>
        /// TestFixtures 根目录（运行时输出目录下）。
        /// </summary>
        public static string FixturesRoot => Path.Combine(AppContext.BaseDirectory, "TestFixtures");

        /// <summary>
        /// 获取 fixture 子路径的完整绝对路径。
        /// </summary>
        public static string Fixture(params string[] relativeParts)
        {
            var parts = new string[relativeParts.Length + 1];
            parts[0] = FixturesRoot;
            Array.Copy(relativeParts, 0, parts, 1, relativeParts.Length);
            return Path.GetFullPath(Path.Combine(parts));
        }

        /// <summary>
        /// 创建安全默认 ModuleRuntimeOptions，projectRoot 指向指定 fixture 子目录。
        /// </summary>
        public static ModuleRuntimeOptions CreateOptions(string fixtureSubDir, bool enableDebug = false, bool allowNodeModules = false)
        {
            var root = Fixture(fixtureSubDir);
            var options = new ModuleRuntimeOptions
            {
                ProjectRoot = root,
                EnableDebugLogs = enableDebug,
                EnableStructuredDebugEvents = enableDebug,
                AllowNodeModulesResolution = allowNodeModules
            };
            options.ValidateAndNormalize();
            return options;
        }

        /// <summary>
        /// 创建 ModuleResolver，默认内建映射为空。
        /// </summary>
        public static ModuleResolver CreateResolver(
            ModuleRuntimeOptions options,
            System.Collections.Generic.Dictionary<string, string>? builtins = null,
            System.Collections.Generic.Dictionary<string, string>? workspaceImports = null,
            ImportSecurityPolicy? securityPolicy = null)
        {
            return new ModuleResolver(
                options,
                builtins,
                workspaceImports,
                securityPolicy);
        }

        /// <summary>
        /// 创建默认 ModuleCache。
        /// </summary>
        public static ModuleCache CreateCache() => new ModuleCache();

        /// <summary>
        /// 创建完整的 ModuleLoader 管线（resolver + cache + loader）。
        /// </summary>
        public static (ModuleLoader Loader, ModuleResolver Resolver, ModuleCache Cache) CreateLoaderPipeline(
            ModuleRuntimeOptions options,
            System.Collections.Generic.Dictionary<string, string>? builtins = null,
            System.Collections.Generic.Dictionary<string, string>? workspaceImports = null)
        {
            var policy = new ImportSecurityPolicy(options);
            var resolver = CreateResolver(options, builtins, workspaceImports, policy);
            var cache = CreateCache();
            var diagnostics = new ModuleDiagnosticCollector(options.EnableStructuredDebugEvents);
            var loader = new ModuleLoader(resolver, cache, options, policy, diagnostics);
            return (loader, resolver, cache);
        }
    }
}

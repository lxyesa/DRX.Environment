using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Drx.Sdk.Shared;

namespace Drx.Sdk.Shared.JavaScript
{
    /// <summary>
    /// JavaScript 静态门面 TypeScript 能力（转译与项目脚手架）。
    /// 依赖：Engine 执行能力（Execute/ExecuteFile/RegisterGlobal）、Node 项目中的 typescript.js。
    /// </summary>
    public static partial class JavaScript
    {
        private const string TypeScriptCompilerRelativePath = "node_modules\\typescript\\lib\\typescript.js";
        private const string TypeScriptSourceGlobalName = "__drxTsSource";

        /// <summary>
        /// 转译指定 TypeScript 脚本文件为 JavaScript（不执行）。
        /// </summary>
        /// <param name="scriptPath">TypeScript 文件路径。</param>
        /// <param name="workingDirectory">可选工作目录；为空时使用脚本文件所在目录。</param>
        /// <returns>转译后的 JavaScript 文本（附带 sourceURL）。</returns>
        public static string TranspileTypeScriptFile(string scriptPath, string? workingDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(scriptPath))
                throw new ArgumentException("TypeScript 脚本路径不能为空。", nameof(scriptPath));

            if (!File.Exists(scriptPath))
                throw new FileNotFoundException("找不到 TypeScript 脚本文件。", scriptPath);

            var scriptDirectory = Path.GetDirectoryName(scriptPath);
            var searchRoot = string.IsNullOrWhiteSpace(workingDirectory)
                ? (string.IsNullOrWhiteSpace(scriptDirectory) ? Directory.GetCurrentDirectory() : scriptDirectory)
                : workingDirectory;

            var compilerFile = FindTypeScriptCompiler(searchRoot)
                ?? throw new FileNotFoundException(
                    $"找不到 TypeScript 编译器: {TypeScriptCompilerRelativePath}。请先在项目目录执行 npm install typescript。",
                    TypeScriptCompilerRelativePath);

            ExecuteFile(compilerFile);

            var compilerReadyResult = Execute("typeof globalThis.ts !== 'undefined' && !!globalThis.ts.transpileModule");
            var compilerReady = compilerReadyResult is bool flag && flag;
            if (!compilerReady)
                throw new InvalidOperationException("TypeScript 编译器加载失败，globalThis.ts.transpileModule 不可用。");

            var source = File.ReadAllText(scriptPath, Encoding.UTF8);
            var sourceJson = JsonSerializer.Serialize(source);
            Execute($"globalThis.{TypeScriptSourceGlobalName} = {sourceJson};");

            try
            {
                var transpiledResult = Execute("""
                    (() => {
                      const input = globalThis.__drxTsSource ?? '';
                      const result = globalThis.ts.transpileModule(input, {
                        compilerOptions: {
                          module: globalThis.ts.ModuleKind.None,
                          target: globalThis.ts.ScriptTarget.ES2020,
                          strict: false,
                          sourceMap: false,
                          inlineSourceMap: false
                        },
                        reportDiagnostics: true
                      });

                      if (Array.isArray(result.diagnostics) && result.diagnostics.length > 0) {
                        const errors = result.diagnostics
                          .map(d => globalThis.ts.flattenDiagnosticMessageText(d.messageText, '\n'))
                          .join('\n');
                        if (errors && errors.trim().length > 0) {
                          throw new Error(errors);
                        }
                      }

                      return result.outputText ?? '';
                    })();
                    """);

                var transpiled = transpiledResult?.ToString() ?? string.Empty;
                var sourceUrl = scriptPath.Replace('\\', '/');
                return $"{transpiled}{Environment.NewLine}//# sourceURL={sourceUrl}";
            }
            finally
            {
                Execute($"globalThis.{TypeScriptSourceGlobalName} = '';\n");
            }
        }

        /// <summary>
        /// 为项目目录生成最小 TypeScript 脚手架（tsconfig.json 与 package.json）。
        /// </summary>
        /// <param name="targetDir">项目目录。</param>
        /// <param name="projectName">项目名称。</param>
        public static void EnsureTypeScriptScaffold(string targetDir, string projectName)
        {
            if (string.IsNullOrWhiteSpace(targetDir))
                throw new ArgumentException("目标目录不能为空。", nameof(targetDir));

            if (string.IsNullOrWhiteSpace(projectName))
                throw new ArgumentException("项目名称不能为空。", nameof(projectName));

            var tsconfigPath = Path.Combine(targetDir, "tsconfig.json");
            if (!File.Exists(tsconfigPath))
            {
                var tsconfig = """
                    {
                      "compilerOptions": {
                        "target": "ES2020",
                        "module": "None",
                        "strict": false,
                        "moduleResolution": "node"
                      },
                      "include": ["*.ts", "Models/**/*.d.ts"]
                    }
                    """;

                File.WriteAllText(tsconfigPath, tsconfig);
                Logger.Info($"[TypeScript] 已创建 TypeScript 配置: {tsconfigPath}");
            }

            var packageJsonPath = Path.Combine(targetDir, "package.json");
            if (!File.Exists(packageJsonPath))
            {
                var safeName = projectName.Trim().ToLowerInvariant().Replace(' ', '-');

                var packageJson = $$"""
                    {
                      "name": "{{safeName}}",
                      "private": true,
                      "devDependencies": {
                        "typescript": "^5.8.3"
                      }
                    }
                    """;

                File.WriteAllText(packageJsonPath, packageJson);
                Logger.Info($"[TypeScript] 已创建 package.json: {packageJsonPath}");
                Logger.Info("[TypeScript] 请在项目目录执行 npm install，以便 TypeScript 运行时转译可用。");
            }
        }

        private static string? FindTypeScriptCompiler(string startDirectory)
        {
            var current = new DirectoryInfo(startDirectory);
            while (current != null)
            {
                var candidate = Path.Combine(current.FullName, TypeScriptCompilerRelativePath);
                if (File.Exists(candidate))
                    return candidate;

                current = current.Parent;
            }

            return null;
        }
    }
}

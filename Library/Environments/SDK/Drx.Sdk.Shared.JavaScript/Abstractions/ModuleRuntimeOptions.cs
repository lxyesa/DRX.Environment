using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Drx.Sdk.Shared.JavaScript.Abstractions
{
    /// <summary>
    /// Module Runtime 配置选项，控制安全边界与调试输出。
    /// 默认策略为仅允许项目根目录内导入，白名单需显式声明。
    /// </summary>
    public sealed class ModuleRuntimeOptions
    {
        /// <summary>
        /// 项目根目录。默认使用当前工作目录。
        /// </summary>
        public string ProjectRoot { get; set; } = Directory.GetCurrentDirectory();

        /// <summary>
        /// 允许额外导入的白名单路径前缀（相对路径将按 <see cref="ProjectRoot"/> 解析）。
        /// </summary>
        public List<string> AllowedImportPathPrefixes { get; set; } = new();

        /// <summary>
        /// 是否启用调试日志。
        /// </summary>
        public bool EnableDebugLogs { get; set; } = false;

        /// <summary>
        /// 是否输出结构化调试事件。
        /// </summary>
        public bool EnableStructuredDebugEvents { get; set; } = false;

        /// <summary>
        /// 是否允许 node_modules 裸包解析（默认 false，后续任务可按需放开）。
        /// </summary>
        public bool AllowNodeModulesResolution { get; set; } = false;

        /// <summary>
        /// Workspace 级 imports map（paperclip.json#imports）。
        /// key 为导入别名（specifier），value 为目标文件或目录路径（相对路径按 <see cref="ProjectRoot"/> 解析）。
        /// </summary>
        public Dictionary<string, string> WorkspaceImportsMap { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// 创建安全默认配置：项目根目录内允许，其他路径拒绝。
        /// </summary>
        public static ModuleRuntimeOptions CreateSecureDefaults(string projectRoot)
        {
            var options = new ModuleRuntimeOptions
            {
                ProjectRoot = projectRoot
            };

            options.ValidateAndNormalize();
            return options;
        }

        /// <summary>
        /// 校验并规范化路径配置。
        /// </summary>
        /// <exception cref="InvalidOperationException">当项目根目录或白名单路径非法时抛出。</exception>
        public void ValidateAndNormalize()
        {
            if (string.IsNullOrWhiteSpace(ProjectRoot))
            {
                throw new InvalidOperationException("[Paperclip] ModuleRuntimeOptions.ProjectRoot 不能为空。请设置有效的项目目录。");
            }

            ProjectRoot = NormalizePath(ProjectRoot);
            AllowedImportPathPrefixes ??= new List<string>();
            WorkspaceImportsMap ??= new Dictionary<string, string>(StringComparer.Ordinal);

            var normalized = new List<string>();
            foreach (var prefix in AllowedImportPathPrefixes)
            {
                if (string.IsNullOrWhiteSpace(prefix))
                {
                    continue;
                }

                var candidate = Path.IsPathRooted(prefix)
                    ? prefix
                    : Path.Combine(ProjectRoot, prefix);

                var normalizedPrefix = NormalizePath(candidate);
                normalized.Add(normalizedPrefix);
            }

            AllowedImportPathPrefixes = normalized
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var normalizedImportsMap = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in WorkspaceImportsMap)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
                {
                    continue;
                }

                var normalizedSpecifier = pair.Key.Trim();
                var mappedPathCandidate = pair.Value.Trim();
                var mappedPath = Path.IsPathRooted(mappedPathCandidate)
                    ? mappedPathCandidate
                    : Path.Combine(ProjectRoot, mappedPathCandidate);

                normalizedImportsMap[normalizedSpecifier] = NormalizePath(mappedPath);
            }

            WorkspaceImportsMap = normalizedImportsMap;
        }

        /// <summary>
        /// 判断目标路径是否位于项目根目录或白名单路径下。
        /// </summary>
        public bool IsPathAllowed(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            var normalizedCandidate = NormalizePath(path);
            if (IsSameOrChildPath(normalizedCandidate, ProjectRoot))
            {
                return true;
            }

            foreach (var prefix in AllowedImportPathPrefixes)
            {
                if (IsSameOrChildPath(normalizedCandidate, prefix))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsSameOrChildPath(string candidate, string root)
        {
            if (string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var normalizedRoot = root.EndsWith(Path.DirectorySeparatorChar)
                ? root
                : root + Path.DirectorySeparatorChar;

            return candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string value)
        {
            var fullPath = Path.GetFullPath(value);
            return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
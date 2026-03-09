using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Drx.Sdk.Shared.JavaScript.Abstractions;

namespace Drx.Sdk.Shared.JavaScript.Engine
{
    /// <summary>
    /// 导入安全策略——deny-by-default 的路径安全边界。
    /// 仅允许项目根目录内与显式白名单前缀下的路径加载模块。
    /// 所有路径在校验前必须经过规范化（<see cref="Path.GetFullPath(string)"/>），
    /// 禁止绕过路径规范化步骤，并检测符号链接越界。
    /// </summary>
    public sealed class ImportSecurityPolicy
    {
        private readonly string _normalizedProjectRoot;
        private readonly IReadOnlyList<string> _normalizedAllowedPrefixes;
        private readonly bool _enableAuditLog;
        private readonly List<SecurityAuditEntry> _auditLog = new();
        private readonly ModuleDiagnosticCollector? _diagnosticCollector;

        /// <summary>
        /// 安全审计日志条目。
        /// </summary>
        public IReadOnlyList<SecurityAuditEntry> AuditLog => _auditLog;

        /// <summary>
        /// 项目根目录（已规范化）。
        /// </summary>
        public string ProjectRoot => _normalizedProjectRoot;

        /// <summary>
        /// 初始化安全策略。
        /// </summary>
        /// <param name="options">模块运行时选项（必须已调用 <see cref="ModuleRuntimeOptions.ValidateAndNormalize"/>）。</param>
        /// <param name="diagnosticCollector">统一诊断收集器（可选，debug 模式下注入）。</param>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> 为 null。</exception>
        public ImportSecurityPolicy(ModuleRuntimeOptions options, ModuleDiagnosticCollector? diagnosticCollector = null)
        {
            if (options is null) throw new ArgumentNullException(nameof(options));

            _normalizedProjectRoot = NormalizePath(options.ProjectRoot);
            _normalizedAllowedPrefixes = options.AllowedImportPathPrefixes
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(NormalizePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            _enableAuditLog = options.EnableDebugLogs || options.EnableStructuredDebugEvents;
            _diagnosticCollector = diagnosticCollector;
        }

        /// <summary>
        /// 校验指定路径是否允许加载。deny-by-default：
        /// 仅当路径在项目根内或白名单前缀下时才允许。
        /// 同时检测符号链接越界（实际物理路径也必须在允许范围内）。
        /// </summary>
        /// <param name="resolvedPath">已解析的绝对路径（未规范化亦可，内部会规范化）。</param>
        /// <param name="specifier">原始 specifier（用于审计与错误信息）。</param>
        /// <param name="fromFilePath">发起导入的来源文件（用于审计与错误信息），可为 null。</param>
        /// <exception cref="ImportSecurityException">路径被安全策略拒绝时抛出。</exception>
        public void ValidateAccess(string resolvedPath, string specifier, string? fromFilePath)
        {
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                Deny(SecurityDenialReason.InvalidPath, resolvedPath ?? "", specifier, fromFilePath,
                    "PC_SEC_003", "路径为空或仅包含空白字符。");
                return; // Deny 必然抛异常，此 return 仅满足编译器可达性分析
            }

            var normalizedPath = NormalizePath(resolvedPath);

            // 1. 检测路径穿越序列（规范化后不应再含 ..）
            if (ContainsTraversalSequence(resolvedPath))
            {
                RecordAudit(SecurityDecision.Denied, SecurityDenialReason.PathTraversal,
                    normalizedPath, specifier, fromFilePath);
                // 虽然规范化后不一定有 ..，但原始路径意图可疑，记录后继续正常校验
            }

            // 2. 符号链接越界检测：获取物理路径
            var physicalPath = ResolvePhysicalPath(normalizedPath);

            // 3. deny-by-default：检查规范化路径和物理路径都在允许范围内
            var normalizedAllowed = IsWithinAllowedBoundary(normalizedPath);
            var physicalAllowed = string.Equals(physicalPath, normalizedPath, StringComparison.OrdinalIgnoreCase)
                || IsWithinAllowedBoundary(physicalPath);

            if (!normalizedAllowed)
            {
                Deny(SecurityDenialReason.OutOfBoundary, normalizedPath, specifier, fromFilePath,
                    "PC_SEC_001", "解析到的模块路径超出安全边界。");
            }

            if (!physicalAllowed)
            {
                Deny(SecurityDenialReason.SymlinkEscape, physicalPath, specifier, fromFilePath,
                    "PC_SEC_001", $"符号链接目标路径超出安全边界（物理路径: {physicalPath}）。");
            }

            RecordAudit(SecurityDecision.Allowed, null, normalizedPath, specifier, fromFilePath);
        }

        /// <summary>
        /// 快速判断路径是否在允许边界内（不抛异常版本，供内部或解析器快速预检用）。
        /// 注意：此方法不做符号链接检测，完整校验请用 <see cref="ValidateAccess"/>。
        /// </summary>
        public bool IsPathAllowed(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            return IsWithinAllowedBoundary(NormalizePath(path));
        }

        /// <summary>
        /// 判断规范化路径是否在项目根或白名单前缀内。
        /// </summary>
        private bool IsWithinAllowedBoundary(string normalizedPath)
        {
            if (IsSameOrChildPath(normalizedPath, _normalizedProjectRoot))
            {
                return true;
            }

            foreach (var prefix in _normalizedAllowedPrefixes)
            {
                if (IsSameOrChildPath(normalizedPath, prefix))
                {
                    return true;
                }
            }

            return false;
        }

        private void Deny(
            SecurityDenialReason reason,
            string path,
            string specifier,
            string? fromFilePath,
            string code,
            string message)
        {
            RecordAudit(SecurityDecision.Denied, reason, path, specifier, fromFilePath);

            throw new ImportSecurityException(
                code: code,
                resolvedPath: path,
                specifier: specifier,
                from: fromFilePath,
                reason: reason,
                message: message,
                hint: BuildHint(reason));
        }

        private void RecordAudit(
            SecurityDecision decision,
            SecurityDenialReason? denialReason,
            string path,
            string specifier,
            string? fromFilePath)
        {
            if (!_enableAuditLog) return;

            _auditLog.Add(new SecurityAuditEntry(
                Timestamp: DateTimeOffset.UtcNow,
                Decision: decision,
                DenialReason: denialReason,
                ResolvedPath: path,
                Specifier: specifier,
                From: fromFilePath,
                ProjectRoot: _normalizedProjectRoot));

            // 向统一收集器推送
            _diagnosticCollector?.EmitSecurity(
                decision == SecurityDecision.Allowed ? "security.check.pass" : "security.check.deny",
                path,
                new { decision = decision.ToString(), reason = denialReason?.ToString(), specifier, from = fromFilePath });
        }

        /// <summary>
        /// 解析物理路径（跟踪符号链接）。
        /// 如果路径不存在，回退返回规范化后的原路径。
        /// </summary>
        private static string ResolvePhysicalPath(string normalizedPath)
        {
            try
            {
                // File / Directory 不存在时无法解析符号链接，回退原路径
                if (File.Exists(normalizedPath))
                {
                    var fileInfo = new FileInfo(normalizedPath);
                    if (fileInfo.LinkTarget is not null)
                    {
                        return NormalizePath(Path.GetFullPath(fileInfo.LinkTarget,
                            Path.GetDirectoryName(normalizedPath) ?? normalizedPath));
                    }
                }
                else if (Directory.Exists(normalizedPath))
                {
                    var dirInfo = new DirectoryInfo(normalizedPath);
                    if (dirInfo.LinkTarget is not null)
                    {
                        return NormalizePath(Path.GetFullPath(dirInfo.LinkTarget,
                            Path.GetDirectoryName(normalizedPath) ?? normalizedPath));
                    }
                }

                return normalizedPath;
            }
            catch
            {
                // 无法读取链接目标时，保守视为原路径
                return normalizedPath;
            }
        }

        private static bool ContainsTraversalSequence(string rawPath)
        {
            // 检测原始路径中是否包含 .. 序列（即使规范化后消失也记录审计）
            return rawPath.Contains(".." + Path.DirectorySeparatorChar) ||
                   rawPath.Contains(".." + Path.AltDirectorySeparatorChar) ||
                   rawPath.EndsWith("..") ||
                   rawPath.Contains("..\\") ||
                   rawPath.Contains("../");
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

        private static string BuildHint(SecurityDenialReason reason)
        {
            return reason switch
            {
                SecurityDenialReason.OutOfBoundary =>
                    "请调整导入路径，或在 paperclip.json 的 moduleRuntime.allowImportPaths 中添加白名单。\n" +
                    "也可临时通过 --allow-import=<path> 指定额外白名单。",
                SecurityDenialReason.SymlinkEscape =>
                    "符号链接的物理目标路径超出安全边界。请将白名单指向实际物理路径，\n" +
                    "或将目标文件移入项目目录内。",
                SecurityDenialReason.PathTraversal =>
                    "路径包含 '..' 穿越序列，且规范化后超出安全边界。请使用绝对路径或白名单。",
                SecurityDenialReason.InvalidPath =>
                    "路径为空或非法。请检查 import 语句中的模块路径。",
                _ =>
                    "请检查导入路径与安全配置。"
            };
        }
    }

    /// <summary>
    /// 安全校验决策。
    /// </summary>
    public enum SecurityDecision
    {
        /// <summary>允许。</summary>
        Allowed,
        /// <summary>拒绝。</summary>
        Denied
    }

    /// <summary>
    /// 安全拒绝原因。
    /// </summary>
    public enum SecurityDenialReason
    {
        /// <summary>路径越界。</summary>
        OutOfBoundary,
        /// <summary>符号链接越界。</summary>
        SymlinkEscape,
        /// <summary>路径穿越。</summary>
        PathTraversal,
        /// <summary>非法路径。</summary>
        InvalidPath
    }

    /// <summary>
    /// 安全审计日志条目。
    /// </summary>
    public sealed record SecurityAuditEntry(
        DateTimeOffset Timestamp,
        SecurityDecision Decision,
        SecurityDenialReason? DenialReason,
        string ResolvedPath,
        string Specifier,
        string? From,
        string ProjectRoot);

    /// <summary>
    /// 导入安全策略异常——路径被安全策略拒绝时抛出。
    /// 包含结构化错误码、拒绝原因与修复提示。
    /// </summary>
    public sealed class ImportSecurityException : Exception, IModuleStructuredError
    {
        /// <summary>业务错误码（PC_SEC_001 / PC_SEC_002 / PC_SEC_003）。</summary>
        public string Code { get; }

        /// <summary>被校验的已解析路径。</summary>
        public string ResolvedPath { get; }

        /// <summary>原始 specifier。</summary>
        public string Specifier { get; }

        /// <summary>发起导入的来源文件。</summary>
        public string? From { get; }

        /// <summary>拒绝原因枚举。</summary>
        public SecurityDenialReason Reason { get; }

        /// <summary>修复建议。</summary>
        public string Hint { get; }

        /// <summary>
        /// 初始化安全异常。
        /// </summary>
        public ImportSecurityException(
            string code,
            string resolvedPath,
            string specifier,
            string? from,
            SecurityDenialReason reason,
            string message,
            string hint)
            : base($"[{code}] {message} (specifier: '{specifier}', from: '{from ?? "<entry>"}', resolvedPath: '{resolvedPath}')")
        {
            Code = code;
            ResolvedPath = resolvedPath;
            Specifier = specifier;
            From = from;
            Reason = reason;
            Hint = hint;
        }

        /// <summary>
        /// 转为结构化错误对象，适用于 JSON 序列化与 debug 输出。
        /// </summary>
        public object ToStructuredError() => new
        {
            code = Code,
            specifier = Specifier,
            from = From,
            resolvedPath = ResolvedPath,
            reason = Reason.ToString(),
            hint = Hint,
            message = Message
        };
    }
}

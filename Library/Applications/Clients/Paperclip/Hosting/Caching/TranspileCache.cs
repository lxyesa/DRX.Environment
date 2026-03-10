using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DrxPaperclip.Hosting.Caching;

/// <summary>
/// TypeScript 转译缓存组件。
/// 职责：按“源内容哈希 + TypeScript 版本 + 缓存格式版本”校验缓存命中，并将结果持久化到磁盘。
/// 关键依赖：System.Text.Json、System.Security.Cryptography、文件系统 API。
/// </summary>
public sealed class TranspileCache
{
    private const int CurrentFormatVersion = 2;
    private const string CacheRelativePath = ".paperclip/transpile-cache";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly string _cacheRoot;

    /// <summary>
    /// 初始化转译缓存。
    /// </summary>
    /// <param name="projectRoot">项目根目录。</param>
    public TranspileCache(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new ArgumentException("项目根目录不能为空。", nameof(projectRoot));

        _cacheRoot = Path.Combine(Path.GetFullPath(projectRoot), CacheRelativePath);

        try
        {
            Directory.CreateDirectory(_cacheRoot);
        }
        catch
        {
            // 缓存目录创建失败时降级：后续读写会自动 miss，不影响主流程。
        }
    }

    /// <summary>
    /// 尝试命中转译缓存。
    /// </summary>
    /// <param name="scriptPath">TypeScript 脚本路径。</param>
    /// <param name="sourceContent">脚本源内容。</param>
    /// <param name="typeScriptVersion">TypeScript 版本标识。</param>
    /// <param name="transpileConfigTag">转译配置指纹（来自 <c>JavaScript.TranspileConfigTag</c>），配置变化时自动失效。</param>
    /// <param name="transpiledCode">命中时返回转译结果。</param>
    /// <returns>命中返回 true，否则 false。</returns>
    public bool TryGet(
        string scriptPath,
        string sourceContent,
        string typeScriptVersion,
        string transpileConfigTag,
        out string? transpiledCode)
    {
        transpiledCode = null;

        if (string.IsNullOrWhiteSpace(scriptPath) ||
            string.IsNullOrEmpty(sourceContent) ||
            string.IsNullOrWhiteSpace(typeScriptVersion))
        {
            return false;
        }

        var cacheFile = GetCacheFilePath(scriptPath);

        if (!File.Exists(cacheFile))
        {
            return false;
        }

        try
        {
            var json = File.ReadAllText(cacheFile, Encoding.UTF8);
            var payload = JsonSerializer.Deserialize<CachePayload>(json, JsonOptions);
            if (payload == null)
            {
                SafeDelete(cacheFile);
                return false;
            }

            var sourceHash = ComputeSha256(sourceContent);
            var isValid = payload.FormatVersion == CurrentFormatVersion
                          && string.Equals(payload.TypeScriptVersion, typeScriptVersion, StringComparison.Ordinal)
                          && string.Equals(payload.TranspileConfigTag, transpileConfigTag, StringComparison.Ordinal)
                          && FixedTimeEquals(payload.SourceHash, sourceHash)
                          && !string.IsNullOrEmpty(payload.TranspiledCode);

            if (!isValid)
            {
                SafeDelete(cacheFile);
                return false;
            }

            transpiledCode = payload.TranspiledCode;
            return true;
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            SafeDelete(cacheFile);
            return false;
        }
    }

    /// <summary>
    /// 写入转译缓存。
    /// </summary>
    /// <param name="scriptPath">TypeScript 脚本路径。</param>
    /// <param name="sourceContent">脚本源内容。</param>
    /// <param name="typeScriptVersion">TypeScript 版本标识。</param>
    /// <param name="transpileConfigTag">转译配置指纹（来自 <c>JavaScript.TranspileConfigTag</c>）。</param>
    /// <param name="transpiledCode">转译后的 JavaScript 代码。</param>
    public void Set(string scriptPath, string sourceContent, string typeScriptVersion, string transpileConfigTag, string transpiledCode)
    {
        if (string.IsNullOrWhiteSpace(scriptPath) ||
            string.IsNullOrEmpty(sourceContent) ||
            string.IsNullOrWhiteSpace(typeScriptVersion) ||
            string.IsNullOrEmpty(transpileConfigTag) ||
            string.IsNullOrEmpty(transpiledCode))
        {
            return;
        }

        var cacheFile = GetCacheFilePath(scriptPath);
        var tempFile = cacheFile + ".tmp";

        var payload = new CachePayload
        {
            FormatVersion = CurrentFormatVersion,
            ScriptPath = NormalizePath(scriptPath),
            SourceHash = ComputeSha256(sourceContent),
            TypeScriptVersion = typeScriptVersion,
            TranspileConfigTag = transpileConfigTag,
            TranspiledCode = transpiledCode,
            CachedAtUtc = DateTimeOffset.UtcNow
        };

        try
        {
            Directory.CreateDirectory(_cacheRoot);
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            File.WriteAllText(tempFile, json, Encoding.UTF8);

            if (File.Exists(cacheFile))
            {
                File.Copy(tempFile, cacheFile, overwrite: true);
                File.Delete(tempFile);
            }
            else
            {
                File.Move(tempFile, cacheFile);
            }
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            SafeDelete(tempFile);
        }
    }

    /// <summary>
    /// 使指定脚本的缓存失效。
    /// </summary>
    /// <param name="scriptPath">TypeScript 脚本路径。</param>
    public void Invalidate(string scriptPath)
    {
        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            return;
        }

        SafeDelete(GetCacheFilePath(scriptPath));
    }

    private string GetCacheFilePath(string scriptPath)
    {
        var normalizedPath = NormalizePath(scriptPath);
        var pathHash = ComputeSha256(normalizedPath);
        return Path.Combine(_cacheRoot, $"{pathHash}.json");
    }

    private static string NormalizePath(string scriptPath)
    {
        var fullPath = Path.GetFullPath(scriptPath);
        return fullPath.Replace('\\', '/');
    }

    private static string ComputeSha256(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static bool FixedTimeEquals(string? left, string? right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
        {
            return false;
        }

        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static void SafeDelete(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // 删除失败也不能影响主流程。
        }
    }

    private static bool IsRecoverable(Exception ex)
    {
        return ex is IOException
               or UnauthorizedAccessException
               or JsonException
               or NotSupportedException
               or ArgumentException;
    }

    private sealed class CachePayload
    {
        public int FormatVersion { get; init; }

        public string ScriptPath { get; init; } = string.Empty;

        public string SourceHash { get; init; } = string.Empty;

        public string TypeScriptVersion { get; init; } = string.Empty;

        /// <summary>转译配置指纹，对应 <c>JavaScript.TranspileConfigTag</c>。</summary>
        public string TranspileConfigTag { get; init; } = string.Empty;

        public string TranspiledCode { get; init; } = string.Empty;

        public DateTimeOffset CachedAtUtc { get; init; }
    }
}

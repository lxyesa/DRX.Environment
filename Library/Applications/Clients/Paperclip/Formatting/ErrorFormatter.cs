using System.Text;
using Drx.Sdk.Shared.JavaScript.Engine;
using Drx.Sdk.Shared.JavaScript.Exceptions;

namespace DrxPaperclip.Formatting;

/// <summary>
/// 将 SDK 异常格式化为用户友好的 stderr 输出，并提供退出码映射。
/// </summary>
public static class ErrorFormatter
{
    /// <summary>
    /// 将异常格式化为结构化错误字符串。
    /// 格式：Error [{code}]: {message}\n  at {location}\n  Hint: {hint}
    /// </summary>
    public static string Format(Exception ex)
    {
        return ex switch
        {
            ImportSecurityException sec => FormatSecurityException(sec),
            ModuleResolutionException res => FormatResolutionException(res),
            ModuleLoadException load => FormatLoadException(load),
            JavaScriptException js => FormatJavaScriptException(js),
            _ => FormatGenericException(ex)
        };
    }

    /// <summary>
    /// 返回异常对应的进程退出码（NFR-3）。
    /// </summary>
    public static int GetExitCode(Exception ex)
    {
        return ex switch
        {
            ImportSecurityException => 4,
            JavaScriptException => 1,
            ModuleResolutionException => 1,
            ModuleLoadException => 1,
            _ => 1
        };
    }

    private static string FormatSecurityException(ImportSecurityException ex)
    {
        var sb = new StringBuilder();
        sb.Append($"Error [{ex.Code}]: {ex.Message}");
        sb.Append($"\n  Hint: {ex.Hint}");
        return sb.ToString();
    }

    private static string FormatResolutionException(ModuleResolutionException ex)
    {
        var sb = new StringBuilder();
        sb.Append($"Error [{ex.Code}]: {ex.Message}");
        if (ex.Hint is not null)
            sb.Append($"\n  Hint: {ex.Hint}");
        return sb.ToString();
    }

    private static string FormatLoadException(ModuleLoadException ex)
    {
        var sb = new StringBuilder();
        sb.Append($"Error [{ex.Code}]: {ex.Message}");
        if (ex.Hint is not null)
            sb.Append($"\n  Hint: {ex.Hint}");
        return sb.ToString();
    }

    private static string FormatJavaScriptException(JavaScriptException ex)
    {
        var sb = new StringBuilder();
        var code = ex.ErrorType ?? "JavaScriptError";
        sb.Append($"Error [{code}]: {ex.Message}");
        if (ex.ScriptStack is not null)
            sb.Append($"\n  at {ex.ScriptStack}");
        else if (ex.ErrorLocation is not null)
            sb.Append($"\n  at {ex.ErrorLocation}");
        return sb.ToString();
    }

    private static string FormatGenericException(Exception ex)
    {
        return $"Error [UNKNOWN]: {ex.Message}";
    }
}

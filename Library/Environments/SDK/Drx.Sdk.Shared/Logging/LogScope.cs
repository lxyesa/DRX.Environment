namespace Drx.Sdk.Shared;

/// <summary>
/// 日志作用域，在 <c>using</c> 块内为当前异步上下文附加额外的上下文标签。
/// <example>
/// <code>
/// using (Logger.BeginScope("RequestId=abc123"))
/// {
///     Logger.Info("处理请求");  // 输出中会附带 [RequestId=abc123]
/// }
/// </code>
/// </example>
/// </summary>
public sealed class LogScope : IDisposable
{
    private static readonly AsyncLocal<LogScope?> _current = new();
    private readonly LogScope? _parent;
    private readonly string _state;
    private bool _disposed;

    internal LogScope(string state)
    {
        _state = state;
        _parent = _current.Value;
        _current.Value = this;
    }

    /// <summary>
    /// 当前异步上下文中的活动作用域（可能为 null）。
    /// </summary>
    internal static LogScope? Current => _current.Value;

    /// <summary>
    /// 构建当前作用域链的完整上下文字符串。
    /// </summary>
    internal static string? GetScopeString()
    {
        var current = _current.Value;
        if (current == null) return null;

        // 单层优化：避免创建 List
        if (current._parent == null)
            return current._state;

        // 多层：从内到外收集，再反转拼接
        var parts = new List<string>();
        var scope = current;
        while (scope != null)
        {
            parts.Add(scope._state);
            scope = scope._parent;
        }

        parts.Reverse();
        return string.Join(" → ", parts);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _current.Value = _parent;
    }
}

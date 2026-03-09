using Drx.Sdk.Shared.JavaScript.Engine;

namespace DrxPaperclip.Diagnostics;

/// <summary>
/// 诊断输出管道。将 <see cref="ModuleDiagnosticCollector"/> 事件实时输出到 stderr。
/// 仅在 <c>--debug</c> 模式下激活；默认模式静默，无任何诊断输出。
/// </summary>
public sealed class DiagnosticOutput
{
    private readonly ModuleDiagnosticCollector _collector;
    private readonly bool _enabled;
    private int _lastPrintedIndex;

    /// <summary>
    /// 创建诊断输出管道。
    /// </summary>
    /// <param name="collector">SDK 诊断事件收集器实例。</param>
    /// <param name="enabled">是否启用诊断输出（对应 <c>--debug</c> 标志）。</param>
    public DiagnosticOutput(ModuleDiagnosticCollector collector, bool enabled)
    {
        _collector = collector;
        _enabled = enabled;
    }

    /// <summary>
    /// 刷新并输出自上次调用以来新产生的诊断事件到 stderr。
    /// 非 debug 模式下直接返回，零开销。
    /// </summary>
    public void Flush()
    {
        if (!_enabled) return;

        var events = _collector.Events;
        for (var i = _lastPrintedIndex; i < events.Count; i++)
        {
            Console.Error.WriteLine(events[i].ToReadableString());
        }
        _lastPrintedIndex = events.Count;
    }

    /// <summary>
    /// 输出模块缓存统计摘要到 stderr。
    /// 非 debug 模式下直接返回。
    /// </summary>
    /// <param name="cache">模块缓存实例。</param>
    public void PrintSummary(ModuleCache cache)
    {
        if (!_enabled) return;

        var stats = cache.GetStatistics();
        Console.Error.WriteLine();
        Console.Error.WriteLine("=== Module Diagnostics Summary ===");
        Console.Error.WriteLine($"  Total modules cached : {stats.TotalEntries}");
        Console.Error.WriteLine($"  Cache hits           : {stats.HitCount}");
        Console.Error.WriteLine($"  Cache misses         : {stats.MissCount}");
        Console.Error.WriteLine($"  Hit rate             : {stats.HitRate:P1}");
        Console.Error.WriteLine($"  Loaded               : {stats.LoadedCount}");
        Console.Error.WriteLine($"  Failed               : {stats.FailedCount}");
        Console.Error.WriteLine($"  Loading              : {stats.LoadingCount}");

        var summary = _collector.GetSummary();
        Console.Error.WriteLine($"  Diagnostic events    : {summary.TotalEvents}");
        foreach (var kvp in summary.ByCategory)
        {
            Console.Error.WriteLine($"    [{kvp.Key}] : {kvp.Value}");
        }
        Console.Error.WriteLine("==================================");
    }
}

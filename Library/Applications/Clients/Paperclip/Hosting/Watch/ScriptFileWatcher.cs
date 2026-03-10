using Drx.Sdk.Network.Http.Configs;
using Drx.Sdk.Network.Http.ResourceManagement;
using DrxPaperclip.Cli;

namespace DrxPaperclip.Hosting.Watch;

/// <summary>
/// 脚本文件监听适配层：复用 <see cref="DevFileChangeService"/> 聚合变更，
/// 并仅向上游分发脚本扩展名（.ts/.mts/.cts/.js/.mjs）对应的事件。
/// </summary>
public sealed class ScriptFileWatcher : IDisposable
{
    private static readonly HashSet<string> ScriptExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ts", ".mts", ".cts", ".js", ".mjs"
    };

    private readonly DevFileChangeService _changeService;
    private bool _disposed;

    /// <summary>
    /// 当聚合后的脚本文件发生变更时触发。
    /// </summary>
    public event EventHandler<ScriptFilesChangedEventArgs>? ScriptFilesChanged;

    /// <summary>
    /// 初始化脚本监听器。
    /// </summary>
    /// <param name="options">CLI 选项，用于获取脚本路径与去抖参数默认值。</param>
    /// <param name="projectRoot">项目根目录（监听根）。</param>
    /// <param name="debounceMilliseconds">可选去抖窗口；不传时默认 200ms。</param>
    public ScriptFileWatcher(PaperclipOptions options, string projectRoot, int? debounceMilliseconds = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        var fullProjectRoot = Path.GetFullPath(projectRoot);
        var devOptions = DevRuntimeOptions.CreateDefault();
        devOptions.WatchDirectories.Add(fullProjectRoot);

        if (debounceMilliseconds is > 0)
        {
            devOptions.DebounceMilliseconds = debounceMilliseconds.Value;
        }

        devOptions.Validate();

        _changeService = new DevFileChangeService(
            devOptions.WatchDirectories,
            devOptions.DebounceMilliseconds,
            path => Path.GetFullPath(path));

        _changeService.ChangesAggregated += OnChangesAggregated;
    }

    /// <summary>
    /// 启动脚本变更监听。
    /// </summary>
    public void Start()
    {
        ThrowIfDisposed();
        _changeService.Start();
    }

    /// <summary>
    /// 停止脚本变更监听。
    /// </summary>
    public void Stop()
    {
        ThrowIfDisposed();
        _changeService.Stop();
    }

    /// <summary>
    /// 释放监听资源。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _changeService.ChangesAggregated -= OnChangesAggregated;
        _changeService.Dispose();
        _disposed = true;
    }

    private void OnChangesAggregated(object? sender, Drx.Sdk.Network.Http.Models.DevAssetChangedEvent e)
    {
        if (e.ChangeSet.Count == 0)
        {
            return;
        }

        var changedScriptFiles = e.ChangeSet
            .Select(item => item.Path)
            .Where(IsScriptFile)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (changedScriptFiles.Count == 0)
        {
            return;
        }

        ScriptFilesChanged?.Invoke(
            this,
            new ScriptFilesChangedEventArgs(changedScriptFiles, DateTimeOffset.UtcNow));
    }

    private static bool IsScriptFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var ext = Path.GetExtension(path);
        return ScriptExtensions.Contains(ext);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ScriptFileWatcher));
        }
    }
}

/// <summary>
/// 脚本文件变更事件参数。
/// </summary>
public sealed class ScriptFilesChangedEventArgs : EventArgs
{
    /// <summary>
    /// 变更脚本文件（已去重）。
    /// </summary>
    public IReadOnlyList<string> Paths { get; }

    /// <summary>
    /// 事件触发时间（UTC）。
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// 创建脚本变更事件参数。
    /// </summary>
    public ScriptFilesChangedEventArgs(IReadOnlyList<string> paths, DateTimeOffset timestamp)
    {
        Paths = paths;
        Timestamp = timestamp;
    }
}

using System.Collections.Concurrent;
using Drx.Sdk.Network.Http.Models;
using Drx.Sdk.Shared;

namespace Drx.Sdk.Network.Http.ResourceManagement;

/// <summary>
/// 开发态文件变更监听与去抖聚合服务。
/// 将 FileSystemWatcher 事件归一化为 DevAssetChangedEvent。
/// </summary>
public sealed class DevFileChangeService : IDisposable
{
    private readonly IReadOnlyList<string> _watchDirectories;
    private readonly int _debounceMilliseconds;
    private readonly Func<string, string>? _pathNormalizer;
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly ConcurrentDictionary<string, PendingChange> _pendingChanges = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lifecycleLock = new();
    private Timer? _debounceTimer;
    private bool _isRunning;
    private bool _disposed;

    /// <summary>
    /// 聚合事件回调。
    /// </summary>
    public event EventHandler<DevAssetChangedEvent>? ChangesAggregated;

    /// <summary>
    /// 服务是否运行中。
    /// </summary>
    public bool IsRunning
    {
        get
        {
            lock (_lifecycleLock)
            {
                return _isRunning;
            }
        }
    }

    /// <summary>
    /// 创建开发态变更监听服务。
    /// </summary>
    public DevFileChangeService(IEnumerable<string> watchDirectories, int debounceMilliseconds = 200, Func<string, string>? pathNormalizer = null)
    {
        _watchDirectories = watchDirectories
            ?.Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? [];

        _debounceMilliseconds = debounceMilliseconds <= 0 ? 200 : debounceMilliseconds;
        _pathNormalizer = pathNormalizer;
    }

    /// <summary>
    /// 启动监听。
    /// </summary>
    public void Start()
    {
        ThrowIfDisposed();

        lock (_lifecycleLock)
        {
            if (_isRunning)
            {
                return;
            }

            foreach (var directory in _watchDirectories)
            {
                if (!Directory.Exists(directory))
                {
                    Logger.Warn($"[DevFileChangeService] 目录不存在，跳过监听: {directory}");
                    continue;
                }

                var watcher = new FileSystemWatcher(directory)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
                    EnableRaisingEvents = true
                };

                watcher.Created += OnCreated;
                watcher.Changed += OnChanged;
                watcher.Deleted += OnDeleted;
                watcher.Renamed += OnRenamed;
                watcher.Error += OnWatcherError;

                _watchers.Add(watcher);
            }

            _debounceTimer = new Timer(OnDebounceElapsed);
            _isRunning = true;
            Logger.Info($"[DevFileChangeService] 已启动，监听目录数: {_watchers.Count}");
        }
    }

    /// <summary>
    /// 停止监听。
    /// </summary>
    public void Stop()
    {
        lock (_lifecycleLock)
        {
            if (!_isRunning)
            {
                return;
            }

            foreach (var watcher in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Created -= OnCreated;
                watcher.Changed -= OnChanged;
                watcher.Deleted -= OnDeleted;
                watcher.Renamed -= OnRenamed;
                watcher.Error -= OnWatcherError;
                watcher.Dispose();
            }

            _watchers.Clear();
            _debounceTimer?.Dispose();
            _debounceTimer = null;
            _pendingChanges.Clear();
            _isRunning = false;
            Logger.Info("[DevFileChangeService] 已停止");
        }
    }

    private void OnCreated(object sender, FileSystemEventArgs e) => EnqueueChange(e.FullPath, AssetChangeKind.Created);

    private void OnChanged(object sender, FileSystemEventArgs e) => EnqueueChange(e.FullPath, AssetChangeKind.Changed);

    private void OnDeleted(object sender, FileSystemEventArgs e) => EnqueueChange(e.FullPath, AssetChangeKind.Deleted);

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        EnqueueChange(e.OldFullPath, AssetChangeKind.Deleted);
        EnqueueChange(e.FullPath, AssetChangeKind.Renamed);
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        Logger.Error($"[DevFileChangeService] Watcher error: {e.GetException()?.Message}");
    }

    private void EnqueueChange(string fullPath, AssetChangeKind kind)
    {
        if (string.IsNullOrWhiteSpace(fullPath) || IsSystemTempFile(fullPath))
        {
            return;
        }

        _pendingChanges[fullPath] = new PendingChange
        {
            Path = fullPath,
            Kind = kind,
            AssetType = ResolveAssetKind(fullPath)
        };

        _debounceTimer?.Change(_debounceMilliseconds, Timeout.Infinite);
    }

    private void OnDebounceElapsed(object? state)
    {
        if (_pendingChanges.IsEmpty)
        {
            return;
        }

        var drained = new List<PendingChange>();
        foreach (var pair in _pendingChanges.ToArray())
        {
            if (_pendingChanges.TryRemove(pair.Key, out var value))
            {
                drained.Add(value);
            }
        }

        if (drained.Count == 0)
        {
            return;
        }

        var evt = new DevAssetChangedEvent
        {
            EventId = Guid.NewGuid().ToString("N"),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            RecommendedAction = ResolveRecommendedAction(drained),
            ChangeSet = drained.Select(ToAssetChangeItem).ToList()
        };

        try
        {
            ChangesAggregated?.Invoke(this, evt);
            Logger.Debug($"[DevFileChangeService] 聚合 {evt.ChangeSet.Count} 个变更事件");
        }
        catch (Exception ex)
        {
            Logger.Warn($"[DevFileChangeService] 分发聚合事件失败: {ex.Message}");
        }
    }

    private AssetChangeItem ToAssetChangeItem(PendingChange pending)
    {
        var path = _pathNormalizer?.Invoke(pending.Path) ?? pending.Path.Replace('\\', '/');
        return new AssetChangeItem
        {
            Path = path,
            Kind = pending.Kind,
            AssetType = pending.AssetType
        };
    }

    private static DevRecommendedAction ResolveRecommendedAction(IEnumerable<PendingChange> changes)
    {
        var list = changes as IList<PendingChange> ?? changes.ToList();
        if (list.Count == 0)
        {
            return DevRecommendedAction.Reload;
        }

        return list.All(x => x.AssetType == AssetKind.Css)
            ? DevRecommendedAction.CssReplace
            : DevRecommendedAction.Reload;
    }

    private static AssetKind ResolveAssetKind(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".css" => AssetKind.Css,
            ".html" or ".htm" => AssetKind.Html,
            ".js" or ".mjs" => AssetKind.Js,
            _ => AssetKind.Other
        };
    }

    private static bool IsSystemTempFile(string path)
    {
        var fileName = Path.GetFileName(path);
        return fileName.StartsWith("~$") || fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(DevFileChangeService));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
    }

    private sealed class PendingChange
    {
        public string Path { get; init; } = string.Empty;
        public AssetChangeKind Kind { get; init; }
        public AssetKind AssetType { get; init; }
    }
}

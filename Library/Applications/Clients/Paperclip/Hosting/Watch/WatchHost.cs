using DrxPaperclip.Cli;
using DrxPaperclip.Formatting;

namespace DrxPaperclip.Hosting.Watch;

/// <summary>
/// run --watch 宿主：负责初次执行、脚本变更重载、异常容错与优雅退出。
/// </summary>
public static class WatchHost
{
    private static readonly object ReloadGate = new();
    private static bool _pendingReload;
    private static IReadOnlyList<string> _latestChangedPaths = [];
    private static int _round;
    private static CancellationTokenSource? _roundCts;

    /// <summary>
    /// 启动 watch 生命周期。
    /// </summary>
    /// <param name="options">CLI 选项。</param>
    /// <param name="bootstrap">引擎启动上下文。</param>
    /// <returns>退出码（正常退出为 0）。</returns>
    public static int Run(PaperclipOptions options, EngineBootstrap bootstrap)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(bootstrap);

        lock (ReloadGate)
        {
            _pendingReload = false;
            _latestChangedPaths = [];
            _round = 0;
        }

        var stopSignal = new ManualResetEventSlim(false);
        using var reloadSignal = new AutoResetEvent(false);
        var watcher = new ScriptFileWatcher(options, bootstrap.Options.ProjectRoot, debounceMilliseconds: 200);
        var watchWorker = Task.Run(() => ExecuteReloadLoop(options, bootstrap, stopSignal, reloadSignal));

        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            stopSignal.Set();
            reloadSignal.Set();
        };

        EventHandler<ScriptFilesChangedEventArgs> reloadHandler = (_, e) =>
        {
            lock (ReloadGate)
            {
                _latestChangedPaths = e.Paths;
                _pendingReload = true;
            }

            reloadSignal.Set();
        };

        watcher.ScriptFilesChanged += reloadHandler;
        Console.CancelKeyPress += cancelHandler;

        try
        {
            watcher.Start();
            Console.Error.WriteLine("[watch] 已启动，等待脚本文件变更（Ctrl+C 退出）...");

            lock (ReloadGate)
            {
                _latestChangedPaths = [];
                _pendingReload = true;
            }

            reloadSignal.Set();

            stopSignal.Wait();
            return 0;
        }
        finally
        {
            stopSignal.Set();
            reloadSignal.Set();

            // 确保退出时停止所有服务器
            CancelCurrentRound();

            watcher.ScriptFilesChanged -= reloadHandler;
            watcher.Dispose();
            Console.CancelKeyPress -= cancelHandler;

            try
            {
                watchWorker.Wait(TimeSpan.FromMilliseconds(500));
            }
            catch
            {
                // 退出阶段忽略 worker 状态异常，避免影响主进程收尾。
            }
        }
    }

    private static void ExecuteReloadLoop(
        PaperclipOptions options,
        EngineBootstrap bootstrap,
        ManualResetEventSlim stopSignal,
        AutoResetEvent reloadSignal)
    {
        var isInitial = true;
        Task? currentRound = null;

        while (true)
        {
            var signaled = WaitHandle.WaitAny([
                stopSignal.WaitHandle,
                reloadSignal
            ]);

            if (signaled == 0 || stopSignal.IsSet)
            {
                // 退出前停止当前轮次
                CancelCurrentRound();
                currentRound?.Wait(TimeSpan.FromSeconds(3));
                return;
            }

            IReadOnlyList<string> changedPaths;
            lock (ReloadGate)
            {
                if (!_pendingReload)
                {
                    continue;
                }

                _pendingReload = false;
                changedPaths = _latestChangedPaths;
            }

            // 停止上一轮的服务器实例并等待轮次结束
            if (currentRound is { IsCompleted: false })
            {
                CancelCurrentRound();
                try { currentRound.Wait(TimeSpan.FromSeconds(5)); } catch { }
            }

            if (!isInitial)
            {
                SafeClearScreen();
            }

            PrintRoundBanner(isInitial: isInitial, changedPaths: isInitial ? null : changedPaths);

            if (!isInitial)
            {
                ApplyIncrementalInvalidation(bootstrap, changedPaths);
            }

            // 启动新轮次
            var cts = new CancellationTokenSource();
            lock (ReloadGate) { _roundCts = cts; }
            currentRound = Task.Run(() => ExecuteSingleRound(options, bootstrap, cts.Token));
            isInitial = false;
        }
    }

    /// <summary>
    /// 取消当前轮次：先停止所有活跃服务器（解除 StartAsync 阻塞），再触发 CancellationToken。
    /// </summary>
    private static void CancelCurrentRound()
    {
        CancellationTokenSource? cts;
        lock (ReloadGate) { cts = _roundCts; }

        // 先停止服务器，使 StartAsync 返回，WaitForPendingTask 才能解除阻塞
        ActiveServerTracker.StopAllAsync().Wait(TimeSpan.FromSeconds(3));

        try { cts?.Cancel(); } catch { }
    }

    private static void ApplyIncrementalInvalidation(EngineBootstrap bootstrap, IReadOnlyList<string>? changedPaths)
    {
        if (changedPaths is not { Count: > 0 })
        {
            return;
        }

        var moduleCache = bootstrap.ModuleCache;
        if (moduleCache is null)
        {
            return;
        }

        var invalidatedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var changedPath in changedPaths)
            {
                if (string.IsNullOrWhiteSpace(changedPath))
                {
                    continue;
                }

                var cacheKey = Drx.Sdk.Shared.JavaScript.Engine.ModuleCache.NormalizeCacheKey(changedPath);
                var invalidated = moduleCache.InvalidateWithDependents(cacheKey);
                foreach (var key in invalidated)
                {
                    invalidatedKeys.Add(key);
                }
            }

            if (invalidatedKeys.Count > 0)
            {
                Console.Error.WriteLine($"[watch] 模块增量失效完成，失效条目: {invalidatedKeys.Count}");
            }
        }
        catch (Exception ex)
        {
            moduleCache.Clear();
            Console.Error.WriteLine($"[watch] 模块增量失效失败，已回退全量清空缓存: {ex.Message}");
        }
    }

    private static void ExecuteSingleRound(PaperclipOptions options, EngineBootstrap bootstrap, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _round);

        try
        {
            ScriptHost.Run(options, bootstrap, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Console.Error.WriteLine("[watch] 当前轮次已被取消，准备重载...");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ErrorFormatter.Format(ex));
            bootstrap.DiagnosticOutput.Flush();
            Console.Error.WriteLine("[watch] 本轮执行失败，继续监听下一次变更...");
        }
    }

    private static void PrintRoundBanner(bool isInitial, IReadOnlyList<string>? changedPaths)
    {
        var now = DateTimeOffset.Now;
        var label = isInitial ? "initial" : "reload";

        Console.Error.WriteLine($"[{now:yyyy-MM-dd HH:mm:ss.fff}] [watch:{label}] round={Volatile.Read(ref _round) + 1}");
        if (changedPaths is { Count: > 0 })
        {
            Console.Error.WriteLine($"[watch] 触发文件: {string.Join(", ", changedPaths)}");
        }
    }

    private static void SafeClearScreen()
    {
        try
        {
            if (!Console.IsOutputRedirected)
            {
                Console.Clear();
            }
        }
        catch
        {
            // 清屏失败不影响 watch 主流程
        }
    }
}

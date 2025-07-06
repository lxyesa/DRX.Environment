using System.Diagnostics;

namespace Drx.Sdk.Events
{
    /// <summary>
    /// 提供监听特定进程启动和关闭事件的功能
    /// </summary>
    public sealed class ProcessListener : IDisposable
    {
        private readonly string processName;
        private readonly int pollingInterval;
        private readonly Dictionary<int, ProcessInfo> runningProcesses = new();
        private bool isRunning = false;
        private readonly CancellationTokenSource cts = new();
        private Task? monitoringTask;

        /// <summary>
        /// 当指定进程启动时触发
        /// </summary>
        public event EventHandler<ProcessEventArgs>? ProcessStarted;

        /// <summary>
        /// 当指定进程关闭时触发
        /// </summary>
        public event EventHandler<ProcessEventArgs>? ProcessStopped;

        /// <summary>
        /// 当监听器状态发生变化时触发
        /// </summary>
        public event EventHandler<bool>? StateChanged;

        /// <summary>
        /// 获取监听器当前是否正在运行
        /// </summary>
        public bool IsRunning => isRunning;

        /// <summary>
        /// 获取正在监听的进程名称
        /// </summary>
        public string ProcessName => processName;

        /// <summary>
        /// 初始化进程监听器
        /// </summary>
        /// <param name="processName">要监听的进程名称（不含.exe扩展名）</param>
        /// <param name="pollingInterval">轮询间隔，单位毫秒（默认500ms）</param>
        public ProcessListener(string processName, int pollingInterval = 500)
        {
            if (string.IsNullOrWhiteSpace(processName))
                throw new ArgumentNullException(nameof(processName), "进程名称不能为空");

            if (pollingInterval <= 0)
                throw new ArgumentOutOfRangeException(nameof(pollingInterval), "轮询间隔必须大于0");

            this.processName = processName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
            this.pollingInterval = pollingInterval;
        }

        /// <summary>
        /// 开始监听进程
        /// </summary>
        public void Start()
        {
            if (isRunning)
                return;

            isRunning = true;

            // 初始化已运行的进程列表
            UpdateRunningProcesses();

            // 启动监控任务
            monitoringTask = Task.Run(MonitorProcessesAsync, cts.Token);

            // 触发状态改变事件
            StateChanged?.Invoke(this, true);
        }

        /// <summary>
        /// 停止监听进程
        /// </summary>
        public void Stop()
        {
            if (!isRunning)
                return;

            // 请求取消监控任务
            cts.Cancel();

            try
            {
                // 等待任务完成
                monitoringTask?.Wait();
            }
            catch (AggregateException ex) when (ex.InnerExceptions.Any(e => e is TaskCanceledException))
            {
                // 预期的取消异常，忽略
            }

            isRunning = false;

            // 清空运行中进程列表
            runningProcesses.Clear();

            // 触发状态改变事件
            StateChanged?.Invoke(this, false);
        }

        /// <summary>
        /// 异步监控进程的启动和关闭
        /// </summary>
        private async Task MonitorProcessesAsync()
        {
            while (!cts.Token.IsCancellationRequested)
            {
                try
                {
                    CheckProcessChanges();
                    await Task.Delay(pollingInterval, cts.Token);
                }
                catch (TaskCanceledException)
                {
                    // 预期的取消异常，退出循环
                    break;
                }
                catch (Exception ex)
                {
                    // 记录意外异常但继续监控
                    Debug.WriteLine($"进程监控异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 检查进程变化并触发相应事件
        /// </summary>
        private void CheckProcessChanges()
        {
            try
            {
                // 获取当前运行的进程
                var currentProcesses = Process.GetProcessesByName(processName);
                var currentProcessDict = currentProcesses.ToDictionary(p => p.Id);

                // 检查新启动的进程
                foreach (var process in currentProcesses)
                {
                    var id = process.Id;
                    if (runningProcesses.ContainsKey(id)) continue;
                    // 新进程启动
                    runningProcesses[id] = new ProcessInfo(id, processName);
                    ProcessStarted?.Invoke(this, new ProcessEventArgs(process));
                }

                // 检查已关闭的进程
                var stoppedIds = new List<int>();
                foreach (var id in from kvp in runningProcesses let id = kvp.Key where !currentProcessDict.ContainsKey(id) let processInfo = kvp.Value select id)
                {
                    ProcessStopped?.Invoke(this, new ProcessEventArgs(id, processName));
                    stoppedIds.Add(id);
                }

                // 从跟踪列表中移除已停止的进程
                foreach (var id in stoppedIds)
                {
                    runningProcesses.Remove(id);
                }

                // 更新当前运行进程信息
                foreach (var process in currentProcesses)
                {
                    var id = process.Id;
                    if (runningProcesses.TryGetValue(id, out var info))
                    {
                        info.LastSeen = DateTime.Now;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"检查进程变化时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新当前运行的进程列表
        /// </summary>
        private void UpdateRunningProcesses()
        {
            runningProcesses.Clear();

            try
            {
                var processes = Process.GetProcessesByName(processName);
                foreach (var process in processes)
                {
                    runningProcesses[process.Id] = new ProcessInfo(process.Id, processName);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"更新进程列表时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放托管和非托管资源
        /// </summary>
        private void Dispose(bool disposing)
        {
            if (!disposing) return;
            Stop();
            cts.Dispose();
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~ProcessListener()
        {
            Dispose(false);
        }
    }

    /// <summary>
    /// 进程信息类，用于跟踪进程状态
    /// </summary>
    internal class ProcessInfo(int processId, string processName)
    {
        public int ProcessId { get; } = processId;
        public string ProcessName { get; } = processName;
        public DateTime FirstSeen { get; } = DateTime.Now;
        public DateTime LastSeen { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 提供进程事件的信息
    /// </summary>
    public class ProcessEventArgs : EventArgs
    {
        /// <summary>
        /// 获取相关的进程。如果进程已停止，可能为null
        /// </summary>
        public Process? Process { get; }

        /// <summary>
        /// 获取进程ID
        /// </summary>
        public int ProcessId { get; }

        /// <summary>
        /// 获取进程名称
        /// </summary>
        public string ProcessName { get; }

        /// <summary>
        /// 获取事件发生的时间
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 初始化进程事件参数
        /// </summary>
        /// <param name="process">相关的进程</param>
        public ProcessEventArgs(Process process)
        {
            Process = process;
            ProcessId = process.Id;
            ProcessName = process.ProcessName;
            Timestamp = DateTime.Now;
        }

        /// <summary>
        /// 初始化已停止进程的事件参数
        /// </summary>
        /// <param name="processId">进程ID</param>
        /// <param name="processName">进程名称</param>
        public ProcessEventArgs(int processId, string processName)
        {
            Process = null;
            ProcessId = processId;
            ProcessName = processName;
            Timestamp = DateTime.Now;
        }
    }
}

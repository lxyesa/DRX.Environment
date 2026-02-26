using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Shared;

namespace Drx.Sdk.Network.Http.Commands
{
    /// <summary>
    /// 交互式命令控制台 - 简化版本，使用 Logger 输出，支持基本命令输入与历史。
    /// </summary>
    public class InteractiveCommandConsole
    {
        private readonly DrxHttpServer _server;
        private string _currentInput = "";
        private readonly List<string> _commandHistory = new();
        private readonly Stack<string> _undoStack = new();
        private bool _isRunning = false;
        private CancellationTokenSource? _cts;
        private TaskCompletionSource<bool>? _stoppedTcs;

        public InteractiveCommandConsole(DrxHttpServer server)
        {
            _server = server ?? throw new ArgumentNullException(nameof(server));
        }

        public async Task StartAsync()
        {
            _isRunning = true;
            _cts = new CancellationTokenSource();
            _stoppedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            
            Logger.Info("========== 命令控制台已启动 ==========");
            Logger.Info("输入命令或 'help' 获取帮助, 'exit' 退出");
            // 绿色提示符
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("[USER_INPUT] > ");
            Console.ResetColor();

            try
            {
                while (_isRunning && !(_cts?.IsCancellationRequested ?? false))
                {
                    try
                    {
                        await HandleInputAsync();
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex) { Logger.Error($"控制台错误: {ex.Message}"); }
                }
            }
            finally
            {
                _isRunning = false;
                Logger.Info("========== 命令控制台已停止 ==========");
                try { _stoppedTcs?.TrySetResult(true); } catch { }
            }
        }

        private async Task HandleInputAsync()
        {
            // 使用系统默认的控制台输入，自动显示用户输入的文本
            Task<string?> readLineTask = Task.Run(() => Console.ReadLine());
            var token = _cts?.Token ?? CancellationToken.None;
            var completed = await Task.WhenAny(readLineTask, Task.Delay(Timeout.Infinite, token));
            
            if (completed != readLineTask) return;
            
            _currentInput = readLineTask.Result ?? string.Empty;
            
            if (!string.IsNullOrEmpty(_currentInput))
            {
                await HandleEnter();
            }
        }

        private async Task HandleEnter()
        {
            if (string.IsNullOrWhiteSpace(_currentInput)) return;

            var command = _currentInput.Trim();
            if (command.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                _isRunning = false;
                return;
            }

            if (!_commandHistory.Contains(command)) _commandHistory.Add(command);

            try
            {
                Logger.Info($"[SUBMIT] {command}");
                var result = await _server.SubmitCommandAndWaitAsync(command, 30000).ConfigureAwait(false);
                Logger.Info($"[RESULT] {result}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[ERROR] {ex.Message}");
            }

            _currentInput = "";
            _undoStack.Clear();
        }

        public void Stop()
        {
            try
            {
                _isRunning = false;
                _cts?.Cancel();
            }
            catch { }
        }

        public async Task StopAsync(int millisecondsTimeout = 5000)
        {
            try
            {
                _isRunning = false;
                _cts?.Cancel();
                if (_stoppedTcs != null)
                {
                    var t = _stoppedTcs.Task;
                    if (await Task.WhenAny(t, Task.Delay(millisecondsTimeout)) == t)
                        await t;
                }
            }
            catch { }
        }
    }
}

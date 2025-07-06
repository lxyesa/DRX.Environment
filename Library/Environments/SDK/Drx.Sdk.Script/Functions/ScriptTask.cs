using System;
using System.Collections.Concurrent;
using Drx.Sdk.Script.Attributes;
using Drx.Sdk.Script.Interfaces;

namespace Drx.Sdk.Script.Functions;

[ScriptClass("task")]
public class ScriptTask : IScript
{
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> _tasks = new ConcurrentDictionary<string, CancellationTokenSource>();


    // TODO: 启动一个新的任务，返回任务 UID，任务 UID 可以用于取消任务
    // JS: 
    // var uid = Task.start(function(self) { console.log('Hello, World!'); }, 1000, 10);
    // 参数:
    // - action: function(self: any): void - 要执行的函数
    // - interval: number - 任务执行间隔，单位毫秒
    // - count: number - 任务执行次数(-1 表示无限次)
    // Lua:
    // local uid = Task.start(function() print('Hello, World!') end, 1000, 10)
    // 参数:
    // - action: function() - 要执行的函数
    // - interval: number - 任务执行间隔，单位毫秒
    // - count: number - 任务执行次数(-1 表示无限次)
    // 启动一个新的任务，返回任务 UID，任务 UID 可以用于取消任务，任务会异步执行，不会阻塞当前线程

    public static string start(dynamic action, int interval, int count)
    {
        // 确保在 JavaScript 上下文中调用
        if (LuaScriptLoader.IsLuaContext)
        {
            throw new InvalidOperationException("此方法只能在 JavaScript 环境中调用");
        }

        return StartInternal(new Action(() => {
            try {
                ((dynamic)action).Call(action); // 使用 Call 方法调用 JavaScript 函数
            }
            catch (Exception) {
                // 如果 Call 失败，尝试直接调用
                ((dynamic)action)();
            }
        }), interval, count);
    }

    private static string StartInternal(Action action, int interval, int count)
    {
        var uid = Guid.NewGuid().ToString();
        var cts = new CancellationTokenSource();
        _tasks[uid] = cts;

        System.Console.WriteLine($"[Task {uid}] 已创建任务");
        System.Console.WriteLine($"[Task {uid}] 执行间隔: {interval}ms, 计划执行次数: {(count == -1 ? "无限" : count.ToString())}");

        Task.Run(async () =>
        {
            try
            {
                int executionCount = 0;
                while (!cts.Token.IsCancellationRequested && (count == -1 || executionCount < count))
                {
                    try 
                    {
                        action();
                        executionCount++;
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"[Task {uid}] 单次执行出错: {ex.Message}");
                    }
                    await Task.Delay(interval, cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                System.Console.WriteLine($"[Task {uid}] 任务被取消");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[Task {uid}] 执行过程中发生异常: {ex.Message}");
                System.Console.WriteLine($"[Task {uid}] 异常堆栈: {ex.StackTrace}");
            }
            finally
            {
                _tasks.TryRemove(uid, out _);
                cts.Dispose();
                System.Console.WriteLine($"[Task {uid}] 任务已结束，资源已释放");
            }
        }, cts.Token);

        return uid;
    }

    // TODO: 取消一个任务
    // JS: Task.cancel(uid);
    // 参数:
    // - uid: string - 任务 UID
    // JS: Task.cancel(uid);
    // 参数:
    // - uid: string - 任务 UID
    // Lua: Task.cancel(uid)
    // 参数:
    // - uid: string - 任务 UID
    public static void cancel(string uid)
    {
        if (_tasks.TryGetValue(uid, out var cts))
        {
            cts.Cancel();
            _tasks.TryRemove(uid, out _);
        }
    }

    // TODO: 等待函数执行完成
    // JS:
    // Task.wait(function(self) { console.log('Hello, World!'); });
    // 参数:
    // - action: function(self: any): void - 要执行的函数
    // Lua:
    // Task.wait(function() print('Hello, World!') end)
    // 参数:
    // - action: function() - 要执行的函数
    // 在 任何环境（JavaScript、Lua）中，等待函数执行完成（阻塞当前线程），直到函数执行完成
    // 注意：等待函数执行完成会阻塞当前线程，直到函数执行完成，不要在主线程中调用
    public static void wait(Action action)
    {
        action();
    }

    // TODO: 异步执行一个函数
    // JS:
    // Task.run(function(self) { console.log('Hello, World!'); });
    // 参数:
    // - action: function(self: any): void - 要执行的函数
    // Lua:
    // Task.run(function() print('Hello, World!') end)
    // 参数:
    // - action: function() - 要执行的函数
    // 在 任何环境（JavaScript、Lua）中，异步执行一个函数，不会阻塞当前线程，哪怕其中运行了 Task.wait、Task.sleep 等阻塞函数
    public static void run(Action action)
    {
        Task.Run(action);
    }

    // TODO: 等待指定时间
    // JS: Task.sleep(1000);
    // 参数:
    // - milliseconds: number - 等待时间，单位毫秒
    // Lua: Task.sleep(1000)
    // 参数:
    // - milliseconds: number - 等待时间，单位毫秒
    public static void sleep(int milliseconds)
    {
        Thread.Sleep(milliseconds);
    }
}

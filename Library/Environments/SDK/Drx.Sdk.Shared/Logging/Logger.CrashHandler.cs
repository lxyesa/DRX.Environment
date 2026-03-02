using System.Text;

namespace Drx.Sdk.Shared;

/// <summary>
/// 全局未处理异常与未观察任务异常的捕获处理。
/// </summary>
public static partial class Logger
{
    /// <summary>
    /// 注册全局异常处理器，确保崩溃信息输出到控制台与日志队列。
    /// </summary>
    private static void RegisterCrashHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            try
            {
                if (e.ExceptionObject is System.Exception ex)
                    LogFatalException("UnhandledException", ex);
                else
                    Console.Error.WriteLine($"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] [FATAL] Unhandled non-Exception object: {e.ExceptionObject}");
            }
            catch
            {
                try { Console.Error.WriteLine($"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] [FATAL] Failed to handle UnhandledException."); }
                catch { }
            }
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            try
            {
                LogFatalException("UnobservedTaskException", e.Exception);
                e.SetObserved();
            }
            catch
            {
                try { Console.Error.WriteLine($"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] [FATAL] Failed to handle UnobservedTaskException."); }
                catch { }
            }
        };
    }

    private static void LogFatalException(string source, System.Exception ex)
    {
        try
        {
            var sb = new StringBuilder(512);
            var current = ex;
            int depth = 0;
            while (current != null && depth < 10)
            {
                sb.AppendLine($"{current.GetType().FullName}: {current.Message}");
                if (!string.IsNullOrEmpty(current.StackTrace))
                    sb.AppendLine(current.StackTrace);
                current = current.InnerException;
                depth++;
                if (current != null)
                    sb.AppendLine("---- Inner Exception ----");
            }

            var message = sb.ToString().TrimEnd();

            try
            {
                Console.Error.WriteLine($"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] [FATAL] {source} - {message}");
            }
            catch { }

            WriteImmediately(LogLevel.Fatal, source, message, null, source, 0);
        }
        catch
        {
            try { Console.Error.WriteLine($"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] [FATAL] Exception while logging fatal exception."); }
            catch { }
        }
    }
}

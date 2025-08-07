using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Drx.Sdk.Shared.JavaScript
{
    /// <summary>
    /// 脚本日志记录器，支持多级别日志、性能监控、输出到控制台/文件/自定义
    /// </summary>
    public class ScriptLogger
    {
        public enum LogLevel { Debug, Info, Warning, Error }

        public LogLevel Level { get; set; } = LogLevel.Info;
        public string LogFilePath { get; set; }
        public Action<string> CustomOutput { get; set; }

        private readonly object _lock = new();

        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            if (level < Level) return;
            string log = FormatLog(message, level);
            lock (_lock)
            {
                if (CustomOutput != null)
                    CustomOutput(log);
                else
                    Console.WriteLine(log);

                if (!string.IsNullOrEmpty(LogFilePath))
                    File.AppendAllText(LogFilePath, log + Environment.NewLine, Encoding.UTF8);
            }
        }

        public void LogException(Exception ex, ScriptExecutionContext? context = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[Exception] {ex.GetType().Name}: {ex.Message}");
            if (context != null)
                sb.AppendLine($"Context: {context}");
            sb.AppendLine(ex.StackTrace);
            Log(sb.ToString(), LogLevel.Error);
        }

        public void LogPerformance(ScriptExecutionContext context)
        {
            if (context == null) return;
            string msg = $"[Performance] File: {context.FilePath}, Duration: {context.Duration}, Retry: {context.RetryCount}, Start: {context.StartTime}";
            Log(msg, LogLevel.Debug);
        }

        private string FormatLog(string message, LogLevel level)
        {
            return $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";
        }
    }
}
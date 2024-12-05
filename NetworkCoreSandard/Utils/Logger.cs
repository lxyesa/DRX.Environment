using System;
using System.Text;

namespace NetworkCoreStandard.Utils
{
  public static class Logger
  {
    private static readonly object _lock = new();
    private static readonly StringBuilder _sharedBuilder = new(128);
    private const string DateTimeFormat = "yyyy/MM/dd HH:mm:ss";

    // 保持原有方法签名以兼容现有代码
    public static void Log(string header, string message)
    {
      LogInternal(LogLevel.Info, header, message);
    }

    // 新增重载方法
    public static void Log(LogLevel level, string header, string message)
    {
      LogInternal(level, header, message);
    }

    private static void LogInternal(LogLevel level, string header, string message)
    {
      if (string.IsNullOrEmpty(message)) return;

      lock (_lock)
      {
        try
        {
          Console.OutputEncoding = Encoding.UTF8;
          _sharedBuilder.Clear();
          _sharedBuilder.Append('[')
                       .Append(DateTime.Now.ToString(DateTimeFormat))
                       .Append(']');

          if (!string.IsNullOrEmpty(header))
          {
            _sharedBuilder.Append(" [")
                         .Append(header)
                         .Append(']');
          }

          _sharedBuilder.Append(" [")
                       .Append(level.ToString().ToUpper())
                       .Append("] ")
                       .Append(message);

          // 根据日志级别使用不同的控制台颜色
          var originalColor = Console.ForegroundColor;
          Console.ForegroundColor = GetLogLevelColor(level);
          Console.WriteLine(_sharedBuilder.ToString());
          Console.ForegroundColor = originalColor;
        }
        catch (Exception)
        {
          // 发生异常时回退到基础日志记录
          Console.WriteLine($"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] [ERROR] 日志系统异常");
        }
      }
    }

    private static ConsoleColor GetLogLevelColor(LogLevel level) => level switch
    {
      LogLevel.Debug => ConsoleColor.Gray,
      LogLevel.Info => ConsoleColor.White,
      LogLevel.Warning => ConsoleColor.Yellow,
      LogLevel.Error => ConsoleColor.Red,
      LogLevel.Fatal => ConsoleColor.DarkRed,
      _ => ConsoleColor.White
    };
  }

  public enum LogLevel
  {
    Debug,
    Info,
    Warning,
    Error,
    Fatal
  }
}
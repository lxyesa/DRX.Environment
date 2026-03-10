// Copyright (c) DRX SDK — Paperclip 计时器脚本桥接层
// 职责：将 System.Diagnostics.Stopwatch 能力导出到 JS/TS 脚本
// 关键依赖：System.Diagnostics

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace DrxPaperclip.Hosting;

/// <summary>
/// 计时器脚本桥接层。提供高精度计时（Stopwatch）及延时等待等静态 API。
/// </summary>
public static class TimerBridge
{
    #region Stopwatch

    /// <summary>创建并启动一个 Stopwatch 实例。</summary>
    public static Stopwatch startNew()
        => Stopwatch.StartNew();

    /// <summary>获取已用时间（毫秒）。</summary>
    public static double elapsedMs(Stopwatch sw)
    {
        ArgumentNullException.ThrowIfNull(sw);
        return sw.Elapsed.TotalMilliseconds;
    }

    /// <summary>获取已用时间（秒）。</summary>
    public static double elapsedSeconds(Stopwatch sw)
    {
        ArgumentNullException.ThrowIfNull(sw);
        return sw.Elapsed.TotalSeconds;
    }

    /// <summary>停止计时。</summary>
    public static void stop(Stopwatch sw)
    {
        ArgumentNullException.ThrowIfNull(sw);
        sw.Stop();
    }

    /// <summary>重置并重新启动。</summary>
    public static void restart(Stopwatch sw)
    {
        ArgumentNullException.ThrowIfNull(sw);
        sw.Restart();
    }

    /// <summary>重置计时器。</summary>
    public static void reset(Stopwatch sw)
    {
        ArgumentNullException.ThrowIfNull(sw);
        sw.Reset();
    }

    /// <summary>获取 Stopwatch 高精度时间戳。</summary>
    public static long getTimestamp()
        => Stopwatch.GetTimestamp();

    #endregion

    #region 延时

    /// <summary>异步延时指定毫秒。</summary>
    public static Task delay(int milliseconds)
        => Task.Delay(milliseconds);

    /// <summary>同步阻塞当前线程指定毫秒（慎用）。</summary>
    public static void sleep(int milliseconds)
        => Thread.Sleep(milliseconds);

    #endregion

    #region 简易计时

    /// <summary>
    /// 简易计时——返回当前高精度时间戳（毫秒），用于手动 diff 计算。
    /// </summary>
    public static double nowMs()
        => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency * 1000.0;

    #endregion
}

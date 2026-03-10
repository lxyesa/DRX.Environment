namespace DrxPaperclip.Hosting;

/// <summary>
/// Paperclip 内置全局函数。由 <see cref="EngineBootstrap"/> 在引擎初始化后
/// 通过 RegisterGlobal 注册为 JS 顶层 print() / pause() 函数。
/// </summary>
public static class BuiltinFunctionsBridge
{
    /// <summary>
    /// 向控制台输出空字符串。
    /// JS/TS 调用：<c>print()</c>
    /// </summary>
    public static void print()
    {
        print(null);
    }

    /// <summary>
    /// 向控制台输出文本，不附加换行符。
    /// JS/TS 调用：<c>print("hello")</c>
    /// </summary>
    public static void print(object? value)
    {
        Console.Write(value?.ToString() ?? "");
    }

    /// <summary>
    /// 暂停执行，等待用户按下任意键后继续。
    /// JS/TS 调用：<c>pause()</c> 或 <c>pause("提示文字")</c>
    /// </summary>
    public static void pause()
    {
        pause(null);
    }

    /// <summary>
    /// 暂停执行，等待用户按下任意键后继续。
    /// JS/TS 调用：<c>pause()</c> 或 <c>pause("提示文字")</c>
    /// </summary>
    public static void pause(object? prompt = null)
    {
        var message = prompt?.ToString();
        Console.Write(string.IsNullOrEmpty(message) ? "Press any key to continue..." : message);
        Console.ReadKey(intercept: true);
        Console.WriteLine();
    }
}

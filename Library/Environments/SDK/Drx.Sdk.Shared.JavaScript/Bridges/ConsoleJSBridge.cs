// Copyright (c) DRX SDK
using System;
using System.Threading.Tasks;
using Drx.Sdk.Shared.JavaScript.Attributes;

namespace Drx.Sdk.Shared.JavaScript.Bridges
{
    /// <summary>
    /// Console 的 JavaScript 桥接层，暴露 console.log/info/debug/warn/error。
    /// 直接输出到标准输出/标准错误，不经过 Logger。
    /// </summary>
    [ScriptExport("console", ScriptExportType.StaticClass)]
    public static class ConsoleJSBridge
    {
        [ScriptExport]
        public static void log(object? message)
        {
            Console.WriteLine(message?.ToString() ?? "null");
        }

        [ScriptExport]
        public static void info(object? message)
        {
            Console.WriteLine(message?.ToString() ?? "null");
        }

        [ScriptExport]
        public static void debug(object? message)
        {
            Console.WriteLine(message?.ToString() ?? "null");
        }

        [ScriptExport]
        public static void warn(object? message)
        {
            Console.Error.WriteLine(message?.ToString() ?? "null");
        }

        [ScriptExport]
        public static void error(object? message)
        {
            Console.Error.WriteLine(message?.ToString() ?? "null");
        }

        [ScriptExport]
        public static Task logAsync(object? message)
            => Console.Out.WriteLineAsync(message?.ToString() ?? "null");

        [ScriptExport]
        public static Task infoAsync(object? message)
            => Console.Out.WriteLineAsync(message?.ToString() ?? "null");

        [ScriptExport]
        public static Task debugAsync(object? message)
            => Console.Out.WriteLineAsync(message?.ToString() ?? "null");

        [ScriptExport]
        public static Task warnAsync(object? message)
            => Console.Error.WriteLineAsync(message?.ToString() ?? "null");

        [ScriptExport]
        public static Task errorAsync(object? message)
            => Console.Error.WriteLineAsync(message?.ToString() ?? "null");
    }
}

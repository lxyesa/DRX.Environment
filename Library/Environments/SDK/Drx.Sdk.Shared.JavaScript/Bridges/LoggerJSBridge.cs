// Copyright (c) DRX SDK
using System;
using System.Threading.Tasks;
using Drx.Sdk.Shared.JavaScript.Attributes;

namespace Drx.Sdk.Shared.JavaScript.Bridges
{
    /// <summary>
    /// Logger 的 JavaScript 桥接层，暴露 Logger.info/warn/error/debug 方法给 JS。
    /// </summary>
    [ScriptExport("Logger", ScriptExportType.StaticClass)]
    public static class LoggerJSBridge
    {
        [ScriptExport]
        public static void info(object message)
        {
            // 将Object转换为字符串，null 则显示 "null"
            Logger.Info(message?.ToString() ?? "null");
        }

        [ScriptExport]
        public static void warn(object message)
        {
            Logger.Warn(message?.ToString() ?? "null");
        }

        [ScriptExport]
        public static void error(object message)
        {
            Logger.Error(message?.ToString() ?? "null");
        }

        [ScriptExport]
        public static void debug(object message)
        {
            Logger.Debug(message?.ToString() ?? "null");
        }

        [ScriptExport]
        public static void log(object message)
        {
            Logger.Info(message?.ToString() ?? "null");
        }

        [ScriptExport]
        public static Task infoAsync(object message)
            => Task.Run(() => Logger.Info(message?.ToString() ?? "null"));

        [ScriptExport]
        public static Task warnAsync(object message)
            => Task.Run(() => Logger.Warn(message?.ToString() ?? "null"));

        [ScriptExport]
        public static Task errorAsync(object message)
            => Task.Run(() => Logger.Error(message?.ToString() ?? "null"));

        [ScriptExport]
        public static Task debugAsync(object message)
            => Task.Run(() => Logger.Debug(message?.ToString() ?? "null"));

        [ScriptExport]
        public static Task logAsync(object message)
            => Task.Run(() => Logger.Info(message?.ToString() ?? "null"));
    }

    /// <summary>
    /// logger 的兼容别名导出，避免旧脚本迁移成本。
    /// </summary>
    [ScriptExport("logger", ScriptExportType.StaticClass)]
    public static class LoggerJsCompatBridge
    {
        [ScriptExport]
        public static void info(object message) => LoggerJSBridge.info(message);

        [ScriptExport]
        public static void warn(object message) => LoggerJSBridge.warn(message);

        [ScriptExport]
        public static void error(object message) => LoggerJSBridge.error(message);

        [ScriptExport]
        public static void debug(object message) => LoggerJSBridge.debug(message);

        [ScriptExport]
        public static void log(object message) => LoggerJSBridge.log(message);

        [ScriptExport]
        public static Task infoAsync(object message) => LoggerJSBridge.infoAsync(message);

        [ScriptExport]
        public static Task warnAsync(object message) => LoggerJSBridge.warnAsync(message);

        [ScriptExport]
        public static Task errorAsync(object message) => LoggerJSBridge.errorAsync(message);

        [ScriptExport]
        public static Task debugAsync(object message) => LoggerJSBridge.debugAsync(message);

        [ScriptExport]
        public static Task logAsync(object message) => LoggerJSBridge.logAsync(message);
    }
}
// Copyright (c) DRX SDK
using System;
using DRX.Framework;
using Drx.Sdk.Shared.JavaScript;

namespace Drx.Sdk.Shared.JavaScript.Bridges
{
    /// <summary>
    /// Logger 的 JavaScript 桥接层，暴露 log.info/warn/error/debug 方法给 JS。
    /// </summary>
    [ScriptExport("logger", ScriptExportType.StaticClass)]
    public static class LoggerJSBridge
    {
        [ScriptExport]
        public static void info(object message)
        {
            // 将Object转换为字符串，无论是什么类型，如果是数字类，就转换为int的字符串形式
            if (message is not string && message is not null)
            {
                message = message.ToString();
            }
            else if (message == null)
            {
                message = "null";
            }
            Logger.Info(message?.ToString()!);
        }

        [ScriptExport]
        public static void warn(object message)
        {
            Logger.Warn(message.ToString()!);
        }

        [ScriptExport]
        public static void error(object message)
        {
            Logger.Error(message.ToString()!);
        }

        [ScriptExport]
        public static void debug(object message)
        {
            Logger.Debug(message.ToString()!);
        }
    }
}
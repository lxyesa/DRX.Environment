using System;
using System.Collections.Generic;

namespace Drx.Sdk.Network.Http.Configs
{
    /// <summary>
    /// 命令解析结果类
    /// </summary>
    public class CommandParseResult
    {
        /// <summary>
        /// 解析是否成功
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// 提取的参数字典（参数名 -> 值）
        /// </summary>
        public Dictionary<string, string> Parameters { get; set; } = new();

        /// <summary>
        /// 错误消息
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}

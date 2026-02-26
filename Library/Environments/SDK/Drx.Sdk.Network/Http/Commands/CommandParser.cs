using System;
using System.Collections.Generic;
using System.Linq;
using Drx.Sdk.Network.Http.Configs;

namespace Drx.Sdk.Network.Http.Commands
{
    /// <summary>
    /// 命令格式解析器，将命令格式字符串解析为可验证和处理的结构。
    /// 支持必须参数（&lt;name&gt;）和可选参数（[name]）。
    /// </summary>
    public class CommandParser
    {
        /// <summary>
        /// 参数信息
        /// </summary>
        private class ParameterInfo
        {
            public string Name { get; set; }
            public bool IsRequired { get; set; }
        }

        public string CommandName { get; private set; }
        private List<ParameterInfo> _parameters = new();
        private string _originalFormat;

        /// <summary>
        /// 构造函数，解析命令格式
        /// </summary>
        public CommandParser(string format)
        {
            if (string.IsNullOrWhiteSpace(format))
                throw new ArgumentNullException(nameof(format));

            _originalFormat = format;
            ParseFormat(format);
        }

        /// <summary>
        /// 解析命令格式字符串
        /// 示例："ban &lt;username&gt; &lt;reason&gt; [duration]"
        /// </summary>
        private void ParseFormat(string format)
        {
            var tokens = format.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
                throw new ArgumentException("命令格式不能为空");

            CommandName = tokens[0];

            // 解析参数
            for (int i = 1; i < tokens.Length; i++)
            {
                var token = tokens[i];

                if (token.StartsWith("<") && token.EndsWith(">"))
                {
                    // 必须参数
                    var paramName = token.Substring(1, token.Length - 2);
                    _parameters.Add(new ParameterInfo { Name = paramName, IsRequired = true });
                }
                else if (token.StartsWith("[") && token.EndsWith("]"))
                {
                    // 可选参数
                    var paramName = token.Substring(1, token.Length - 2);
                    _parameters.Add(new ParameterInfo { Name = paramName, IsRequired = false });
                }
                else
                {
                    throw new ArgumentException($"无效的参数格式: {token}，应使用 &lt;name&gt; 或 [name]");
                }
            }
        }

        /// <summary>
        /// 解析输入的参数列表
        /// </summary>
        public CommandParseResult Parse(List<string> args)
        {
            var result = new CommandParseResult { IsValid = true, Parameters = new Dictionary<string, string>() };

            var requiredCount = _parameters.Count(p => p.IsRequired);
            var providedCount = args.Count;

            // 检查必须参数数量
            if (providedCount < requiredCount)
            {
                result.IsValid = false;
                result.ErrorMessage = $"参数不足，需要至少 {requiredCount} 个参数，但只提供了 {providedCount} 个。";
                return result;
            }

            if (providedCount > _parameters.Count)
            {
                result.IsValid = false;
                result.ErrorMessage = $"参数过多，最多需要 {_parameters.Count} 个参数，但提供了 {providedCount} 个。";
                return result;
            }

            // 将参数值映射到参数名
            for (int i = 0; i < args.Count; i++)
            {
                if (i < _parameters.Count)
                {
                    result.Parameters[_parameters[i].Name] = args[i];
                }
            }

            return result;
        }

        /// <summary>
        /// 获取用法帮助文本
        /// </summary>
        public string GetUsage()
        {
            return _originalFormat;
        }
    }
}

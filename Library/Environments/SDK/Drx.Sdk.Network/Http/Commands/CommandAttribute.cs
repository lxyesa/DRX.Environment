using System;

namespace Drx.Sdk.Network.Http.Commands
{
    /// <summary>
    /// 命令处理方法特性，用于标注命令处理方法。
    /// 支持通过属性进行注册，命令格式规范：
    /// - 使用 &lt;&gt; 表示必须参数，例如：&lt;username&gt;
    /// - 使用 [] 表示可选参数，例如：[duration]
    /// 示例：[Command("ban &lt;username&gt; &lt;reason&gt; [duration]", "user:封禁用户", "用于管理员操作")]
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class CommandAttribute : Attribute
    {
        /// <summary>
        /// 命令格式字符串，定义命令名称和参数。
        /// 示例："unban &lt;username&gt;"、"ban &lt;username&gt; &lt;reason&gt; [duration]"
        /// </summary>
        public string Format { get; }

        /// <summary>
        /// 命令分类，用于分组和帮助文本。
        /// 示例："user:管理用户"、"helper:系统帮助"
        /// </summary>
        public string Category { get; }

        /// <summary>
        /// 命令描述，简短说明该命令的作用。
        /// 示例："仅限开发者使用"、"解除用户的封禁状态"
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public CommandAttribute(string format, string category, string description)
        {
            Format = format ?? throw new ArgumentNullException(nameof(format));
            Category = category ?? "";
            Description = description ?? "";
        }
    }
}

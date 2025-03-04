using DRX.Framework.Common.Base;
using DRX.Framework.Common.Utility;

namespace DRX.Framework.Common.Args
{
    /// <summary>
    /// 执行参数的基类，包含公共参数。
    /// </summary>
    public class RegexParam : BaseArgs<RegexParam>
    {
        /// <summary>
        /// 输入字符串。
        /// </summary>
        public string Input { get; set; } = string.Empty;

        /// <summary>
        /// 匹配模式。
        /// </summary>
        public DRXRegex.RegexMode Mode { get; set; } = DRXRegex.RegexMode.IsEmail;

        /// <summary>
        /// 返回模式。
        /// </summary>
        public DRXRegex.ReturnMode ReturnMode { get; set; } = DRXRegex.ReturnMode.ReturnMatchedStrings;

        /// <summary>
        /// 返回过滤器。
        /// </summary>
        public DRXRegex.ReturnFilter Filter { get; set; } = DRXRegex.ReturnFilter.Default;

        /// <summary>
        /// 指定的匹配索引（仅在过滤器为Specify时使用）。
        /// </summary>
        public int SpecifyIndex { get; set; } = 0;

        /// <summary>
        /// 附加参数1（如匹配两个字符串之间的开始字符串）。
        /// </summary>
        public string Param1 { get; set; } = string.Empty;

        /// <summary>
        /// 附加参数2（如匹配两个字符串之间的结束字符串）。
        /// </summary>
        public string Param2 { get; set; } = string.Empty;
    }
}

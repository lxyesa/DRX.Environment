using DRX.Framework.Common.Args;
using System.Text.RegularExpressions;

namespace DRX.Framework.Common.Utility
{
    /// <summary>
    /// 提供各种正则表达式匹配和处理的方法
    /// </summary>
    public static class DRXRegex
    {
        /// <summary>
        /// 执行正则表达式匹配操作。
        /// 使用 <see cref="ExecuteParametersWithParams"/> 或 <see cref="RegexParam"/> 进行参数传递。
        /// </summary>
        /// <param name="parameters">执行参数对象。</param>
        /// <returns>根据返回模式和过滤器返回不同类型的结果。</returns>
        public static object? Execute(RegexParam parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters), "执行参数不能为空。");

            string input = parameters.Input;
            if (string.IsNullOrEmpty(input) && parameters.ReturnMode != ReturnMode.ReturnBoolean)
            {
                throw new ArgumentException("输入字符串不能为空。");
            }

            string? pattern = GetPattern(parameters.Mode, parameters.Param1, parameters.Param2);
            if (pattern == null && parameters.Mode != RegexMode.MatchContentBetweenStrings)
            {
                throw new ArgumentException("无效的匹配模式或缺少必要的参数。");
            }

            MatchCollection matches = Regex.Matches(input, pattern);

            switch (parameters.Filter)
            {
                case ReturnFilter.Default:
                    if (matches.Count > 0)
                    {
                        return ProcessMatch(matches[0], parameters.ReturnMode);
                    }
                    return GetDefaultReturn(parameters.ReturnMode);

                case ReturnFilter.All:
                    List<object> allResults = new List<object>();
                    foreach (var match in matches.Cast<Match>())
                    {
                        allResults.Add(ProcessMatch(match, parameters.ReturnMode));
                    }
                    return allResults.ToArray();

                case ReturnFilter.Specify:
                    if (parameters.SpecifyIndex < 0 || parameters.SpecifyIndex >= matches.Count)
                    {
                        throw new ArgumentOutOfRangeException(nameof(parameters.SpecifyIndex), "指定的索引超出匹配范围。");
                    }
                    return ProcessMatch(matches[parameters.SpecifyIndex], parameters.ReturnMode);

                default:
                    throw new NotSupportedException("不支持的返回过滤器。");
            }
        }

        /// <summary>
        /// 根据匹配模式生成相应的正则表达式模式
        /// </summary>
        /// <param name="mode">匹配模式</param>
        /// <param name="param1">附加参数1</param>
        /// <param name="param2">附加参数2</param>
        /// <returns>正则表达式模式字符串</returns>
        private static string? GetPattern(RegexMode mode, string param1, string param2)
        {
            switch (mode)
            {
                case RegexMode.MatchContentBetweenStrings:
                    if (string.IsNullOrEmpty(param1) || string.IsNullOrEmpty(param2))
                        throw new ArgumentException("匹配两个字符串之间的内容需要提供 param1 和 param2。");
                    // 使用非贪婪模式匹配两个字符串之间的内容
                    return $"{Regex.Escape(param1)}(.*?){Regex.Escape(param2)}";

                case RegexMode.IsLowercase:
                    return @"^[a-z]+$";

                case RegexMode.IsUppercase:
                    return @"^[A-Z]+$";

                case RegexMode.IsAlphabet:
                    return @"^[A-Za-z]+$";

                case RegexMode.IsNumeric:
                    return @"^\d+$";

                case RegexMode.IsAlphanumeric:
                    return @"^[A-Za-z0-9]+$";

                case RegexMode.IsChinese:
                    return @"^[\u4e00-\u9fa5]+$";

                case RegexMode.ContainsChinese:
                    return @"[\u4e00-\u9fa5]";

                case RegexMode.ContainsString:
                    if (string.IsNullOrEmpty(param1))
                        throw new ArgumentException("匹配是否存在字符串需要提供 param1。");
                    return Regex.Escape(param1);

                // 新增的匹配模式
                case RegexMode.IsEmail:
                    return @"^[^\s@]+@[^\s@]+\.[^\s@]+$";

                case RegexMode.IsURL:
                    return @"^(https?:\/\/)?([\w\-])+\.{1}([a-zA-Z]{2,63})([\/\w\-.?&=]*)*\/?$";

                case RegexMode.IsPhoneNumber:
                    return @"^\+?\d{10,15}$";

                case RegexMode.IsIPAddress:
                    return @"^(25[0-5]|2[0-4]\d|[01]?\d\d?)\."
                         + @"(25[0-5]|2[0-4]\d|[01]?\d\d?)\."
                         + @"(25[0-5]|2[0-4]\d|[01]?\d\d?)\."
                         + @"(25[0-5]|2[0-4]\d|[01]?\d\d?)$";

                case RegexMode.IsHexadecimal:
                    return @"^[0-9A-Fa-f]+$";

                case RegexMode.IsGUID:
                    return @"^\{?[0-9A-Fa-f]{8}\-[0-9A-Fa-f]{4}\-[0-9A-Fa-f]{4}\-[0-9A-Fa-f]{4}\-[0-9A-Fa-f]{12}\}?$";

                case RegexMode.IsDate:
                    return @"^(19|20)\d\d[- /.](0[1-9]|1[012])[- /.](0[1-9]|[12][0-9]|3[01])$";

                case RegexMode.IsPasswordStrong:
                    // 至少8个字符，包含大写、小写字母，数字和特殊字符
                    return @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$";

                case RegexMode.IsFilePath:
                    return @"^(?:[a-zA-Z]\:)?(?:\\|\/)?(?:[\w\-\.]+(?:\\|\/)?)*$";

                default:
                    return null;
            }
        }

        /// <summary>
        /// 处理单个匹配结果
        /// </summary>
        /// <param name="match">匹配项</param>
        /// <param name="returnMode">返回模式</param>
        /// <returns>处理后的结果</returns>
        private static object ProcessMatch(Match match, ReturnMode returnMode)
        {
            switch (returnMode)
            {
                case ReturnMode.ReturnBoolean:
                    return match.Success;
                case ReturnMode.ReturnMatchedStrings:
                    return match.Value;
                case ReturnMode.ReturnMatchPositions:
                    return match.Index;
                case ReturnMode.ReturnStringAfterRemoval:
                    // 需要结合上下文使用，这里仅返回匹配项
                    return match.Value;
                default:
                    throw new NotSupportedException("不支持的返回模式。");
            }
        }

        /// <summary>
        /// 获取默认返回值
        /// </summary>
        /// <param name="returnMode">返回模式</param>
        /// <returns>默认返回值</returns>
        private static object? GetDefaultReturn(ReturnMode returnMode)
        {
            switch (returnMode)
            {
                case ReturnMode.ReturnBoolean:
                    return false;
                case ReturnMode.ReturnMatchedStrings:
                    return null;
                case ReturnMode.ReturnMatchPositions:
                    return null;
                case ReturnMode.ReturnStringAfterRemoval:
                    return string.Empty;
                default:
                    throw new NotSupportedException("不支持的返回模式。");
            }
        }

        /// <summary>
        /// 定义各种返回过滤器
        /// </summary>
        public enum ReturnFilter
        {
            /// <summary>
            /// 默认（返回第一个匹配的内容）
            /// </summary>
            Default,

            /// <summary>
            /// 全部（返回全部匹配的内容）
            /// </summary>
            All,

            /// <summary>
            /// 指定（返回指定索引的匹配内容）
            /// </summary>
            Specify
        }

        /// <summary>
        /// 定义各种匹配模式
        /// </summary>
        public enum RegexMode
        {
            /// <summary>
            /// 匹配两个字符串之间的内容
            /// </summary>
            MatchContentBetweenStrings,

            /// <summary>
            /// 匹配是否为小写字母
            /// </summary>
            IsLowercase,

            /// <summary>
            /// 匹配是否为大写字母
            /// </summary>
            IsUppercase,

            /// <summary>
            /// 匹配是否为字母(包括大小写)
            /// </summary>
            IsAlphabet,

            /// <summary>
            /// 匹配是否为数字
            /// </summary>
            IsNumeric,

            /// <summary>
            /// 匹配是否为数字或字母(包括大小写)
            /// </summary>
            IsAlphanumeric,

            /// <summary>
            /// 匹配是否为中文字符
            /// </summary>
            IsChinese,

            /// <summary>
            /// 匹配是否存在中文字符
            /// </summary>
            ContainsChinese,

            /// <summary>
            /// 匹配是否存在字符串
            /// </summary>
            ContainsString,

            /// <summary>
            /// 匹配是否为有效的电子邮件地址
            /// </summary>
            IsEmail,

            /// <summary>
            /// 匹配是否为有效的URL
            /// </summary>
            IsURL,

            /// <summary>
            /// 匹配是否为有效的电话号码
            /// </summary>
            IsPhoneNumber,

            /// <summary>
            /// 匹配是否为有效的IP地址
            /// </summary>
            IsIPAddress,

            /// <summary>
            /// 匹配是否为十六进制数
            /// </summary>
            IsHexadecimal,

            /// <summary>
            /// 匹配是否为有效的GUID
            /// </summary>
            IsGUID,

            /// <summary>
            /// 匹配是否为有效的日期格式 (YYYY-MM-DD)
            /// </summary>
            IsDate,

            /// <summary>
            /// 匹配是否为强密码 (至少8个字符，包含大写、小写字母，数字和特殊字符)
            /// </summary>
            IsPasswordStrong,

            /// <summary>
            /// 匹配是否为有效的文件路径
            /// </summary>
            IsFilePath
        }

        /// <summary>
        /// 定义各种返回模式
        /// </summary>
        public enum ReturnMode
        {
            /// <summary>
            /// 返回布尔值
            /// </summary>
            ReturnBoolean,

            /// <summary>
            /// 返回匹配到的字符串
            /// </summary>
            ReturnMatchedStrings,

            /// <summary>
            /// 返回匹配到的字符串在原字符串中的位置
            /// </summary>
            ReturnMatchPositions,

            /// <summary>
            /// 返回移除了匹配到的字符串后的字符串
            /// </summary>
            ReturnStringAfterRemoval
        }
    }

    /// <summary>
    /// 定义各种匹配模式
    /// </summary>
    public enum RegexMode
    {
        /// <summary>
        /// 匹配两个字符串之间的内容
        /// </summary>
        MatchContentBetweenStrings,

        /// <summary>
        /// 匹配是否为小写字母
        /// </summary>
        IsLowercase,

        /// <summary>
        /// 匹配是否为大写字母
        /// </summary>
        IsUppercase,

        /// <summary>
        /// 匹配是否为字母(包括大小写)
        /// </summary>
        IsAlphabet,

        /// <summary>
        /// 匹配是否为数字
        /// </summary>
        IsNumeric,

        /// <summary>
        /// 匹配是否为数字或字母(包括大小写)
        /// </summary>
        IsAlphanumeric,

        /// <summary>
        /// 匹配是否为中文字符
        /// </summary>
        IsChinese,

        /// <summary>
        /// 匹配是否存在中文字符
        /// </summary>
        ContainsChinese,

        /// <summary>
        /// 匹配是否存在字符串
        /// </summary>
        ContainsString,

        /// <summary>
        /// 匹配是否为有效的电子邮件地址
        /// </summary>
        IsEmail,

        /// <summary>
        /// 匹配是否为有效的URL
        /// </summary>
        IsURL,

        /// <summary>
        /// 匹配是否为有效的电话号码
        /// </summary>
        IsPhoneNumber,

        /// <summary>
        /// 匹配是否为有效的IP地址
        /// </summary>
        IsIPAddress,

        /// <summary>
        /// 匹配是否为十六进制数
        /// </summary>
        IsHexadecimal,

        /// <summary>
        /// 匹配是否为有效的GUID
        /// </summary>
        IsGUID,

        /// <summary>
        /// 匹配是否为有效的日期格式 (YYYY-MM-DD)
        /// </summary>
        IsDate,

        /// <summary>
        /// 匹配是否为强密码 (至少8个字符，包含大写、小写字母，数字和特殊字符)
        /// </summary>
        IsPasswordStrong,

        /// <summary>
        /// 匹配是否为有效的文件路径
        /// </summary>
        IsFilePath
    }

    /// <summary>
    /// 定义各种返回模式
    /// </summary>
    public enum ReturnMode
    {
        /// <summary>
        /// 返回布尔值
        /// </summary>
        ReturnBoolean,

        /// <summary>
        /// 返回匹配到的字符串
        /// </summary>
        ReturnMatchedStrings,

        /// <summary>
        /// 返回匹配到的字符串在原字符串中的位置
        /// </summary>
        ReturnMatchPositions,

        /// <summary>
        /// 返回移除了匹配到的字符串后的字符串
        /// </summary>
        ReturnStringAfterRemoval
    }
}

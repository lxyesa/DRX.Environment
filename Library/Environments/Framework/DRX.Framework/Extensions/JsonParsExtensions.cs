using DRX.Framework.Common.Utility;

namespace DRX.Framework.Extensions
{
    public static class JsonParsExtensions
    {
        /// <summary>
        /// 从 JSON 字符串中获取值
        /// </summary>
        public static T? GetValueFromJson<T>(this string json, string path, T? defaultValue = default)
        {
            var jsonNode = json.ParseToNode();
            return jsonNode.GetValue(path, defaultValue);
        }
    }
}

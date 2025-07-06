using DRX.Framework.Common.Base;
using System.Text.Json.Nodes;

namespace DRX.Framework.Common.Models
{
    public class DRXPacket : BasePacket<DRXPacket>
    {
        // 正确的 Action 属性定义
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// 将具体属性添加到 JSON 对象中。
        /// </summary>
        /// <param name="jsonObject">JSON 对象。</param>
        protected override void AddPropertiesToJson(JsonObject jsonObject)
        {
            jsonObject.Add("action", Action);
        }

        /// <summary>
        /// 从 JSON 对象中设置具体属性。
        /// </summary>
        /// <param name="jsonObject">JSON 对象。</param>
        protected override void SetPropertiesFromJson(JsonObject jsonObject)
        {
            if (jsonObject.TryGetPropertyValue("action", out var value))
            {
                Action = value?.GetValue<string>() ?? string.Empty;
            }
        }
    }
}

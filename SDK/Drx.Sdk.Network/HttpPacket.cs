using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Drx.Sdk.Network
{
    public class HttpPacket : BasePacket<HttpPacket>
    {
        protected override void AddPropertiesToJson(JsonObject jsonObject)
        {
            // 添加 HttpPacket 特有的属性到 JSON 对象
        }

        protected override void SetPropertiesFromJson(JsonObject jsonObject)
        {
            // 从 JSON 对象中设置 HttpPacket 特有的属性
        }
    }
}

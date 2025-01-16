using DRX.Framework.Common.Base;
using System.Text.Json.Nodes;

namespace NDVServerLib
{
    public class MyPacket : BasePacket<MyPacket>
    {
        protected override void AddPropertiesToJson(JsonObject jsonObject)
        {
            throw new NotImplementedException();
        }

        protected override void SetPropertiesFromJson(JsonObject jsonObject)
        {
            throw new NotImplementedException();
        }
    }
}

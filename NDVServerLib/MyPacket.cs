using NetworkCoreStandard.Common.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

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

using System.Text.Json.Nodes;

namespace DRX.Framework.Common.Models
{
    public class ArgObject : Dictionary<string, object>
    {
        internal JsonObject ToJsonObject()
        {
            var jsonObject = new JsonObject();
            foreach (var kvp in this)
            {
                jsonObject.Add(kvp.Key, JsonValue.Create(kvp.Value));
            }
            return jsonObject;
        }

        internal static ArgObject FromJsonObject(JsonObject jsonObject)
        {
            var packetObject = new ArgObject();
            foreach (var kvp in jsonObject)
            {
                if (kvp.Value is JsonValue jsonValue)
                {
                    var value = jsonValue.GetValue<object>();
                    if (value != null)
                    {
                        packetObject.Add(kvp.Key, value);
                    }
                }
                else if (kvp.Value != null)
                {
                    packetObject.Add(kvp.Key, kvp.Value);
                }
            }
            return packetObject;
        }

        public object[]? ToArray(string key)
        {
            if (TryGetValue(key, out var value))
            {
                if (value is JsonArray jsonArray)
                {
                    return jsonArray.Select(item => (object)item).ToArray();
                }
                else if (value is object[] objectArray)
                {
                    return objectArray;
                }
            }
            return null;
        }
    }
}

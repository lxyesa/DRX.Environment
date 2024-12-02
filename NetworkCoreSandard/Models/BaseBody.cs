using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetworkCoreStandard.Models;

public class BaseBody
{
    [JsonPropertyName("msg")]
    public string? Message { get; set; }
    
    // 用于存储动态添加的字段
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

using System.Text.Json.Serialization;

namespace Drx.Sdk.Network.Http.Models;

/// <summary>
/// 前端资源变更事件。
/// 可直接用于 SSE 或其他文本协议序列化。
/// </summary>
public sealed class DevAssetChangedEvent
{
    /// <summary>
    /// 事件唯一标识。
    /// </summary>
    public string EventId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 事件时间戳（UTC 毫秒）。
    /// </summary>
    public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>
    /// 变更集合。
    /// </summary>
    public List<AssetChangeItem> ChangeSet { get; set; } = new();

    /// <summary>
    /// 建议客户端动作。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DevRecommendedAction RecommendedAction { get; set; } = DevRecommendedAction.Reload;
}

/// <summary>
/// 资源变更项。
/// </summary>
public sealed class AssetChangeItem
{
    /// <summary>
    /// 相对资源路径。
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 变更类型。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AssetChangeKind Kind { get; set; } = AssetChangeKind.Changed;

    /// <summary>
    /// 资源类型。
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public AssetKind AssetType { get; set; } = AssetKind.Other;
}

/// <summary>
/// 变更动作建议。
/// </summary>
public enum DevRecommendedAction
{
    Reload = 0,
    CssReplace = 1
}

/// <summary>
/// 资源变更类型。
/// </summary>
public enum AssetChangeKind
{
    Created = 0,
    Changed = 1,
    Deleted = 2,
    Renamed = 3
}

/// <summary>
/// 资源分类。
/// </summary>
public enum AssetKind
{
    Html = 0,
    Css = 1,
    Js = 2,
    Other = 3
}

using System;
using System.Text.Json.Serialization;

namespace Drx.Sdk.Network.Http.ResourceManagement
{
    /// <summary>
    /// 资源索引条目：描述一个被索引的文件或子索引的元数据
    /// </summary>
    public class ResourceIndexEntry
    {
        /// <summary>
        /// 资源唯一标识符（UUID，自动生成）
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// 文件相对路径（相对于 resources 根目录，使用 / 分隔符）
        /// </summary>
        [JsonPropertyName("path")]
        public string RelativePath { get; set; } = string.Empty;

        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        [JsonPropertyName("size")]
        public long Size { get; set; }

        /// <summary>
        /// 文件最后修改时间的 UTC ticks
        /// </summary>
        [JsonPropertyName("lastModified")]
        public long LastModifiedTicks { get; set; }

        /// <summary>
        /// 文件内容哈希（xxHash64 十六进制字符串）
        /// </summary>
        [JsonPropertyName("hash")]
        public string? Hash { get; set; }

        /// <summary>
        /// 是否为子索引引用（主索引使用此标志追踪子索引）
        /// </summary>
        [JsonPropertyName("isSubIndex")]
        public bool IsSubIndex { get; set; }

        /// <summary>
        /// 子索引文件名（仅当 IsSubIndex 为 true 时有值）
        /// </summary>
        [JsonPropertyName("subIndexName")]
        public string? SubIndexName { get; set; }
    }
}

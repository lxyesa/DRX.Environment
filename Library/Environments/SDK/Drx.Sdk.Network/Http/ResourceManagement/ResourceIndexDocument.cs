using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Drx.Sdk.Network.Http.ResourceManagement
{
    /// <summary>
    /// 资源索引文档：持久化到磁盘的 JSON 索引结构
    /// </summary>
    public class ResourceIndexDocument
    {
        /// <summary>
        /// 索引版本号，用于兼容性校验
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// 索引创建时间 (UTC)
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 索引最后更新时间 (UTC)
        /// </summary>
        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 索引所属的资源根目录绝对路径
        /// </summary>
        [JsonPropertyName("rootPath")]
        public string RootPath { get; set; } = string.Empty;

        /// <summary>
        /// 文件条目索引表：key 为资源 ID，value 为索引条目
        /// </summary>
        [JsonPropertyName("entries")]
        public Dictionary<string, ResourceIndexEntry> Entries { get; set; } = new();

        /// <summary>
        /// 被追踪的子索引列表：key 为子索引名称，value 为子索引文件的相对路径
        /// </summary>
        [JsonPropertyName("subIndexes")]
        public Dictionary<string, string> SubIndexes { get; set; } = new();
    }
}

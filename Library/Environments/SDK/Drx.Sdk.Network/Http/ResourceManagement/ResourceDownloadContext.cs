using System;
using System.Collections.Generic;

namespace Drx.Sdk.Network.Http.ResourceManagement
{
    /// <summary>
    /// 资源下载上下文：在下载回调的各个阶段传递，包含完整的下载状态和控制标志
    /// </summary>
    public class ResourceDownloadContext
    {
        /// <summary>
        /// 当前下载状态
        /// </summary>
        public DownloadStatus Status { get; set; }

        /// <summary>
        /// 下载的文件名（从 Content-Disposition 或 URL 解析）
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// 目标保存目录
        /// </summary>
        public string TargetDirectory { get; set; } = string.Empty;

        /// <summary>
        /// 文件最终保存的完整路径（在 BeforeSave 阶段可修改，AfterSave 阶段为实际保存路径）
        /// </summary>
        public string? SavedFilePath { get; set; }

        /// <summary>
        /// 已下载的字节数（在 Downloading 阶段持续更新）
        /// </summary>
        public long DownloadedBytes { get; set; }

        /// <summary>
        /// 文件总大小（字节，来自 Content-Length 响应头，未知时为 -1）
        /// </summary>
        public long TotalBytes { get; set; } = -1;

        /// <summary>
        /// 剩余字节数（仅在 Downloading 且 TotalBytes 已知时有效）
        /// </summary>
        public long RemainingBytes => TotalBytes > 0 ? Math.Max(0, TotalBytes - DownloadedBytes) : -1;

        /// <summary>
        /// 下载进度百分比（0.0 ~ 100.0），TotalBytes 未知时为 -1
        /// </summary>
        public double ProgressPercentage => TotalBytes > 0 ? Math.Round((double)DownloadedBytes / TotalBytes * 100.0, 2) : -1;

        /// <summary>
        /// 文件内容哈希（SHA256，在 DownloadCompleted/BeforeSave/AfterSave 阶段可用）
        /// </summary>
        public string? FileHash { get; set; }

        /// <summary>
        /// 期望的文件哈希（用于完整性校验，若服务器返回了哈希或调用方指定）
        /// </summary>
        public string? ExpectedHash { get; set; }

        /// <summary>
        /// 哈希校验是否通过（true = 匹配，false = 不匹配，null = 未校验）
        /// </summary>
        public bool? HashVerified { get; set; }

        /// <summary>
        /// 文件 MIME 类型（来自 Content-Type 响应头）
        /// </summary>
        public string? ContentType { get; set; }

        /// <summary>
        /// 服务器返回的 ETag（可用于缓存校验和断点续传）
        /// </summary>
        public string? ETag { get; set; }

        /// <summary>
        /// 服务器文件最后修改时间（来自 Last-Modified 响应头）
        /// </summary>
        public DateTimeOffset? LastModified { get; set; }

        /// <summary>
        /// 服务器元数据（来自响应头 X-MetaData，解析为字典）
        /// </summary>
        public Dictionary<string, object>? ServerMetadata { get; set; }

        /// <summary>
        /// 取消标志：在 BeforeDownload/Downloading/BeforeSave 阶段设置为 true 可终止下载流程
        /// </summary>
        public bool Cancel { get; set; }

        /// <summary>
        /// 取消原因（可选，当 Cancel 被设置为 true 时附带原因信息）
        /// </summary>
        public string? CancelReason { get; set; }

        /// <summary>
        /// HTTP 响应状态码
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// 下载源 URL
        /// </summary>
        public string? SourceUrl { get; set; }

        /// <summary>
        /// 临时文件路径（下载进行中的中间文件，仅内部使用）
        /// </summary>
        internal string? TempFilePath { get; set; }

        /// <summary>
        /// 下载开始时间 (UTC)
        /// </summary>
        public DateTime DownloadStartTime { get; set; } = DateTime.UtcNow;
    }
}

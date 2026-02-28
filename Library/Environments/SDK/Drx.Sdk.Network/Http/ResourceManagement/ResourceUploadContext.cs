using System;
using System.Collections.Generic;

namespace Drx.Sdk.Network.Http.ResourceManagement
{
    /// <summary>
    /// 资源上传上下文：在上传回调的各个阶段传递，包含完整的上传状态和控制标志
    /// </summary>
    public class ResourceUploadContext
    {
        /// <summary>
        /// 当前上传状态
        /// </summary>
        public UploadStatus Status { get; set; }

        /// <summary>
        /// 上传的文件名（原始文件名）
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// 目标存储目录（相对于 resources 根目录）
        /// </summary>
        public string TargetDirectory { get; set; } = string.Empty;

        /// <summary>
        /// 文件最终保存的完整路径（仅在 BeforeSave/AfterSave 阶段可用）
        /// </summary>
        public string? SavedFilePath { get; set; }

        /// <summary>
        /// 已上传的字节数（仅在 Uploading 阶段有效）
        /// </summary>
        public long UploadedBytes { get; set; }

        /// <summary>
        /// 文件总大小（字节，若客户端提供了 Content-Length 则有值，否则为 -1）
        /// </summary>
        public long TotalBytes { get; set; } = -1;

        /// <summary>
        /// 剩余字节数（仅在 Uploading 且 TotalBytes 已知时有效）
        /// </summary>
        public long RemainingBytes => TotalBytes > 0 ? Math.Max(0, TotalBytes - UploadedBytes) : -1;

        /// <summary>
        /// 上传进度百分比（0.0 ~ 100.0），TotalBytes 未知时为 -1
        /// </summary>
        public double ProgressPercentage => TotalBytes > 0 ? Math.Round((double)UploadedBytes / TotalBytes * 100.0, 2) : -1;

        /// <summary>
        /// 文件内容哈希（仅在 UploadCompleted/BeforeSave/AfterSave 阶段可用）
        /// </summary>
        public string? FileHash { get; set; }

        /// <summary>
        /// 文件 MIME 类型
        /// </summary>
        public string? ContentType { get; set; }

        /// <summary>
        /// 文件类型验证结果（true = 通过验证，null = 未验证）
        /// </summary>
        public bool? FileTypeValid { get; set; }

        /// <summary>
        /// 用户自定义元数据（来自客户端请求头 X-MetaData）
        /// </summary>
        public Dictionary<string, object>? UserMetadata { get; set; }

        /// <summary>
        /// 取消标志：在 BeforeUpload/Uploading/BeforeSave 阶段设置为 true 可终止上传流程
        /// </summary>
        public bool Cancel { get; set; }

        /// <summary>
        /// 取消原因（可选，当 Cancel 被设置为 true 时附带原因信息）
        /// </summary>
        public string? CancelReason { get; set; }

        /// <summary>
        /// 是否将文件添加到资源索引（默认 true，在回调中可设置为 false 以跳过索引追加）
        /// </summary>
        public bool ShouldAddToIndex { get; set; } = true;

        /// <summary>
        /// 上传对应的资源索引条目 ID（仅在 AfterSave 阶段可用）
        /// </summary>
        public string? ResourceId { get; set; }

        /// <summary>
        /// 客户端 IP 地址
        /// </summary>
        public string? ClientIp { get; set; }

        /// <summary>
        /// 上传开始时间 (UTC)
        /// </summary>
        public DateTime UploadStartTime { get; set; } = DateTime.UtcNow;
    }
}

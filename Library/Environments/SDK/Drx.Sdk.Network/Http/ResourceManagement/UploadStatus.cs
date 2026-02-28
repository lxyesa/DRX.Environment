namespace Drx.Sdk.Network.Http.ResourceManagement
{
    /// <summary>
    /// 文件上传生命周期状态
    /// </summary>
    public enum UploadStatus
    {
        /// <summary>
        /// 上传开始前（可在此阶段取消上传或验证文件类型）
        /// </summary>
        BeforeUpload,

        /// <summary>
        /// 上传进行中（包含进度信息：已上传字节、总字节、剩余字节、进度百分比）
        /// </summary>
        Uploading,

        /// <summary>
        /// 上传数据接收完毕
        /// </summary>
        UploadCompleted,

        /// <summary>
        /// 文件即将保存到磁盘（可在此阶段取消保存或修改保存路径）
        /// </summary>
        BeforeSave,

        /// <summary>
        /// 文件已保存到磁盘（包含最终保存路径和文件哈希）
        /// </summary>
        AfterSave
    }
}

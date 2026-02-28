namespace Drx.Sdk.Network.Http.ResourceManagement
{
    /// <summary>
    /// 文件下载生命周期状态
    /// </summary>
    public enum DownloadStatus
    {
        /// <summary>
        /// 下载开始前（已收到响应头，可在此阶段取消下载或验证元数据）
        /// </summary>
        BeforeDownload,

        /// <summary>
        /// 下载进行中（包含进度信息：已下载字节、总字节、剩余字节、进度百分比）
        /// </summary>
        Downloading,

        /// <summary>
        /// 下载数据接收完毕（网络流读取完成，文件哈希已计算）
        /// </summary>
        DownloadCompleted,

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

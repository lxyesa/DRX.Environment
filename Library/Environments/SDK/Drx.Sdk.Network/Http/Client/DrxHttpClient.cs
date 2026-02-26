namespace Drx.Sdk.Network.Http
{
    /// <summary>
    /// HTTP 客户端，用于发送各类 HTTP 请求并支持流式上传与下载。
    /// </summary>
    /// <remarks>
    /// DrxHttpClient 已拆分为多个 partial 文件以便于维护。
    /// 各部分功能如下：
    /// - DrxHttpClient.cs (base): 核心定义与类声明
    /// - DrxHttpClient.Base.cs: 字段、常量、构造函数、核心属性
    /// - DrxHttpClient.Cookies.cs: 会话管理、Cookie 导入/导出、ASCII 编码
    /// - DrxHttpClient.Send.cs: SendAsync 重载、SendAsyncInternal、便捷方法（Get/Post/Put/Delete）、ParseMethod、BuildUrl
    /// - DrxHttpClient.Upload.cs: 文件上传及带元数据的上传
    /// - DrxHttpClient.Download.cs: 文件下载与流式下载
    /// - DrxHttpClient.Queue.cs: 后台请求队列处理
    /// - DrxHttpClient.Helpers.cs: 默认头设置、超时配置、资源释放
    ///
    /// 已提取的独立类型：
    /// - HttpRequestTask (Core/Client/): 内部请求队列条目
    /// - ProgressableStreamContent (Performance/): 进度流包装
    /// </remarks>
    /// <seealso cref="Protocol.HttpRequest"/>
    /// <seealso cref="Protocol.HttpResponse"/>
    public partial class DrxHttpClient : IAsyncDisposable
    {
    }
}

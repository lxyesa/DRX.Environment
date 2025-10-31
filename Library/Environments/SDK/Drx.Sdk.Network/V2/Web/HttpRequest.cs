using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Specialized;
using System.Net;
using System.IO;

namespace Drx.Sdk.Network.V2.Web
{
    /// <summary>
    /// 表示 HTTP 请求
    /// </summary>
    public class HttpRequest : IDisposable
    {
        /// <summary>
        /// HTTP 方法 (GET, POST, etc.)
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// 请求路径
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// 完整请求 URL（可选，通常在客户端调用时使用）
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// 查询参数
        /// </summary>
        public NameValueCollection Query { get; set; }

        /// <summary>
        /// 请求头
        /// </summary>
        public NameValueCollection Headers { get; set; }

        /// <summary>
        /// 请求体 (字符串形式)
        /// </summary>
        public string Body { get; set; }

        /// <summary>
        /// 请求体 (字节数组形式)
        /// </summary>
        public byte[] BodyBytes { get; set; }

        /// <summary>
        /// 请求体 (对象形式)
        /// </summary>
        public object BodyObject { get; set; }

        /// <summary>
        /// 专用于传递 JSON 字符串的属性（兼容旧调用方）
        /// </summary>
        public string BodyJson { get; set; }

        /// <summary>
        ///兼容旧调用方的额外字节包字段
        /// </summary>
        public byte[] ExtraDataPack { get; set; }

        /// <summary>
        ///远程端点
        /// </summary>
        public IPEndPoint RemoteEndPoint { get; set; }

        /// <summary>
        /// 路径参数，从路径模板中提取
        /// </summary>
        public Dictionary<string, string> PathParameters { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 上传文件描述(当接口支持文件上传时使用)。
        /// 如果设置, 将以 multipart/form-data 的方式把该文件流上传到服务器。
        /// </summary>
        public UploadFileDescriptor UploadFile { get; set; }

        /// <summary>
        /// 文件上传描述类型
        /// </summary>
        public class UploadFileDescriptor
        {
            /// <summary>
            /// 要上传的流，调用方负责流的生命周期（是否关闭由调用方决定）
            /// </summary>
            public Stream Stream { get; set; }

            /// <summary>
            /// 上传到服务端的文件名
            /// </summary>
            public string FileName { get; set; }

            /// <summary>
            /// 表单字段名，默认 "file"
            /// </summary>
            public string FieldName { get; set; } = "file";

            /// <summary>
            /// 可选：当需要报告进度时传入一个 IProgress<long>
            /// </summary>
            public IProgress<long> Progress { get; set; }

            /// <summary>
            /// 可选：取消令牌，用于在上传时取消操作
            /// </summary>
            public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
        }

        /// <summary>
        /// 原始的 HttpListenerContext（仅当需要访问底层上下文时设置）
        /// </summary>
        public HttpListenerContext ListenerContext { get; set; }

        public void Dispose()
        {
            // 不在此处自动关闭 UploadFile.Stream，由创建者决定何时关闭
        }
    }
}

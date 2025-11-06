using System;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Specialized;
using System.Dynamic;
using System.Collections.Generic;
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
        /// HTTP 方法 (GET, POST 等)
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// 请求路径
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// 完整 URL（可选），客户端直连或代理场景下使用
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// 查询参数
        /// </summary>
        public NameValueCollection Query { get; set; }

        /// <summary>
        /// 请求头
        /// </summary>
        public NameValueCollection Headers { get; set; }

        /// <summary>
        /// 请求体（字符串形式）
        /// </summary>
        public string? Body { get; set; }

        /// <summary>
        /// 请求内容的便捷字段（Content），为动态对象，调用者可使用 Content.XYZ 任意扩展字段。
        /// 默认实现为 ExpandoObject，解析器会在解析到文本体时将其赋值到 Content.Text。
        /// </summary>
        public dynamic Content { get; set; } = new ExpandoObject();

        /// <summary>
        /// 请求体（字节数组形式）
        /// </summary>
        public byte[]? BodyBytes { get; set; }

        /// <summary>
        /// 请求体（对象形式，可序列化为 JSON）
        /// </summary>
        public object? BodyObject { get; set; }

        /// <summary>
        /// 原始或缓存的 JSON 字符串（如果适用）
        /// </summary>
        public string BodyJson { get; set; }

        /// <summary>
        /// 附加数据包（字节数组），用于承载自定义二进制扩展数据
        /// </summary>
        public byte[] ExtraDataPack { get; set; }

        /// <summary>
        /// 远端终结点信息（可用于记录来源或回传）
        /// </summary>
        public IPEndPoint RemoteEndPoint { get; set; }

        /// <summary>
        /// 路径参数（从模板化路径中提取的命名参数）
        /// </summary>
        public Dictionary<string, string> PathParameters { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 表单字段集合，multipart/form-data 或 application/x-www-form-urlencoded 场景使用
        /// </summary>
        public NameValueCollection Form { get; set; } = new NameValueCollection();

        /// <summary>
        /// 上传文件描述（当需要以 multipart/form-data 上传文件时使用）
        /// </summary>
        public UploadFileDescriptor? UploadFile { get; set; }

        /// <summary>
        /// 上传文件描述符
        /// </summary>
        public class UploadFileDescriptor
        {
            /// <summary>
            /// 待上传的流（调用方负责流的生命周期管理，通常不在框架内部关闭）
            /// </summary>
            public Stream Stream { get; set; }

            /// <summary>
            /// 上传时使用的文件名
            /// </summary>
            public string FileName { get; set; }

            /// <summary>
            /// 表单字段名，默认 "file"
            /// </summary>
            public string FieldName { get; set; } = "file";

            /// <summary>
            /// 可选：上传进度回调 (已上传字节数)
            /// </summary>
            public IProgress<long>? Progress { get; set; }

            /// <summary>
            /// 可选：取消令牌，用于取消上传操作
            /// </summary>
            public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
        }

        /// <summary>
        /// 原始的 HttpListenerContext（仅在实现基于 HttpListener 的服务器时设置）
        /// </summary>
        public HttpListenerContext ListenerContext { get; set; }

        /// <summary>
        /// 设置单个默认请求头（仅在当前 Headers 中不存在该键时添加）
        /// </summary>
        /// <param name="name">头名称</param>
        /// <param name="value">头值</param>
        public void SetDefaultHeader(string name, string value)
        {
            if (string.IsNullOrEmpty(name) || value == null) return;
            try
            {
                // NameValueCollection 的索引器在键不存在时返回 null
                if (Headers[name] == null)
                {
                    Headers.Add(name, value);
                }
            }
            catch { }
        }

        /// <summary>
        /// 批量设置默认请求头（仅在目标头不存在时添加）。接受 NameValueCollection 或 IDictionary&lt;string,string&gt;。
        /// </summary>
        /// <param name="defaults">默认头集合</param>
        public void SetDefaultHeaders(System.Collections.Specialized.NameValueCollection defaults)
        {
            if (defaults == null) return;
            try
            {
                foreach (string key in defaults)
                {
                    if (string.IsNullOrEmpty(key)) continue;
                    if (Headers[key] == null)
                    {
                        Headers.Add(key, defaults[key]);
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// 批量设置默认请求头（仅在目标头不存在时添加），接受 IDictionary&lt;string,string&gt;。
        /// </summary>
        /// <param name="defaults">默认头字典</param>
        public void SetDefaultHeaders(System.Collections.Generic.IDictionary<string, string> defaults)
        {
            if (defaults == null) return;
            try
            {
                foreach (var kv in defaults)
                {
                    if (string.IsNullOrEmpty(kv.Key)) continue;
                    if (Headers[kv.Key] == null)
                    {
                        Headers.Add(kv.Key, kv.Value);
                    }
                }
            }
            catch { }
        }

        public void Dispose()
        {
            // 如果需要，在此处释放 UploadFile.Stream 等资源（目前保留给调用方管理流生命周期）
        }
    }
}

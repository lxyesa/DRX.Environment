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
        public NameValueCollection Query { get; set; } = new NameValueCollection();

        /// <summary>
        /// 请求头
        /// </summary>
        public NameValueCollection Headers { get; set; } = new NameValueCollection();

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
        /// 客户端地址信息的便捷结构体，包含 IP、Port、EndPoint、Host 等字段，路由可直接使用该字段获取客户端网络信息。
        /// </summary>
        public Address ClientAddress { get; set; }

        /// <summary>
        /// 表示客户端地址信息的结构体
        /// </summary>
        public struct Address
        {
            /// <summary>
            /// IP 地址字符串（例如 192.168.1.1 或 ::1）
            /// </summary>
            public string? Ip { get; set; }

            /// <summary>
            /// 远端端口号
            /// </summary>
            public int Port { get; set; }

            /// <summary>
            /// 原始的 IPEndPoint（如果可用）
            /// </summary>
            public IPEndPoint? EndPoint { get; set; }

            /// <summary>
            /// 请求头中的 Host 字段（如果存在）
            /// </summary>
            public string? Host { get; set; }

            /// <summary>
            /// 是否为 IPv6 地址
            /// </summary>
            public bool IsIPv6 { get; set; }

            /// <summary>
            /// 返回可读的字符串表示
            /// </summary>
            /// <returns>优先返回 EndPoint.ToString，否则返回 Ip</returns>
            public override string ToString()
            {
                if (EndPoint != null) return EndPoint.ToString();
                return Ip ?? string.Empty;
            }

            /// <summary>
            /// 从 IPEndPoint 与 Headers 构造 Address 实例
            /// </summary>
            /// <param name="ep">远端终结点（可能为 null）</param>
            /// <param name="headers">请求头（可能为 null）</param>
            /// <returns>填充好的 Address 结构体</returns>
            public static Address FromEndPoint(IPEndPoint? ep, System.Collections.Specialized.NameValueCollection? headers)
            {
                var a = new Address();
                if (ep != null)
                {
                    a.EndPoint = ep;
                    a.Ip = ep.Address.ToString();
                    a.Port = ep.Port;
                    try { a.IsIPv6 = ep.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6; } catch { a.IsIPv6 = false; }
                }
                try { a.Host = headers?[("Host")] ?? headers?[("host")]; } catch { a.Host = null; }
                return a;
            }
        }

        /// <summary>
        /// 路径参数（从模板化路径中提取的命名参数）
        /// </summary>
        public Dictionary<string, string> PathParameters { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 会话对象（如果启用了会话中间件）
        /// </summary>
        public Session? Session { get; set; }

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

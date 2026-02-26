using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using Microsoft.Extensions.Primitives;
using System.Collections.Specialized;
using System.Dynamic;
using System.Net;
using System.IO;
using Drx.Sdk.Network.Http;
using Drx.Sdk.Network.Http.Configs;

namespace Drx.Sdk.Network.Http.Protocol
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
        /// 会话 Id（如果启用了会话中间件）
        /// 
        /// 说明：重构后请求仅携带会话 id，若需获取会话对象可通过 DrxHttpServer.SessionManager 或调用
        /// request.ResolveSession(server) 来获取对应的 Session 实例。
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// 通过服务器实例解析会话 id 为会话对象（若找不到返回 null）。
        /// 这是一个便捷方法，避免调用方直接依赖 SessionManager 内部实现。
        /// </summary>
        public Configs.Session? ResolveSession(DrxHttpServer server)
        {
            if (server == null) return null;
            if (string.IsNullOrEmpty(SessionId)) return null;
            try
            {
                return server.SessionManager.GetSession(SessionId);
            }
            catch
            {
                return null;
            }
        }

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

        /// <summary>
        /// 解析表单数据（支持 application/x-www-form-urlencoded 与 multipart/form-data），
        /// 并把结果填充到 HttpRequest.Form 与 UploadFile 中。
        /// 该方法会读取传入的 bodyStream（可能为 request.InputStream），因此调用方应在调用后
        /// 不要再重复读取该流。
        /// 对于 multipart 的文件部分，当前实现会把第一个文件内容复制到内存流并设置到 UploadFile（内存占用请注意）。
        /// </summary>
        /// <param name="contentType">Content-Type 头（可为 null 或空）</param>
        /// <param name="bodyStream">请求体流（例如 HttpListenerRequest.InputStream）</param>
        /// <param name="encoding">文本编码，默认 UTF8</param>
        /// <returns>Task</returns>
        public async Task ParseFormAsync(string? contentType, Stream bodyStream, Encoding? encoding = null)
        {
            encoding ??= Encoding.UTF8;
            if (string.IsNullOrEmpty(contentType) || bodyStream == null)
                return;

            try
            {
                if (contentType.IndexOf("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    using var sr = new StreamReader(bodyStream, encoding, detectEncodingFromByteOrderMarks: true, bufferSize: 8192, leaveOpen: true);
                    var body = await sr.ReadToEndAsync().ConfigureAwait(false);
                    // 填充 Body 与 BodyBytes
                    Body = body;
                    BodyBytes = encoding.GetBytes(body);
                    Content = new System.Dynamic.ExpandoObject();
                    try { ((System.Collections.Generic.IDictionary<string, object>)Content)["Text"] = body; } catch { }

                    // 使用 QueryHelpers 解析 urlencoded body
                    try
                    {
                        var parsed = QueryHelpers.ParseQuery(body);
                        var form = new NameValueCollection();
                        foreach (var kv in parsed)
                        {
                            StringValues vals = kv.Value;
                            for (int i = 0; i < vals.Count; i++)
                            {
                                try { form.Add(kv.Key, vals[i]); } catch { }
                            }
                        }
                        Form = form;
                    }
                    catch
                    {
                        // 容错：解析失败则保持原有 Form
                    }

                    return;
                }

                if (contentType.IndexOf("multipart/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var mediaType = MediaTypeHeaderValue.Parse(contentType);
                    var boundary = HeaderUtilities.RemoveQuotes(mediaType.Boundary).ToString();
                    var reader = new MultipartReader(boundary, bodyStream);

                    NameValueCollection form = new NameValueCollection();

                    MultipartSection section;
                    while ((section = await reader.ReadNextSectionAsync().ConfigureAwait(false)) != null)
                    {
                        var hasContentDispositionHeader = ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var contentDisposition);
                        if (!hasContentDispositionHeader || contentDisposition == null) continue;

                        // 文件部分
                        var fileNameSegment = contentDisposition.FileName.HasValue ? contentDisposition.FileName : contentDisposition.FileNameStar;
                        var nameSegment = contentDisposition.Name;
                        var fieldName = nameSegment.HasValue ? HeaderUtilities.RemoveQuotes(nameSegment).ToString() : "file";

                        if (!StringSegment.IsNullOrEmpty(fileNameSegment))
                        {
                            var fileName = HeaderUtilities.RemoveQuotes(fileNameSegment).ToString();
                            var ms = new MemoryStream();
                            await section.Body.CopyToAsync(ms).ConfigureAwait(false);
                            ms.Position = 0;
                            UploadFile = new UploadFileDescriptor
                            {
                                Stream = ms,
                                FileName = string.IsNullOrEmpty(fileName) ? "file" : fileName,
                                FieldName = fieldName,
                                CancellationToken = CancellationToken.None,
                                Progress = null
                            };
                        }
                        else
                        {
                            // 普通表单字段
                            using var sr = new StreamReader(section.Body, encoding, detectEncodingFromByteOrderMarks: true, bufferSize: 8192, leaveOpen: true);
                            var value = await sr.ReadToEndAsync().ConfigureAwait(false);
                            try { form.Add(fieldName, value); } catch { }
                        }
                    }

                    if (form.Count > 0) Form = form;
                }
            }
            catch (Exception)
            {
                // 不抛异常以免影响请求流程，调用者可检测 Form/UploadFile 是否被填充
            }
        }

        /// <summary>
        /// 从 Form 中安全获取第一个值（若不存在返回空字符串）。
        /// </summary>
        public string GetFormValue(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            try
            {
                var v = Form[key];
                return v ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        /// <summary>
        /// 从 Form 中获取所有值（若不存在返回空数组）。
        /// </summary>
        public string[] GetFormValues(string key)
        {
            if (string.IsNullOrEmpty(key)) return Array.Empty<string>();
            try
            {
                var vals = Form.GetValues(key);
                return vals ?? Array.Empty<string>();
            }
            catch { return Array.Empty<string>(); }
        }

        /// <summary>
        /// 添加元数据到请求头中
        /// </summary>
        /// <param name="metaData">必须是一个有效的 JSON 字符串</param>
        public void AddMetaData(string metaData)
        {
            this.Headers.Add("X-MetaData", metaData);
        }

        /// <summary>
        /// 获取元数据
        /// </summary>
        /// <returns></returns>
        public string? GetMetaData()
        {
            // 首先尝试从 Headers 中获取
            var metaData = this.Headers["X-MetaData"];

            // 若 header 中不存在，尝试从 multipart/form-data 的表单字段或 urlencoded body 中获取名为 metadata 的值
            if (string.IsNullOrEmpty(metaData))
            {
                try { metaData = this.Form?["metadata"]; } catch { metaData = null; }
            }

            if (string.IsNullOrEmpty(metaData)) return null;

            metaData = metaData.Trim();

            // 优先处理 URL 百分号编码（percent-encoding），例如 %7B %22 %0D%0A 等
            try
            {
                if (metaData.IndexOf('%') >= 0)
                {
                    // 使用 WebUtility.UrlDecode 还原
                    try
                    {
                        var urlDecoded = System.Net.WebUtility.UrlDecode(metaData);
                        if (!string.IsNullOrEmpty(urlDecoded)) return urlDecoded;
                    }
                    catch { /* 容错，继续回退 */ }
                }
            }
            catch { }

            // 如果不是 percent-encoding，尝试 Base64 解码（兼容旧逻辑）
            try
            {
                var base64Bytes = Convert.FromBase64String(metaData);
                return Encoding.UTF8.GetString(base64Bytes);
            }
            catch
            {
                // 既不是 URL 编码也不是 Base64，则返回原始字符串
                return metaData;
            }
        }
    }
}

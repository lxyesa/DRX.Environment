using System.Collections.Specialized;
using System.IO;
using System.Text.Json;
using System.Dynamic;
using System.Collections.Generic;

namespace Drx.Sdk.Network.V2.Web
{
    /// <summary>
    /// 表示 HTTP 响应
    /// </summary>
    public class HttpResponse : IDisposable
    {
        /// <summary>
        /// 状态码
        /// </summary>
        public int StatusCode { get; set; }

        public void Dispose()
        {
            // 释放资源
        }

        /// <summary>
        /// 状态描述
        /// </summary>
        public string StatusDescription { get; set; }

        /// <summary>
        /// 响应头
        /// </summary>
        public NameValueCollection Headers { get; set; }

        /// <summary>
        /// 响应体 (字符串形式)
        /// </summary>
        public string Body { get; set; }

        /// <summary>
        /// 响应内容的便捷字段（Content），为动态对象，调用者可使用 Content.XYZ 任意扩展字段。
        /// 默认实现为 ExpandoObject，且在使用字符串/对象构造器时会填充常用字段（Text/Json/Object）。
        /// </summary>
        public dynamic Content { get; set; }

        /// <summary>
        /// 响应体 (字节数组形式)
        /// </summary>
        public byte[] BodyBytes { get; set; }

        /// <summary>
        /// 响应体 (对象形式)
        /// </summary>
        public object? BodyObject { get; set; }

        /// <summary>
        /// 响应流，用于文件下载等场景，此时 Body 包含 Stream，客户端在处理时应获取响应体，使用自己的 API 处理
        /// </summary>
        public Stream FileStream { get; set; }

        /// <summary>
        /// 可选的带宽限制（以 KB/s 为单位），0 表示不限制
        /// </summary>
        public int BandwidthLimitKb { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public HttpResponse()
        {
            Headers = new NameValueCollection();
            Content = new ExpandoObject();
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public HttpResponse(int statusCode, string body = "", string? statusDescription = null)
        {
            StatusCode = statusCode;
            Body = body;
            Content = new ExpandoObject();
            try { ((IDictionary<string, object>)Content)["Text"] = body; } catch { }
            StatusDescription = statusDescription ?? GetDefaultStatusDescription(statusCode);
            Headers = new NameValueCollection();
        }

        /// <summary>
        /// 构造函数 (字节数组)
        /// </summary>
        public HttpResponse(int statusCode, byte[] bodyBytes, string? statusDescription = null)
        {
            StatusCode = statusCode;
            BodyBytes = bodyBytes;
            Content = new ExpandoObject();
            try { ((IDictionary<string, object>)Content)["Text"] = System.Text.Encoding.UTF8.GetString(bodyBytes); } catch { }
            StatusDescription = statusDescription ?? GetDefaultStatusDescription(statusCode);
            Headers = new NameValueCollection();
        }

        /// <summary>
        /// 构造函数 (对象)
        /// </summary>
        public HttpResponse(int statusCode, object? bodyObject, string? statusDescription = null)
        {
            StatusCode = statusCode;
            BodyObject = bodyObject;
            Content = new ExpandoObject();
            try
            {
                ((IDictionary<string, object>)Content)["Object"] = bodyObject!;
                ((IDictionary<string, object>)Content)["Json"] = bodyObject == null ? string.Empty : JsonSerializer.Serialize(bodyObject);
            }
            catch { }
            StatusDescription = statusDescription ?? GetDefaultStatusDescription(statusCode);
            Headers = new NameValueCollection();
        }

        private string GetDefaultStatusDescription(int statusCode)
        {
            return statusCode switch
            {
                200 => "OK",
                404 => "Not Found",
                500 => "Internal Server Error",
                _ => "Unknown"
            };
        }

        public bool IsSuccessStatusCode => StatusCode >= 200 && StatusCode <= 299;
    }
}

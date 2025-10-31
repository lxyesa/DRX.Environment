using System.Collections.Specialized;

namespace Drx.Sdk.Network.V2.Web
{
    /// <summary>
    /// 表示 HTTP 响应
    /// </summary>
    public class HttpResponse
    {
        /// <summary>
        /// 状态码
        /// </summary>
        public int StatusCode { get; set; }

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
        /// 响应体 (字节数组形式)
        /// </summary>
        public byte[] BodyBytes { get; set; }

        /// <summary>
        /// 响应体 (对象形式)
        /// </summary>
        public object BodyObject { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public HttpResponse()
        {
            Headers = new NameValueCollection();
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public HttpResponse(int statusCode, string body = "", string statusDescription = null)
        {
            StatusCode = statusCode;
            Body = body;
            StatusDescription = statusDescription ?? GetDefaultStatusDescription(statusCode);
            Headers = new NameValueCollection();
        }

        /// <summary>
        /// 构造函数 (字节数组)
        /// </summary>
        public HttpResponse(int statusCode, byte[] bodyBytes, string statusDescription = null)
        {
            StatusCode = statusCode;
            BodyBytes = bodyBytes;
            StatusDescription = statusDescription ?? GetDefaultStatusDescription(statusCode);
            Headers = new NameValueCollection();
        }

        /// <summary>
        /// 构造函数 (对象)
        /// </summary>
        public HttpResponse(int statusCode, object bodyObject, string statusDescription = null)
        {
            StatusCode = statusCode;
            BodyObject = bodyObject;
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

using System.Collections.Specialized;
using System.Net.Http;
using Drx.Sdk.Network.Http.Protocol;

namespace Drx.Sdk.Network.Http
{
    /// <summary>
    /// 内部请求队列条目，封装了一次 HTTP 请求的所有参数及完成回调。
    /// </summary>
    internal class HttpRequestTask
    {
        public HttpMethod Method { get; set; }
        public string Url { get; set; }
        public string Body { get; set; }
        public byte[] BodyBytes { get; set; }
        public object BodyObject { get; set; }
        public NameValueCollection Headers { get; set; }
        public NameValueCollection Query { get; set; }
        public HttpRequest.UploadFileDescriptor UploadFile { get; set; }
        public TaskCompletionSource<HttpResponse> Tcs { get; set; }
    }
}

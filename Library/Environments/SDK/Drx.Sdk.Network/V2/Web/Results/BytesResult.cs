using System.Threading.Tasks;
using Drx.Sdk.Network.V2.Web.Core;
using Drx.Sdk.Network.V2.Web.Http;

namespace Drx.Sdk.Network.V2.Web.Results
{
    /// <summary>
    /// 二进制数据结果（用于返回图片、文件等二进制内容）
    /// </summary>
    public class BytesResult : IActionResult
    {
        private readonly byte[] _data;
        private readonly string _contentType;
        private readonly int _statusCode;

        /// <summary>
        /// 构造二进制结果
        /// </summary>
        /// <param name="data">二进制数据</param>
        /// <param name="contentType">Content-Type，例如 "image/png"</param>
        /// <param name="statusCode">HTTP 状态码，默认 200</param>
        public BytesResult(byte[]? data, string contentType = "application/octet-stream", int statusCode = 200)
        {
            _data = data ?? [];
            _contentType = contentType;
            _statusCode = statusCode;
        }

        /// <summary>
        /// 将 BytesResult 转换为 HttpResponse
        /// </summary>
        public Task<HttpResponse> ExecuteAsync(HttpRequest request, DrxHttpServer server)
        {
            var resp = new HttpResponse(_statusCode, _data);
            try { resp.Headers.Add("Content-Type", _contentType); } catch { }
            return Task.FromResult(resp);
        }
    }
}

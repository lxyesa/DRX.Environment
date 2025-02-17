using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.Helpers
{
    /// <summary>
    /// 提供标准化的HTTP响应方法
    /// </summary>
    public static class ResponseHelper
    {
        /// <summary>
        /// 返回200 OK响应
        /// </summary>
        /// <param name="context">HTTP上下文</param>
        /// <param name="data">响应数据</param>
        /// <returns></returns>
        public static async Task Ok(HttpListenerContext context, object data)
        {
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = "application/json";
            var json = JsonSerializer.Serialize(data);
            var buffer = Encoding.UTF8.GetBytes(json);
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            context.Response.Close();
        }

        /// <summary>
        /// 返回400 Bad Request响应
        /// </summary>
        /// <param name="context">HTTP上下文</param>
        /// <param name="message">错误信息</param>
        /// <returns></returns>
        public static async Task BadRequest(HttpListenerContext context, string message)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "text/plain";
            var buffer = Encoding.UTF8.GetBytes(message);
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            context.Response.Close();
        }

        /// <summary>
        /// 返回404 Not Found响应
        /// </summary>
        /// <param name="context">HTTP上下文</param>
        /// <param name="message">错误信息</param>
        /// <returns></returns>
        public static async Task NotFound(HttpListenerContext context, string message)
        {
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            context.Response.ContentType = "text/plain";
            var buffer = Encoding.UTF8.GetBytes(message);
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            context.Response.Close();
        }

        // 可以根据需要添加更多响应方法，如 InternalServerError 等
    }
}

using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.Helpers
{
    /// <summary>
    /// �ṩ��׼����HTTP��Ӧ����
    /// </summary>
    public static class ResponseHelper
    {
        /// <summary>
        /// ����200 OK��Ӧ
        /// </summary>
        /// <param name="context">HTTP������</param>
        /// <param name="data">��Ӧ����</param>
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
        /// ����400 Bad Request��Ӧ
        /// </summary>
        /// <param name="context">HTTP������</param>
        /// <param name="message">������Ϣ</param>
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
        /// ����404 Not Found��Ӧ
        /// </summary>
        /// <param name="context">HTTP������</param>
        /// <param name="message">������Ϣ</param>
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

        // ���Ը�����Ҫ��Ӹ�����Ӧ�������� InternalServerError ��
    }
}

using System;
using System.Threading.Tasks;
using System.Net;
using Drx.Sdk.Network.Interfaces;
using System.Text;

namespace Drx.Sdk.Network
{
    public class ApiKeyMiddleware : IMiddleware
    {
        private readonly List<string> _validApiKeys;

        public ApiKeyMiddleware(IEnumerable<string> validApiKeys)
        {
            _validApiKeys = new List<string>(validApiKeys);
        }

        public void AddApiKey(string apiKey)
        {
            if (!_validApiKeys.Contains(apiKey))
            {
                _validApiKeys.Add(apiKey);
            }
        }

        public async Task Invoke(HttpListenerContext context)
        {
            var authorizationHeader = context.Request.Headers["Authorization"];
            if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("Unauthorized"));
                context.Response.Close();
                return;
            }

            var apiKey = authorizationHeader.Substring("Bearer ".Length).Trim();
            if (!_validApiKeys.Contains(apiKey))
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes("Unauthorized"));
                context.Response.Close();
                return;
            }

            // Call the next middleware in the pipeline
            await Task.CompletedTask;
        }
    }
}

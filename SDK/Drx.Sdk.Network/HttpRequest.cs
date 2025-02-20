using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Text.Json;
using System.Net.Http.Headers;

namespace Drx.Sdk.Network
{
    public struct HttpResult
    {
        public HttpPacket RequestPacket { get; set; }
        public HttpResponseMessage Response { get; set; }
    }

    public class HttpRequest
    {
        private readonly string _apiKey;

        public HttpRequest(string apiKey)
        {
            _apiKey = apiKey;
        }

        private HttpClient CreateHttpClient(int timeOut)
        {
            var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(timeOut)
            };
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            return httpClient;
        }

        public async Task<HttpResult> SendPostAsync(string url, HttpPacket packet, int timeOut = 30000)
        {
            using (var httpClient = CreateHttpClient(timeOut))
            {
                var jsonContent = packet.ToJson().ToString();
                var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();

                return new HttpResult
                {
                    RequestPacket = packet,
                    Response = response
                };
            }
        }

        public async Task<HttpResult> SendGetAsync(string url, int timeOut = 30000)
        {
            using (var httpClient = CreateHttpClient(timeOut))
            {
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                return new HttpResult
                {
                    RequestPacket = new HttpPacket(),
                    Response = response
                };
            }
        }
    }
}

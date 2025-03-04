using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Text.Json;

namespace Drx.Sdk.Network
{
    /// <summary>
    /// HTTP请求工具类，用于发送HTTP GET和POST请求
    /// </summary>
    public class HttpRequest
    {
        /// <summary>
        /// HTTP客户端实例
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        /// 初始化HTTP请求工具类的新实例
        /// </summary>
        public HttpRequest()
        {
            _httpClient = new HttpClient();
        }

        /// <summary>
        /// 发送POST请求到指定URL并携带请求体数据
        /// </summary>
        /// <typeparam name="T">响应数据反序列化的目标类型</typeparam>
        /// <param name="url">请求的URL地址</param>
        /// <param name="body">要发送的请求体数据对象</param>
        /// <param name="timeOut">超时时间(毫秒)，默认30000毫秒</param>
        /// <returns>返回反序列化后的T类型响应数据</returns>
        /// <exception cref="HttpRequestException">当请求失败、超时或响应数据反序列化失败时抛出</exception>
        public async Task<T> SendPostAsync<T>(string url, HttpPacket packet, int timeOut = 30000)
        {
            try
            {
                _httpClient.Timeout = TimeSpan.FromMilliseconds(timeOut);
                var jsonContent = JsonContent.Create(packet.ToJson());
                var response = await _httpClient.PostAsync(url, jsonContent);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<T>()
                    ?? throw new HttpRequestException("响应数据反序列化失败");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                throw new HttpRequestException($"POST请求失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 发送GET请求到指定URL
        /// </summary>
        /// <typeparam name="T">响应数据反序列化的目标类型</typeparam>
        /// <param name="url">请求的URL地址</param>
        /// <param name="timeOut">超时时间(毫秒)，默认30000毫秒</param>
        /// <returns>返回反序列化后的T类型响应数据</returns>
        /// <exception cref="HttpRequestException">当请求失败、超时或响应数据反序列化失败时抛出</exception>
        public async Task<T> SendGetAsync<T>(string url, int timeOut = 30000)
        {
            try
            {
                _httpClient.Timeout = TimeSpan.FromMilliseconds(timeOut);
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<T>()
                    ?? throw new HttpRequestException("响应数据反序列化失败");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                throw new HttpRequestException($"GET请求失败: {ex.Message}", ex);
            }
        }
    }
}

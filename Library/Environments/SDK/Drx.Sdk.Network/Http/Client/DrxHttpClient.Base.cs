using Drx.Sdk.Shared;
using System.Collections.Specialized;
using System.Threading.Channels;
using Drx.Sdk.Network.Http.Protocol;

namespace Drx.Sdk.Network.Http
{
    /// <summary>
    /// DrxHttpClient 基础部分：字段、常量、构造函数及核心属性
    /// </summary>
    public partial class DrxHttpClient
    {
        private readonly System.Net.Http.HttpClient _httpClient;
        private readonly System.Net.Http.HttpClientHandler _httpHandler;
        private System.Net.CookieContainer _cookieContainer;
        private readonly Channel<HttpRequestTask> _requestChannel;
        private readonly SemaphoreSlim _semaphore;
        private readonly CancellationTokenSource _cts;
        private const int MaxConcurrentRequests = 10;
        private Task _processingTask;

        /// <summary>
        /// 默认构造函数，使用内部 HttpClient 并启动请求处理通道。
        /// </summary>
        public DrxHttpClient()
        {
            _cookieContainer = new System.Net.CookieContainer();
            _httpHandler = new System.Net.Http.HttpClientHandler { CookieContainer = _cookieContainer, UseCookies = true };
            _httpClient = new System.Net.Http.HttpClient(_httpHandler);
            AutoManageCookies = true;
            _requestChannel = Channel.CreateBounded<HttpRequestTask>(new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.Wait
            });
            _semaphore = new SemaphoreSlim(MaxConcurrentRequests);
            _cts = new CancellationTokenSource();
            _processingTask = Task.Run(() => ProcessRequestsAsync(_cts.Token));
        }

        /// <summary>
        /// 指定基础地址的构造函数。
        /// </summary>
        /// <param name="baseAddress">用于初始化内部 HttpClient 的基地址。</param>
        /// <exception cref="System.ArgumentException">当 baseAddress 不是有效的 URI 时抛出。</exception>
        /// <exception cref="System.Exception">初始化 HttpClient 发生其它错误时抛出并向上传播。</exception>
        public DrxHttpClient(string baseAddress)
        {
            try
            {
                _cookieContainer = new System.Net.CookieContainer();
                _httpHandler = new System.Net.Http.HttpClientHandler { CookieContainer = _cookieContainer, UseCookies = true };
                _httpClient = new System.Net.Http.HttpClient(_httpHandler)
                {
                    BaseAddress = new Uri(baseAddress)
                };
                AutoManageCookies = true;
                _requestChannel = Channel.CreateBounded<HttpRequestTask>(new BoundedChannelOptions(100)
                {
                    FullMode = BoundedChannelFullMode.Wait
                });
                _semaphore = new SemaphoreSlim(MaxConcurrentRequests);
                _cts = new CancellationTokenSource();
                _processingTask = Task.Run(() => ProcessRequestsAsync(_cts.Token));
                Logger.Info($"HttpClient 初始化，基础地址: {baseAddress}");
            }
            catch (Exception ex)
            {
                Logger.Error($"初始化 HttpClient 时发生错误: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 是否自动管理 Cookie（将自动使用内部 CookieContainer 跟踪 Set-Cookie 并在请求时发送对应 Cookie）。
        /// 默认为 true。
        /// </summary>
        public bool AutoManageCookies { get; set; } = true;

        /// <summary>
        /// 会话 Cookie 名称，默认与服务器一致为 "session_id"。可根据服务端配置调整。
        /// </summary>
        public string SessionCookieName { get; set; } = "session_id";

        /// <summary>
        /// 可选：如果服务端使用自定义请求头传递会话令牌，可设置该字段（例如 "X-Session-Id"），
        /// 客户端在发送请求时会自动将当前会话 id 写入该 header（当 header 未被调用方显式设置时）。
        /// 默认为 null（不使用 header）。
        /// </summary>
        public string? SessionHeaderName { get; set; } = null;
    }
}

using Drx.Sdk.Shared;
using System.Collections.Specialized;
using System.Threading.Channels;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.Performance;

namespace Drx.Sdk.Network.Http
{
    /// <summary>
    /// DrxHttpClient 基础部分：字段、常量、构造函数及核心属性
    /// </summary>
    public partial class DrxHttpClient
    {
        private readonly System.Net.Http.HttpClient _httpClient;
        // _httpHandler 保留以维持与使用 HttpClientHandler 的旧代码兼容；
        // 新路径使用 SocketsHttpHandler（存储在 _socketsHandler）以支持连接池参数化。
        private readonly System.Net.Http.HttpClientHandler? _httpHandler;
        private readonly System.Net.Http.SocketsHttpHandler? _socketsHandler;
        private System.Net.CookieContainer _cookieContainer;
        private readonly Channel<HttpRequestTask> _requestChannel;
        private readonly SemaphoreSlim _semaphore;
        private readonly CancellationTokenSource _cts;
        private Task _processingTask;

        /// <summary>
        /// 客户端连接池与并发配置（只读，构造后不可更改）。
        /// </summary>
        internal readonly DrxHttpClientOptions _clientOptions;

        /// <summary>
        /// 默认构造函数，使用内部 HttpClient 并启动请求处理通道。
        /// 行为与历史版本完全兼容（MaxConcurrentRequests=10，队列容量=100）。
        /// </summary>
        public DrxHttpClient() : this(null as string, DrxHttpClientOptions.Default) { }

        /// <summary>
        /// 指定基础地址的构造函数，使用默认连接池配置。
        /// </summary>
        /// <param name="baseAddress">用于初始化内部 HttpClient 的基地址。</param>
        /// <exception cref="System.ArgumentException">当 baseAddress 不是有效的 URI 时抛出。</exception>
        /// <exception cref="System.Exception">初始化 HttpClient 发生其它错误时抛出并向上传播。</exception>
        public DrxHttpClient(string baseAddress) : this(baseAddress, DrxHttpClientOptions.Default) { }

        /// <summary>
        /// 带完整连接池配置选项的构造函数。
        /// </summary>
        /// <param name="baseAddress">基础地址（可为 null）。</param>
        /// <param name="options">连接池与并发配置，null 则使用默认值。</param>
        public DrxHttpClient(string? baseAddress, DrxHttpClientOptions? options)
        {
            _clientOptions = options ?? DrxHttpClientOptions.Default;
            _clientOptions.Validate();

            try
            {
                _cookieContainer = new System.Net.CookieContainer();

                if (_clientOptions.Enabled && _clientOptions.MaxConnectionsPerServer != int.MaxValue)
                {
                    // 使用 SocketsHttpHandler 以支持连接池参数化（MaxConnectionsPerServer 等）
                    _socketsHandler = _clientOptions.BuildSocketsHttpHandler(_cookieContainer);
                    var innerClient = new System.Net.Http.HttpClient(_socketsHandler)
                    {
                        Timeout = TimeSpan.FromSeconds(_clientOptions.RequestTimeoutSeconds)
                    };
                    if (!string.IsNullOrEmpty(baseAddress))
                        innerClient.BaseAddress = new Uri(baseAddress);
                    _httpClient = innerClient;
                }
                else
                {
                    // 默认路径：保持与原始行为完全一致
                    _httpHandler = new System.Net.Http.HttpClientHandler
                    {
                        CookieContainer = _cookieContainer,
                        UseCookies = true
                    };
                    var innerClient = new System.Net.Http.HttpClient(_httpHandler);
                    if (!string.IsNullOrEmpty(baseAddress))
                        innerClient.BaseAddress = new Uri(baseAddress);
                    _httpClient = innerClient;
                }

                AutoManageCookies = true;

                _requestChannel = Channel.CreateBounded<HttpRequestTask>(
                    new BoundedChannelOptions(_clientOptions.RequestQueueCapacity)
                    {
                        FullMode = BoundedChannelFullMode.Wait
                    });

                _semaphore = new SemaphoreSlim(_clientOptions.MaxConcurrentRequests);
                _cts = new CancellationTokenSource();
                _processingTask = Task.Run(() => ProcessRequestsAsync(_cts.Token));

                if (!string.IsNullOrEmpty(baseAddress))
                    Logger.Info($"HttpClient 初始化，基础地址: {baseAddress}, 连接池: maxConn={_clientOptions.MaxConnectionsPerServer}, maxConcurrent={_clientOptions.MaxConcurrentRequests}");
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

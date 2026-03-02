using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.Http
{
    // ─────────────────────────────────────────────────────────────────────────────
    // 枚举 & 基础数据类型
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// LLM API 提供商类型。
    /// </summary>
    public enum ApiProvider
    {
        /// <summary>OpenAI 兼容接口（ChatCompletions 格式）</summary>
        OpenAI,
        /// <summary>Anthropic Claude 接口</summary>
        Claude
    }

    /// <summary>
    /// 消息角色。
    /// </summary>
    public enum LLMRole
    {
        System,
        User,
        Assistant,
        /// <summary>工具调用结果（OpenAI tool role）</summary>
        Tool
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 消息 & 内容块
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// LLM 对话消息。
    /// </summary>
    public sealed class LLMMessage
    {
        public LLMRole Role { get; }
        public string? TextContent { get; }
        /// <summary>多模态内容块（图片 + 文本混合），优先级高于 TextContent。</summary>
        public IReadOnlyList<LLMContentPart>? Parts { get; }
        /// <summary>是否在此消息末尾开启 Claude Prompt Cache（cache_control: ephemeral）。</summary>
        public bool EnableCache { get; init; }

        public LLMMessage(LLMRole role, string text, bool enableCache = false)
        {
            Role = role;
            TextContent = text;
            EnableCache = enableCache;
        }

        public LLMMessage(LLMRole role, IReadOnlyList<LLMContentPart> parts, bool enableCache = false)
        {
            Role = role;
            Parts = parts;
            EnableCache = enableCache;
        }

        /// <summary>快捷构造：用户文本消息。</summary>
        public static LLMMessage FromUser(string text, bool cache = false) => new(LLMRole.User, text, cache);
        /// <summary>快捷构造：助手文本消息。</summary>
        public static LLMMessage FromAssistant(string text) => new(LLMRole.Assistant, text);
        /// <summary>快捷构造：系统消息（通常通过 WithSystemPrompt 设置，此处仅做备用）。</summary>
        public static LLMMessage FromSystem(string text, bool cache = false) => new(LLMRole.System, text, cache);
    }

    /// <summary>
    /// 多模态内容块（文本 / 图片）。
    /// </summary>
    public sealed class LLMContentPart
    {
        public string Type { get; }   // "text" | "image_url"
        public string? Text { get; }
        public string? ImageUrl { get; }   // URL 或 base64 data URI
        /// <summary>是否在此内容块加 cache_control（仅 Claude）。</summary>
        public bool EnableCache { get; init; }

        private LLMContentPart(string type, string? text, string? imageUrl) { Type = type; Text = text; ImageUrl = imageUrl; }

        public static LLMContentPart AsText(string text, bool cache = false) =>
            new LLMContentPart("text", text, null) { EnableCache = cache };

        public static LLMContentPart AsImage(string url) =>
            new LLMContentPart("image_url", null, url);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 响应数据类型
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Token 用量统计。
    /// </summary>
    public sealed class LLMUsage
    {
        public int InputTokens { get; init; }
        public int OutputTokens { get; init; }
        public int CacheCreationInputTokens { get; init; }
        public int CacheReadInputTokens { get; init; }
        public int TotalTokens => InputTokens + OutputTokens;
    }

    /// <summary>
    /// 完整 LLM 响应。
    /// </summary>
    public sealed class LLMResponse
    {
        /// <summary>模型输出的主要文本内容。</summary>
        public string Content { get; init; } = "";
        /// <summary>思考过程文本（Claude extended thinking / OpenAI reasoning_content）。</summary>
        public string? ThinkingContent { get; init; }
        /// <summary>停止原因（stop / max_tokens / tool_calls 等）。</summary>
        public string? StopReason { get; init; }
        public string? Model { get; init; }
        public LLMUsage? Usage { get; init; }
        /// <summary>原始响应 JSON（调试用）。</summary>
        public string? RawJson { get; init; }
        public bool IsSuccess { get; init; }
        public string? ErrorMessage { get; init; }
    }

    /// <summary>
    /// 流式响应片段。
    /// </summary>
    public sealed class LLMStreamChunk
    {
        /// <summary>本次输出的文本片段（空字符串表示无增量）。</summary>
        public string Text { get; init; } = "";
        /// <summary>思考过程增量（仅 Claude extended thinking）。</summary>
        public string? ThinkingDelta { get; init; }
        /// <summary>是否为终止块。</summary>
        public bool IsEnd { get; init; }
        /// <summary>终止块的停止原因。</summary>
        public string? StopReason { get; init; }
        /// <summary>终止块时的用量（部分 API 在最后一包返回用量）。</summary>
        public LLMUsage? Usage { get; init; }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // LLMRequestBuilder — Fluent 请求构建器
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 链式构建 LLM 请求的构建器，由 <see cref="LLMHttpClient.CreateRequest"/> 创建。
    /// <example>
    /// <code>
    /// // 非流式请求（默认）
    /// var response = await client.CreateRequest()
    ///     .WithModel("claude-opus-4-5")
    ///     .WithSystemPrompt("你是一个助手", cache: true)
    ///     .AddUserMessage("你好")
    ///     .WithMaxTokens(2048)
    ///     .WithThinking(10000)
    ///     .SendAsync();
    ///
    /// // 流式请求 + 自动拼接为完整响应
    /// var response2 = await client.CreateRequest()
    ///     .AddUserMessage("你好")
    ///     .WithStream()
    ///     .SendAsync();
    ///
    /// // 流式请求 + 逐块消费
    /// await foreach (var chunk in client.CreateRequest()
    ///     .AddUserMessage("你好")
    ///     .WithStream()
    ///     .StreamAsync())
    /// {
    ///     Console.Write(chunk.Text);
    /// }
    /// </code>
    /// </example>
    /// </summary>
    public sealed class LLMRequestBuilder
    {
        private readonly LLMHttpClient _client;
        private string? _model;
        private string? _systemPrompt;
        private bool _systemPromptCache;
        private readonly List<LLMMessage> _messages = new();
        private int? _maxTokens;
        private double? _temperature;
        private double? _topP;
        private int? _thinkingBudget;      // >0 表示启用思考（Claude）
        private bool _stream = false;       // 流式输出控制，默认 false（非流式）
        private TimeSpan? _timeout;
        private readonly NameValueCollection _extraHeaders = new();

        internal LLMRequestBuilder(LLMHttpClient client) => _client = client;

        // ── 模型 ────────────────────────────────────────────────────────────────

        /// <summary>指定使用的模型名称，覆盖客户端默认值。</summary>
        public LLMRequestBuilder WithModel(string model) { _model = model; return this; }

        // ── 系统提示词 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 设置系统提示词。
        /// </summary>
        /// <param name="prompt">系统提示内容。</param>
        /// <param name="cache">是否开启 Claude Prompt Cache（需要 API 支持）。</param>
        public LLMRequestBuilder WithSystemPrompt(string prompt, bool cache = false)
        {
            _systemPrompt = prompt;
            _systemPromptCache = cache;
            return this;
        }

        // ── 消息历史 ────────────────────────────────────────────────────────────

        /// <summary>添加用户消息。</summary>
        public LLMRequestBuilder AddUserMessage(string text, bool cache = false)
        {
            _messages.Add(new LLMMessage(LLMRole.User, text, cache));
            return this;
        }

        /// <summary>添加助手消息（用于多轮对话注入历史）。</summary>
        public LLMRequestBuilder AddAssistantMessage(string text)
        {
            _messages.Add(new LLMMessage(LLMRole.Assistant, text));
            return this;
        }

        /// <summary>以原始 LLMMessage 对象添加一条消息。</summary>
        public LLMRequestBuilder AddMessage(LLMMessage message) { _messages.Add(message); return this; }

        /// <summary>批量追加消息历史（用于多轮对话恢复）。</summary>
        public LLMRequestBuilder AddMessages(IEnumerable<LLMMessage> history)
        {
            _messages.AddRange(history);
            return this;
        }

        // ── 参数 ────────────────────────────────────────────────────────────────

        /// <summary>最大输出 Token 数。</summary>
        public LLMRequestBuilder WithMaxTokens(int maxTokens) { _maxTokens = maxTokens; return this; }

        /// <summary>采样温度（0.0 ~ 2.0）。</summary>
        public LLMRequestBuilder WithTemperature(double temperature) { _temperature = temperature; return this; }

        /// <summary>Top-P 采样。</summary>
        public LLMRequestBuilder WithTopP(double topP) { _topP = topP; return this; }

        /// <summary>
        /// 启用 Claude Extended Thinking（思考模式）。
        /// </summary>
        /// <param name="budgetTokens">思考预算 Token 数（建议 ≥ 1000，默认 8000）。</param>
        public LLMRequestBuilder WithThinking(int budgetTokens = 8000)
        {
            _thinkingBudget = budgetTokens;
            return this;
        }

        /// <summary>禁用思考模式（显式关闭）。</summary>
        public LLMRequestBuilder WithoutThinking() { _thinkingBudget = null; return this; }

        /// <summary>设置请求超时时间。</summary>
        public LLMRequestBuilder WithTimeout(TimeSpan timeout) { _timeout = timeout; return this; }

        /// <summary>
        /// 控制是否启用流式输出。
        /// </summary>
        /// <param name="stream">true 表示启用流式输出，false 表示返回完整响应（默认）。</param>
        public LLMRequestBuilder WithStream(bool stream = true) { _stream = stream; return this; }

        /// <summary>添加额外的 HTTP 请求头（如自定义认证头）。</summary>
        public LLMRequestBuilder WithHeader(string key, string value) { _extraHeaders[key] = value; return this; }

        // ── 执行 ────────────────────────────────────────────────────────────────

        /// <summary>
        /// 发送请求并等待完整响应。
        /// 当 <see cref="WithStream(bool)"/> 为 true 时，以流式方式接收数据并自动拼接为完整的 <see cref="LLMResponse"/>。
        /// </summary>
        public Task<LLMResponse> SendAsync(CancellationToken cancellationToken = default)
            => _stream
                ? _client.ExecuteStreamCollectAsync(this, cancellationToken)
                : _client.ExecuteAsync(this, cancellationToken);

        /// <summary>
        /// 以流式方式发送请求，通过 <see cref="IAsyncEnumerable{LLMStreamChunk}"/> 逐块返回。
        /// 无论 <see cref="WithStream(bool)"/> 的值如何，此方法始终以流式模式请求 API。
        /// </summary>
        public IAsyncEnumerable<LLMStreamChunk> StreamAsync(CancellationToken cancellationToken = default)
            => _client.ExecuteStreamAsync(this, cancellationToken);

        // ── 内部访问器（供 LLMHttpClient 读取构建参数）────────────────────────

        internal string ResolvedModel => _model ?? _client.DefaultModel;
        internal string? SystemPrompt => _systemPrompt;
        internal bool SystemPromptCache => _systemPromptCache;
        internal IReadOnlyList<LLMMessage> Messages => _messages;
        internal int MaxTokens => _maxTokens ?? _client.DefaultMaxTokens;
        internal double Temperature => _temperature ?? _client.DefaultTemperature;
        internal double? TopP => _topP;
        internal int? ThinkingBudget => _thinkingBudget;
        internal bool Stream => _stream;
        internal TimeSpan? Timeout => _timeout;
        internal NameValueCollection ExtraHeaders => _extraHeaders;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // LLMHttpClient — 主客户端
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 面向大型语言模型（LLM）的现代化 HTTP 客户端，支持：
    /// <list type="bullet">
    ///   <item><description>OpenAI 和 Claude（Anthropic）两种 API 格式，可自定义 Base URL</description></item>
    ///   <item><description>多轮对话历史管理</description></item>
    ///   <item><description>流式输出（IAsyncEnumerable{LLMStreamChunk}）</description></item>
    ///   <item><description>Claude Extended Thinking（思考模式）</description></item>
    ///   <item><description>Claude Prompt Caching（启动缓存）</description></item>
    ///   <item><description>系统提示词、最大 Token、温度等参数配置</description></item>
    ///   <item><description>Fluent（链式）请求构建 API</description></item>
    /// </list>
    /// <example>
    /// <code>
    /// // Claude 示例
    /// var claude = LLMHttpClient.ForClaude("sk-ant-xxxxxxxx");
    /// var reply = await claude.CreateRequest()
    ///     .WithModel("claude-opus-4-5")
    ///     .WithSystemPrompt("你是一位专业的 C# 工程师。", cache: true)
    ///     .AddUserMessage("帮我写一个快速排序。")
    ///     .WithMaxTokens(4096)
    ///     .WithThinking(8000)
    ///     .SendAsync();
    /// Console.WriteLine(reply.Content);
    ///
    /// // OpenAI 流式示例
    /// var openai = LLMHttpClient.ForOpenAI("sk-xxxxxx");
    /// await foreach (var chunk in openai.CreateRequest()
    ///     .WithModel("gpt-4o")
    ///     .AddUserMessage("Hello!")
    ///     .StreamAsync())
    /// {
    ///     if (!chunk.IsEnd) Console.Write(chunk.Text);
    /// }
    /// </code>
    /// </example>
    /// </summary>
    public sealed class LLMHttpClient : IDisposable
    {
        // ── 静态工厂 ─────────────────────────────────────────────────────────────

        /// <summary>创建 OpenAI API 客户端（默认端点 https://api.openai.com）。</summary>
        public static LLMHttpClient ForOpenAI(string apiKey, string? baseUrl = null) =>
            new(ApiProvider.OpenAI, apiKey, baseUrl);

        /// <summary>创建 Claude (Anthropic) API 客户端（默认端点 https://api.anthropic.com）。</summary>
        public static LLMHttpClient ForClaude(string apiKey, string? baseUrl = null) =>
            new(ApiProvider.Claude, apiKey, baseUrl);

        /// <summary>
        /// 以自定义 Base URL 创建指定类型的客户端（适用于三方代理 / 私有部署）。
        /// </summary>
        public static LLMHttpClient ForCustom(ApiProvider provider, string apiKey, string baseUrl) =>
            new(provider, apiKey, baseUrl);

        // ── 字段 & 属性 ──────────────────────────────────────────────────────────

        private readonly System.Net.Http.HttpClient _http;
        private bool _disposed;

        /// <summary>当前使用的 API 提供商。</summary>
        public ApiProvider Provider { get; }

        /// <summary>API 密钥。</summary>
        public string ApiKey { get; }

        /// <summary>Base URL（含协议与端口，不含路径末尾斜杠）。</summary>
        public string BaseUrl { get; }

        /// <summary>默认模型名称，可被 <c>WithModel()</c> 覆盖。</summary>
        public string DefaultModel { get; set; }

        /// <summary>默认最大输出 Token，可被 <c>WithMaxTokens()</c> 覆盖（默认 2048）。</summary>
        public int DefaultMaxTokens { get; set; } = 2048;

        /// <summary>默认采样温度（默认 1.0），可被 <c>WithTemperature()</c> 覆盖。</summary>
        public double DefaultTemperature { get; set; } = 1.0;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        };

        // ── 构造 ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// 构造 LLM 客户端。推荐使用静态工厂方法 <see cref="ForOpenAI"/> / <see cref="ForClaude"/>。
        /// </summary>
        public LLMHttpClient(ApiProvider provider, string apiKey, string? baseUrl = null)
        {
            Provider = provider;
            ApiKey = apiKey;
            BaseUrl = (baseUrl ?? GetDefaultBaseUrl(provider)).TrimEnd('/');

            DefaultModel = provider switch
            {
                ApiProvider.Claude => "claude-opus-4-5",
                ApiProvider.OpenAI => "gpt-4o",
                _ => "gpt-4o"
            };

            _http = new System.Net.Http.HttpClient
            {
                Timeout = System.Threading.Timeout.InfiniteTimeSpan
            };
        }

        // ── 公共 API ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 创建一个新的链式请求构建器。
        /// </summary>
        public LLMRequestBuilder CreateRequest() => new LLMRequestBuilder(this);

        // ── 内部执行（非流式）────────────────────────────────────────────────────

        internal async Task<LLMResponse> ExecuteAsync(LLMRequestBuilder builder, CancellationToken cancellationToken)
        {
            var (url, payload, headers) = BuildRequest(builder, stream: builder.Stream);
            var json = JsonSerializer.Serialize(payload, _jsonOptions);

            using var cts = BuildCts(builder.Timeout, cancellationToken);
            using var req = BuildHttpRequestMessage(url, json, headers, stream: builder.Stream);

            HttpResponseMessage resp;
            try
            {
                resp = await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return new LLMResponse { IsSuccess = false, ErrorMessage = "Request timeout or cancelled." };
            }
            catch (Exception ex)
            {
                return new LLMResponse { IsSuccess = false, ErrorMessage = ex.Message };
            }

            var rawJson = await resp.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                return new LLMResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"HTTP {(int)resp.StatusCode}: {rawJson}",
                    RawJson = rawJson
                };
            }

            return Provider == ApiProvider.Claude
                ? ParseClaudeResponse(rawJson)
                : ParseOpenAIResponse(rawJson);
        }

        // ── 内部执行（流式收集 → 完整响应）────────────────────────────────────

        /// <summary>
        /// 以流式方式接收 API 响应，自动收集所有 chunk 拼接为完整 <see cref="LLMResponse"/>。
        /// 供 <c>SendAsync()</c> 在 stream=true 时调用。
        /// </summary>
        internal async Task<LLMResponse> ExecuteStreamCollectAsync(LLMRequestBuilder builder, CancellationToken cancellationToken)
        {
            var contentSb = new StringBuilder();
            var thinkingSb = new StringBuilder();
            string? stopReason = null;
            LLMUsage? usage = null;

            await foreach (var chunk in ExecuteStreamAsync(builder, cancellationToken).ConfigureAwait(false))
            {
                if (!string.IsNullOrEmpty(chunk.Text))
                    contentSb.Append(chunk.Text);
                if (!string.IsNullOrEmpty(chunk.ThinkingDelta))
                    thinkingSb.Append(chunk.ThinkingDelta);
                if (chunk.StopReason != null)
                    stopReason = chunk.StopReason;
                if (chunk.Usage != null)
                    usage = chunk.Usage;
            }

            return new LLMResponse
            {
                Content = contentSb.ToString(),
                ThinkingContent = thinkingSb.Length > 0 ? thinkingSb.ToString() : null,
                StopReason = stopReason,
                Model = builder.ResolvedModel,
                Usage = usage,
                IsSuccess = true
            };
        }

        // ── 内部执行（流式）──────────────────────────────────────────────────────

        internal async IAsyncEnumerable<LLMStreamChunk> ExecuteStreamAsync(
            LLMRequestBuilder builder,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var (url, payload, headers) = BuildRequest(builder, stream: true);
            var json = JsonSerializer.Serialize(payload, _jsonOptions);

            using var cts = BuildCts(builder.Timeout, cancellationToken);
            using var req = BuildHttpRequestMessage(url, json, headers, stream: true);

            HttpResponseMessage? resp = null;
            LLMStreamChunk? errorChunk = null;
            try
            {
                resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { yield break; }
            catch (Exception ex)
            {
                errorChunk = new LLMStreamChunk { IsEnd = true, StopReason = $"error: {ex.Message}" };
            }

            if (errorChunk != null) { yield return errorChunk; yield break; }
            if (resp == null) yield break;

            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                yield return new LLMStreamChunk { IsEnd = true, StopReason = $"HTTP {(int)resp.StatusCode}: {err}" };
                resp.Dispose();
                yield break;
            }

            using var stream = await resp.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
            var buffer = ArrayPool<byte>.Shared.Rent(8192);
            try
            {
                var lineBuilder = new StringBuilder();
                var decoder = Encoding.UTF8.GetDecoder();
                var charBuffer = new char[8192];

                int read;
                while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cts.Token).ConfigureAwait(false)) > 0)
                {
                    int chars = decoder.GetChars(buffer, 0, read, charBuffer, 0, flush: false);
                    for (int i = 0; i < chars; i++)
                    {
                        char c = charBuffer[i];
                        if (c == '\n')
                        {
                            var line = lineBuilder.ToString();
                            lineBuilder.Clear();
                            if (line.StartsWith("data:", StringComparison.Ordinal))
                            {
                                var data = line[5..].Trim();
                                LLMStreamChunk? chunk = Provider == ApiProvider.Claude
                                    ? TryParseClaudeStreamChunk(data)
                                    : TryParseOpenAIStreamChunk(data);
                                if (chunk != null)
                                {
                                    yield return chunk;
                                    if (chunk.IsEnd) goto streamEnd;
                                }
                            }
                        }
                        else if (c != '\r')
                        {
                            lineBuilder.Append(c);
                        }
                    }
                }

                // flush decoder 尾部
                int finalChars = decoder.GetChars(Array.Empty<byte>(), 0, 0, charBuffer, 0, flush: true);
                if (finalChars > 0 || lineBuilder.Length > 0)
                {
                    if (finalChars > 0)
                        lineBuilder.Append(charBuffer, 0, finalChars);

                    var lastLine = lineBuilder.ToString();
                    if (lastLine.StartsWith("data:", StringComparison.Ordinal))
                    {
                        var data = lastLine[5..].Trim();
                        LLMStreamChunk? chunk = Provider == ApiProvider.Claude
                            ? TryParseClaudeStreamChunk(data)
                            : TryParseOpenAIStreamChunk(data);
                        if (chunk != null) yield return chunk;
                    }
                }

                streamEnd:;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
                try { resp.Dispose(); } catch { }
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 请求构建 helper
        // ─────────────────────────────────────────────────────────────────────────

        private (string url, object payload, Dictionary<string, string> headers) BuildRequest(
            LLMRequestBuilder builder, bool stream)
        {
            return Provider == ApiProvider.Claude
                ? BuildClaudeRequest(builder, stream)
                : BuildOpenAIRequest(builder, stream);
        }

        // ── OpenAI 请求 ──────────────────────────────────────────────────────────

        private (string url, object payload, Dictionary<string, string> headers) BuildOpenAIRequest(
            LLMRequestBuilder builder, bool stream)
        {
            var url = $"{BaseUrl}/v1/chat/completions";
            var messages = new List<object>();

            if (!string.IsNullOrEmpty(builder.SystemPrompt))
                messages.Add(new { role = "system", content = builder.SystemPrompt });

            foreach (var msg in builder.Messages)
            {
                if (msg.Role == LLMRole.System) continue;
                var role = msg.Role switch
                {
                    LLMRole.User => "user",
                    LLMRole.Assistant => "assistant",
                    LLMRole.Tool => "tool",
                    _ => "user"
                };

                if (msg.Parts != null && msg.Parts.Count > 0)
                {
                    var parts = new List<object>();
                    foreach (var p in msg.Parts)
                    {
                        if (p.Type == "text")
                            parts.Add(new { type = "text", text = p.Text });
                        else if (p.Type == "image_url")
                            parts.Add(new { type = "image_url", image_url = new { url = p.ImageUrl } });
                    }
                    messages.Add(new { role, content = parts });
                }
                else
                {
                    messages.Add(new { role, content = msg.TextContent ?? "" });
                }
            }

            var payload = new Dictionary<string, object>
            {
                ["model"] = builder.ResolvedModel,
                ["messages"] = messages,
                ["max_tokens"] = builder.MaxTokens,
                ["temperature"] = builder.Temperature,
                ["stream"] = stream
            };

            if (builder.TopP.HasValue) payload["top_p"] = builder.TopP.Value;
            if (stream) payload["stream_options"] = new { include_usage = true };

            var headers = new Dictionary<string, string>
            {
                ["Authorization"] = $"Bearer {ApiKey}",
                ["Content-Type"] = "application/json"
            };
            foreach (string key in builder.ExtraHeaders)
                if (!string.IsNullOrEmpty(key)) headers[key] = builder.ExtraHeaders[key] ?? "";

            return (url, payload, headers);
        }

        // ── Claude 请求 ──────────────────────────────────────────────────────────

        private (string url, object payload, Dictionary<string, string> headers) BuildClaudeRequest(
            LLMRequestBuilder builder, bool stream)
        {
            var url = $"{BaseUrl}/v1/messages";
            var messages = new List<object>();

            foreach (var msg in builder.Messages)
            {
                if (msg.Role == LLMRole.System) continue;
                var role = msg.Role == LLMRole.Assistant ? "assistant" : "user";

                if (msg.Parts != null && msg.Parts.Count > 0)
                {
                    var parts = new List<object>();
                    for (int i = 0; i < msg.Parts.Count; i++)
                    {
                        var p = msg.Parts[i];
                        bool isLast = i == msg.Parts.Count - 1;
                        bool addCache = p.EnableCache || (isLast && msg.EnableCache);

                        if (p.Type == "text")
                        {
                            parts.Add(addCache
                                ? (object)new { type = "text", text = p.Text, cache_control = new { type = "ephemeral" } }
                                : new { type = "text", text = p.Text });
                        }
                        else if (p.Type == "image_url")
                        {
                            parts.Add(new { type = "image", source = new { type = "url", url = p.ImageUrl } });
                        }
                    }
                    messages.Add(new { role, content = parts });
                }
                else
                {
                    object msgContent = msg.EnableCache
                        ? (object)new List<object>
                          {
                            new { type = "text", text = msg.TextContent ?? "", cache_control = new { type = "ephemeral" } }
                          }
                        : (object)(msg.TextContent ?? "");
                    messages.Add(new { role, content = msgContent });
                }
            }

            var payload = new Dictionary<string, object>
            {
                ["model"] = builder.ResolvedModel,
                ["messages"] = messages,
                ["max_tokens"] = builder.MaxTokens,
            };

            if (builder.TopP.HasValue) payload["top_p"] = builder.TopP.Value;
            if (stream) payload["stream"] = true;

            // 思考模式（extended thinking）
            if (builder.ThinkingBudget.HasValue && builder.ThinkingBudget.Value > 0)
            {
                payload["thinking"] = new { type = "enabled", budget_tokens = builder.ThinkingBudget.Value };
                payload["temperature"] = 1.0; // 思考模式要求 temperature = 1
            }
            else
            {
                payload["temperature"] = builder.Temperature;
            }

            // 系统提示词
            if (!string.IsNullOrEmpty(builder.SystemPrompt))
            {
                payload["system"] = builder.SystemPromptCache
                    ? (object)new List<object>
                      {
                        new { type = "text", text = builder.SystemPrompt, cache_control = new { type = "ephemeral" } }
                      }
                    : (object)builder.SystemPrompt;
            }

            // 请求头
            bool hasCache = builder.SystemPromptCache || builder.Messages.Any(m => m.EnableCache);
            var headers = new Dictionary<string, string>
            {
                ["x-api-key"] = ApiKey,
                ["anthropic-version"] = "2023-06-01",
                ["Content-Type"] = "application/json"
            };

            var betaFeatures = new List<string>();
            if (hasCache) betaFeatures.Add("prompt-caching-2024-07-31");
            if (builder.ThinkingBudget.HasValue) betaFeatures.Add("interleaved-thinking-2025-05-14");
            if (betaFeatures.Count > 0) headers["anthropic-beta"] = string.Join(",", betaFeatures);

            foreach (string key in builder.ExtraHeaders)
                if (!string.IsNullOrEmpty(key)) headers[key] = builder.ExtraHeaders[key] ?? "";

            return (url, payload, headers);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 响应解析 helper
        // ─────────────────────────────────────────────────────────────────────────

        private static LLMResponse ParseOpenAIResponse(string rawJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(rawJson);
                var root = doc.RootElement;

                string content = "";
                string? thinkingContent = null;
                string? stopReason = null;
                string? model = null;
                LLMUsage? usage = null;

                if (root.TryGetProperty("model", out var modelProp))
                    model = modelProp.GetString();

                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var choice = choices[0];
                    if (choice.TryGetProperty("finish_reason", out var fr)) stopReason = fr.GetString();
                    if (choice.TryGetProperty("message", out var msg))
                    {
                        if (msg.TryGetProperty("content", out var c)) content = c.GetString() ?? "";
                        if (msg.TryGetProperty("reasoning_content", out var rc)) thinkingContent = rc.GetString();
                    }
                }

                if (root.TryGetProperty("usage", out var up))
                {
                    usage = new LLMUsage
                    {
                        InputTokens = up.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0,
                        OutputTokens = up.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0
                    };
                }

                return new LLMResponse
                {
                    Content = content, ThinkingContent = thinkingContent,
                    StopReason = stopReason, Model = model, Usage = usage,
                    RawJson = rawJson, IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return new LLMResponse { IsSuccess = false, ErrorMessage = $"Parse error: {ex.Message}", RawJson = rawJson };
            }
        }

        private static LLMResponse ParseClaudeResponse(string rawJson)
        {
            try
            {
                using var doc = JsonDocument.Parse(rawJson);
                var root = doc.RootElement;

                string content = "";
                string? thinkingContent = null;
                string? stopReason = null;
                string? model = null;
                LLMUsage? usage = null;

                if (root.TryGetProperty("model", out var modelProp)) model = modelProp.GetString();
                if (root.TryGetProperty("stop_reason", out var sr)) stopReason = sr.GetString();

                if (root.TryGetProperty("content", out var blocks))
                {
                    var textSb = new StringBuilder();
                    var thinkSb = new StringBuilder();
                    foreach (var block in blocks.EnumerateArray())
                    {
                        if (!block.TryGetProperty("type", out var typeProp)) continue;
                        var type = typeProp.GetString();
                        if (type == "text" && block.TryGetProperty("text", out var t))
                            textSb.Append(t.GetString());
                        else if (type == "thinking" && block.TryGetProperty("thinking", out var th))
                            thinkSb.Append(th.GetString());
                    }
                    content = textSb.ToString();
                    if (thinkSb.Length > 0) thinkingContent = thinkSb.ToString();
                }

                if (root.TryGetProperty("usage", out var up))
                {
                    usage = new LLMUsage
                    {
                        InputTokens = up.TryGetProperty("input_tokens", out var it) ? it.GetInt32() : 0,
                        OutputTokens = up.TryGetProperty("output_tokens", out var ot) ? ot.GetInt32() : 0,
                        CacheCreationInputTokens = up.TryGetProperty("cache_creation_input_tokens", out var cc) ? cc.GetInt32() : 0,
                        CacheReadInputTokens = up.TryGetProperty("cache_read_input_tokens", out var cr) ? cr.GetInt32() : 0
                    };
                }

                return new LLMResponse
                {
                    Content = content, ThinkingContent = thinkingContent,
                    StopReason = stopReason, Model = model, Usage = usage,
                    RawJson = rawJson, IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return new LLMResponse { IsSuccess = false, ErrorMessage = $"Parse error: {ex.Message}", RawJson = rawJson };
            }
        }

        // ── OpenAI 流式解析 ──────────────────────────────────────────────────────

        private static LLMStreamChunk? TryParseOpenAIStreamChunk(string data)
        {
            if (data == "[DONE]") return new LLMStreamChunk { IsEnd = true, StopReason = "stop" };
            if (string.IsNullOrEmpty(data)) return null;
            try
            {
                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;

                string text = "";
                string? reasoning = null;
                string? stopReason = null;
                LLMUsage? usage = null;

                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var choice = choices[0];
                    if (choice.TryGetProperty("finish_reason", out var fr) && fr.ValueKind != JsonValueKind.Null)
                        stopReason = fr.GetString();
                    if (choice.TryGetProperty("delta", out var delta))
                    {
                        if (delta.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
                            text = c.GetString() ?? "";
                        if (delta.TryGetProperty("reasoning_content", out var rc) && rc.ValueKind == JsonValueKind.String)
                            reasoning = rc.GetString();
                    }
                }

                if (root.TryGetProperty("usage", out var up) && up.ValueKind != JsonValueKind.Null)
                {
                    usage = new LLMUsage
                    {
                        InputTokens = up.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0,
                        OutputTokens = up.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0
                    };
                }

                return new LLMStreamChunk
                {
                    Text = text,
                    ThinkingDelta = reasoning,
                    IsEnd = stopReason != null,
                    StopReason = stopReason,
                    Usage = usage
                };
            }
            catch { return null; }
        }

        // ── Claude 流式解析 ──────────────────────────────────────────────────────

        private static LLMStreamChunk? TryParseClaudeStreamChunk(string data)
        {
            if (string.IsNullOrEmpty(data)) return null;
            try
            {
                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;

                if (!root.TryGetProperty("type", out var typeProp)) return null;
                var eventType = typeProp.GetString();

                switch (eventType)
                {
                    case "ping":
                        return null;

                    case "content_block_delta":
                        if (!root.TryGetProperty("delta", out var delta)) return null;
                        if (!delta.TryGetProperty("type", out var dType)) return null;
                        var deltaType = dType.GetString();

                        if (deltaType == "text_delta" && delta.TryGetProperty("text", out var textProp))
                            return new LLMStreamChunk { Text = textProp.GetString() ?? "" };
                        if (deltaType == "thinking_delta" && delta.TryGetProperty("thinking", out var thinkProp))
                            return new LLMStreamChunk { ThinkingDelta = thinkProp.GetString() ?? "" };
                        return null;

                    case "message_delta":
                        string? stopReason = null;
                        LLMUsage? usage = null;
                        if (root.TryGetProperty("delta", out var msgDelta) &&
                            msgDelta.TryGetProperty("stop_reason", out var srProp))
                            stopReason = srProp.GetString();
                        if (root.TryGetProperty("usage", out var up))
                            usage = new LLMUsage { OutputTokens = up.TryGetProperty("output_tokens", out var ot) ? ot.GetInt32() : 0 };
                        return new LLMStreamChunk { StopReason = stopReason, Usage = usage };

                    case "message_stop":
                        return new LLMStreamChunk { IsEnd = true };

                    case "error":
                        string errMsg = "Unknown error";
                        if (root.TryGetProperty("error", out var errObj) &&
                            errObj.TryGetProperty("message", out var errMsgProp))
                            errMsg = errMsgProp.GetString() ?? errMsg;
                        return new LLMStreamChunk { IsEnd = true, StopReason = $"error: {errMsg}" };

                    default:
                        return null;
                }
            }
            catch { return null; }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // 工具方法
        // ─────────────────────────────────────────────────────────────────────────

        private static HttpRequestMessage BuildHttpRequestMessage(string url, string json, Dictionary<string, string> headers, bool stream)
        {
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var req = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, url) { Content = content };
            foreach (var kv in headers)
            {
                if (kv.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)) continue;
                req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
            }
            if (stream)
            {
                req.Headers.Accept.TryParseAdd("text/event-stream");
            }
            else
            {
                req.Headers.Accept.TryParseAdd("application/json");
            }
            return req;
        }

        private static CancellationTokenSource BuildCts(TimeSpan? timeout, CancellationToken cancellationToken)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (timeout.HasValue) cts.CancelAfter(timeout.Value);
            return cts;
        }

        private static string GetDefaultBaseUrl(ApiProvider provider) => provider switch
        {
            ApiProvider.Claude => "https://api.anthropic.com",
            ApiProvider.OpenAI => "https://api.openai.com",
            _ => "https://api.openai.com"
        };

        // ─────────────────────────────────────────────────────────────────────────
        // IDisposable
        // ─────────────────────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _http.Dispose();
        }
    }
}

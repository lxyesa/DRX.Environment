using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DRX.Framework.Chat;

/// <summary>
/// DeepSeek API客户端封装
/// </summary>
public class DeepSeek
{
    #region Fields
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private const string DefaultModel = "deepseek";
    private Message? _systemPrompt;
    private string _conversationId = string.Empty;
    #endregion

    #region Properties
    /// <summary>
    /// 获取或设置系统提示
    /// </summary>
    public string? SystemPrompt
    {
        get => _systemPrompt?.Content;
        set => _systemPrompt = string.IsNullOrEmpty(value) ? null
            : new Message { Role = "system", Content = value };
    }

    /// <summary>
    /// 设置或获取模型名称
    /// </summary>
    public string Model { get; set; } = DefaultModel;
    #endregion

    #region Constructor
    public DeepSeek(string apiKey, string? baseUrl = null)
    {
        _apiKey = apiKey;
        _baseUrl = baseUrl ?? "http://127.0.0.1:8000";
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl)
        };
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// 发送聊天消息并获取响应
    /// </summary>
    public async Task<string> ChatAsync(string message, CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(message);
        if (!string.IsNullOrEmpty(_conversationId))
        {
            request.ConversationId = _conversationId;
        }
        var response = await ExecuteChatRequestAsync(request, cancellationToken);
        Console.WriteLine($"Response ID: {response.Id}");
        _conversationId = response.Id;
        return response.Choices[0].Message.Content;
    }

    /// <summary>
    /// 创建流式聊天会话
    /// </summary>
    public async IAsyncEnumerable<string> CreateChatStreamAsync(
        string message,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = CreateRequest(message, true);
        var buffer = new StringBuilder();
        string? responseId = null;

        await foreach (var chunk in ProcessStreamResponse(request, cancellationToken))
        {
            // 获取第一个响应块中的 ID
            if (responseId == null && chunk.Contains("\"id\":"))
            {
                try
                {
                    var streamResponse = JsonSerializer.Deserialize<ChatResponse>(chunk);
                    if (streamResponse?.Id != null)
                    {
                        responseId = streamResponse.Id;
                        Console.WriteLine($"Stream Response ID: {responseId}");
                    }
                }
                catch { /* 忽略解析错误 */ }
            }
            buffer.Append(chunk);
            yield return chunk;
        }
    }

    /// <summary>
    /// 设置系统角色和提示
    /// </summary>
    public void SetSystemRole(string systemPrompt)
    {
        SystemPrompt = systemPrompt;
    }
    #endregion

    #region Private Methods
    private ChatRequest CreateRequest(string message, bool isStream = false)
    {
        var messages = new List<Message>();
        if (_systemPrompt != null)
        {
            messages.Add(_systemPrompt);
        }
        messages.Add(new Message { Role = "user", Content = message });

        return new ChatRequest
        {
            Model = this.Model,
            Messages = messages,
            Stream = isStream
        };
    }

    private async Task<ChatResponse> ExecuteChatRequestAsync(
        ChatRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync("v1/chat/completions", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Failed to deserialize response");
    }

    private async IAsyncEnumerable<string> ProcessStreamResponse(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync("v1/chat/completions", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var buffer = new StringBuilder();

        while (!reader.EndOfStream)
        {
            if (cancellationToken.IsCancellationRequested) yield break;

            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrEmpty(line) || line == "data: [DONE]") continue;

            if (line.StartsWith("data: "))
            {
                var content = ProcessStreamLine(line["data: ".Length..]);
                if (!string.IsNullOrEmpty(content))
                {
                    buffer.Append(content);
                    if (ShouldOutputBuffer(content, buffer.Length))
                    {
                        yield return buffer.ToString();
                        buffer.Clear();
                    }
                }
            }
        }

        if (buffer.Length > 0)
        {
            yield return buffer.ToString();
        }
    }

    private static string? ProcessStreamLine(string line)
    {
        var streamResponse = JsonSerializer.Deserialize<ChatStreamResponse>(line);
        return streamResponse?.Choices[0].Delta.Content;
    }

    private static bool ShouldOutputBuffer(string content, int bufferLength)
    {
        return content.Any(c =>
            c == '。' || c == '，' || c == '！' || c == '？' ||
            c == '.' || c == ',' || c == '!' || c == '?' ||
            c == ';' || c == '；' || c == '\n' || c == '\r') ||
            bufferLength >= 10;
    }
    #endregion
}

#region Models
public record ChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = "deepseek";

    [JsonPropertyName("messages")]
    public IEnumerable<Message> Messages { get; init; } = Array.Empty<Message>();

    [JsonPropertyName("temperature")]
    public float Temperature { get; init; } = 0.7f;

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; init; }

    [JsonPropertyName("stream")]
    public bool Stream { get; init; }

    [JsonPropertyName("conversation_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ConversationId { get; set; }
}

public record Message
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;
}

public record ChatResponse
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; init; } = string.Empty;

    [JsonPropertyName("created")]
    public int Created { get; init; }

    [JsonPropertyName("choices")]
    public IReadOnlyList<Choice> Choices { get; init; } = Array.Empty<Choice>();
}

public record Choice
{
    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("message")]
    public Message Message { get; init; } = new();

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; init; }
}

public record ChatStreamResponse
{
    [JsonPropertyName("choices")]
    public IReadOnlyList<StreamChoice> Choices { get; init; } = Array.Empty<StreamChoice>();
}

public record StreamChoice
{
    [JsonPropertyName("delta")]
    public Message Delta { get; init; } = new();
}
#endregion

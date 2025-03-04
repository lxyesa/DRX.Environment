// 添加在 Program.cs 的类定义外部
using Drx.Sdk.Json;
using Drx.Sdk.Network;
using DRXServer.Apis;
using System.Security.Cryptography;

public class ApiKeyStore
{
    public DateTime GeneratedTime { get; set; }
    public List<string> Keys { get; set; } = new();
}

class Program
{
    private const string API_KEYS_FILE = "apikeys.json";
    private const int API_KEYS_EXPIRY_DAYS = 7;
    private const int DEFAULT_KEY_COUNT = 1000;

    static async Task Main(string[] args)
    {
        // 获取有效的 API keys 或生成新的
        var apiKeys = await GetOrGenerateApiKeys();

        // 创建 HTTP 服务器实例
        var server = new HttpServer("http://localhost:5000/");

        server.AddApiKeyMiddleware(apiKeys);
        server.RegisterApi(new ConnectTestApi());

        // 启动服务器
        await server.StartAsync();
    }

    static async Task<string[]> GetOrGenerateApiKeys()
    {
        try
        {
            // 尝试读取现有的 API keys
            if (File.Exists(API_KEYS_FILE))
            {
                var apiKeyStore = await JsonFile.ReadFromFileAsync<ApiKeyStore>(API_KEYS_FILE); // 改为读取单个对象
                if (apiKeyStore != null && apiKeyStore.Keys.Any())
                {
                    // 检查是否过期（7天）
                    if (DateTime.UtcNow.Subtract(apiKeyStore.GeneratedTime).TotalDays < API_KEYS_EXPIRY_DAYS)
                    {
                        Console.WriteLine("使用现有的 API keys");
                        return apiKeyStore.Keys.ToArray();
                    }
                    else
                    {
                        Console.WriteLine("API keys 已过期，正在生成新的密钥...");
                    }
                }
            }

            // 生成新的 API keys
            var newKeys = GenerateApiKey(DEFAULT_KEY_COUNT);
            await SaveApiKeys(newKeys);
            return newKeys;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"读取或生成 API keys 时发生错误: {ex.Message}");
            // 如果出现错误，生成新的密钥但不保存
            return GenerateApiKey(DEFAULT_KEY_COUNT);
        }
    }

    static async Task SaveApiKeys(string[] newKeys)
    {
        var apiKeyStore = new ApiKeyStore
        {
            GeneratedTime = DateTime.UtcNow,
            Keys = newKeys.ToList()
        };

        try
        {
            // 覆盖保存新的 API keys（不再使用追加模式）
            await JsonFile.WriteToFileAsync(apiKeyStore, API_KEYS_FILE);
            Console.WriteLine($"API keys 已保存到文件: {API_KEYS_FILE}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"保存 API keys 时发生错误: {ex.Message}");
        }
    }

    static string[] GenerateApiKey(int count)
    {
        var apiKeys = new string[count];
        for (int i = 0; i < count; i++)
        {
            var apiKey = new byte[128];
            RandomNumberGenerator.Fill(apiKey);
            apiKeys[i] = Convert.ToBase64String(apiKey);
        }
        Console.WriteLine($"已生成 {count} 个新的 API keys");
        return apiKeys;
    }
}

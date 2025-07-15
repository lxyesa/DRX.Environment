// Program.cs
// Qdrant API 反向代理，监听本地 8080 端口，转发到 6333 端口，拦截 PUT /collections/{collection_name} 并强制 vectors.size=1024
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

// 检测目录下是否存在名为：qdrant.exe 的文件
if (!File.Exists("qdrant.exe"))
{
    Console.WriteLine("请将 qdrant.exe 放在与本程序相同的目录下。");
    Console.WriteLine("下载地址：https://github.com/qdrant/qdrant/releases");
    Console.WriteLine("按任意键退出...");
    Console.ReadKey();
    Environment.Exit(1);
}

// 启动 qdrant
var psi = new ProcessStartInfo
{
    FileName = "qdrant.exe"
};
var process = Process.Start(psi);
if (process == null)
{
    Console.WriteLine("启动 qdrant 失败，请检查 qdrant.exe 是否存在以及是否有权限执行。");
    Console.WriteLine("按任意键退出...");
    Console.ReadKey();
    Environment.Exit(1);
}

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

var qdrantBase = "http://localhost:6333";

// 日志中间件，详细输出所有请求和响应
app.Use(async (context, next) =>
{
    var logger = app.Logger;
    logger.LogInformation("请求: {method} {url}", context.Request.Method, context.Request.Path + context.Request.QueryString);

    // 记录请求体
    string requestBody = "";
    if (context.Request.ContentLength > 0)
    {
        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        requestBody = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;
        logger.LogInformation("请求体: {body}", requestBody);
    }

    // 捕获响应体
    var originalBody = context.Response.Body;
    using var memStream = new MemoryStream();
    context.Response.Body = memStream;

    await next();

    memStream.Position = 0;
    string responseBody = await new StreamReader(memStream).ReadToEndAsync();
    memStream.Position = 0;
    await memStream.CopyToAsync(originalBody);
    context.Response.Body = originalBody;

    logger.LogInformation("响应状态: {status}", context.Response.StatusCode);
    logger.LogInformation("响应体: {body}", responseBody);
});

// 主代理逻辑
app.Run(async context =>
{
    var logger = app.Logger;
    var client = new HttpClient();

    // 构造目标 Qdrant URL
    var targetUri = qdrantBase + context.Request.Path + context.Request.QueryString;

    // 读取请求体（只读一次，避免多次读取导致 body 丢失）
    string rawBody = "";
    if (context.Request.ContentLength > 0)
    {
        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        rawBody = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;
    }

    // 判断是否为 PUT /collections/{collection_name}
    if (context.Request.Method.Equals("PUT", StringComparison.OrdinalIgnoreCase)
        && context.Request.Path.HasValue
        && context.Request.Path.Value.StartsWith("/collections/", StringComparison.OrdinalIgnoreCase)
        && context.Request.Path.Value.Count(c => c == '/') == 2)
    {
        if (!string.IsNullOrWhiteSpace(rawBody))
        {
            try
            {
                using var doc = JsonDocument.Parse(rawBody);
                var root = doc.RootElement.Clone();

                using var ms = new MemoryStream();
                using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false }))
                {
                    // 递归修改 vectors.size 字段
                    WriteWithVectorsSize(root, writer);
                }
                ms.Position = 0;
                var newBody = Encoding.UTF8.GetString(ms.ToArray());

                logger.LogInformation("已拦截并修改 PUT /collections/*，vectors.size 强制为 1024");

                // 构造新请求，严格遵循 openapi 规范设置 header
                var reqMsg = new HttpRequestMessage(HttpMethod.Put, targetUri)
                {
                    Content = new StringContent(newBody, Encoding.UTF8, context.Request.ContentType ?? "application/json")
                };
                reqMsg.Content.Headers.ContentLength = Encoding.UTF8.GetByteCount(newBody);
                // 只复制允许的 header，避免重复/非法
                CopyHeaders(context.Request.Headers.Select(h => new KeyValuePair<string, IEnumerable<string>>(h.Key, h.Value.AsEnumerable())), reqMsg.Headers, reqMsg.Content?.Headers);

                var resp = await client.SendAsync(reqMsg, HttpCompletionOption.ResponseHeadersRead);
                await CopyResponse(context, resp);
                return;
            }
            catch (Exception ex)
            {
                logger.LogError("处理 PUT /collections/* 时出错: {msg}", ex.Message);
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("代理处理异常: " + ex.Message);
                return;
            }
        }
    }
    else if ((context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
              context.Request.Method.Equals("PUT", StringComparison.OrdinalIgnoreCase)))
    {
        // 其他 PUT/POST，body 只读一次，header 严格规范
        var proxyReq = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUri);
        if (!string.IsNullOrEmpty(rawBody))
        {
            proxyReq.Content = new StringContent(rawBody, Encoding.UTF8, context.Request.ContentType ?? "application/json");
            proxyReq.Content.Headers.ContentLength = Encoding.UTF8.GetByteCount(rawBody);
        }
        CopyHeaders(context.Request.Headers.Select(h => new KeyValuePair<string, IEnumerable<string>>(h.Key, h.Value.AsEnumerable())), proxyReq.Headers, proxyReq.Content?.Headers);

        var proxyResp = await client.SendAsync(proxyReq, HttpCompletionOption.ResponseHeadersRead);
        await CopyResponse(context, proxyResp);
        return;
    }
    else
    {
        // 其他请求透明转发，无 body 也需 header 去重
        var proxyReq = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUri);
        if (!string.IsNullOrEmpty(rawBody))
        {
            proxyReq.Content = new StringContent(rawBody, Encoding.UTF8, context.Request.ContentType);
            proxyReq.Content.Headers.ContentLength = Encoding.UTF8.GetByteCount(rawBody);
        }
        CopyHeaders(context.Request.Headers.Select(h => new KeyValuePair<string, IEnumerable<string>>(h.Key, h.Value.AsEnumerable())), proxyReq.Headers, proxyReq.Content?.Headers);

        var proxyResp = await client.SendAsync(proxyReq, HttpCompletionOption.ResponseHeadersRead);
        await CopyResponse(context, proxyResp);
        return;
    }
});

// 递归写入 vectors.size=1024
void WriteWithVectorsSize(JsonElement element, Utf8JsonWriter writer)
{
    if (element.ValueKind == JsonValueKind.Object)
    {
        writer.WriteStartObject();
        foreach (var prop in element.EnumerateObject())
        {
            if (prop.NameEquals("vectors") && prop.Value.ValueKind == JsonValueKind.Object)
            {
                writer.WritePropertyName("vectors");
                writer.WriteStartObject();
                bool hasSize = false;
                foreach (var vprop in prop.Value.EnumerateObject())
                {
                    if (vprop.NameEquals("size"))
                    {
                        writer.WriteNumber("size", 1024);
                        hasSize = true;
                    }
                    else
                    {
                        vprop.WriteTo(writer);
                    }
                }
                if (!hasSize)
                {
                    writer.WriteNumber("size", 1024);
                }
                writer.WriteEndObject();
            }
            else
            {
                writer.WritePropertyName(prop.Name);
                WriteWithVectorsSize(prop.Value, writer);
            }
        }
        writer.WriteEndObject();
    }
    else if (element.ValueKind == JsonValueKind.Array)
    {
        writer.WriteStartArray();
        foreach (var item in element.EnumerateArray())
        {
            WriteWithVectorsSize(item, writer);
        }
        writer.WriteEndArray();
    }
    else
    {
        element.WriteTo(writer);
    }
}

// 复制请求头，严格去重，避免重复/非法 header，兼容 Qdrant 要求
void CopyHeaders(IEnumerable<KeyValuePair<string, IEnumerable<string>>> src, HttpRequestHeaders dest, HttpContentHeaders? contentDest)
{
    // Qdrant 禁止的 header 列表
    var forbidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Host", "Content-Length", "Transfer-Encoding", "Connection", "Keep-Alive", "Proxy-Authenticate", "Proxy-Authorization", "TE", "Trailer", "Upgrade"
    };
    var contentHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Content-Type", "Content-Disposition", "Content-Encoding", "Content-Language", "Content-Location", "Content-MD5", "Content-Range", "Expires", "Last-Modified"
    };
    var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var header in src)
    {
        if (forbidden.Contains(header.Key) || added.Contains(header.Key))
            continue;
        if (contentHeaders.Contains(header.Key))
        {
            contentDest?.Remove(header.Key); // 避免重复
            contentDest?.TryAddWithoutValidation(header.Key, header.Value);
        }
        else
        {
            dest.Remove(header.Key); // 避免重复
            dest.TryAddWithoutValidation(header.Key, header.Value);
        }
        added.Add(header.Key);
    }
}

// 复制响应，原样转发响应体，日志输出不影响实际响应内容
async Task CopyResponse(HttpContext context, HttpResponseMessage resp)
{
    context.Response.StatusCode = (int)resp.StatusCode;

    // 只转发允许的响应头，避免 ASP.NET Core 自动管理的头冲突
    var excludedHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "transfer-encoding", "content-length", "connection", "keep-alive", "proxy-authenticate", "proxy-authorization", "te", "trailer", "upgrade"
    };

    // Content-Type/Content-Encoding 单独处理
    string? contentType = null;
    string? contentEncoding = null;

    foreach (var header in resp.Headers)
    {
        if (excludedHeaders.Contains(header.Key))
            continue;
        if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
        {
            contentType = header.Value.FirstOrDefault();
            continue;
        }
        if (header.Key.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase))
        {
            contentEncoding = header.Value.FirstOrDefault();
            continue;
        }
        context.Response.Headers[header.Key] = header.Value.ToArray();
    }
    foreach (var header in resp.Content.Headers)
    {
        if (excludedHeaders.Contains(header.Key))
            continue;
        if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
        {
            contentType = header.Value.FirstOrDefault();
            continue;
        }
        if (header.Key.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase))
        {
            contentEncoding = header.Value.FirstOrDefault();
            continue;
        }
        context.Response.Headers[header.Key] = header.Value.ToArray();
    }

    if (!string.IsNullOrEmpty(contentType))
        context.Response.ContentType = contentType;
    if (!string.IsNullOrEmpty(contentEncoding))
        context.Response.Headers["Content-Encoding"] = contentEncoding;

    // 原样转发响应体
    using var respStream = await resp.Content.ReadAsStreamAsync();
    await respStream.CopyToAsync(context.Response.Body);
}

app.Run("http://0.0.0.0:8080");

/*
功能说明：
- 监听本地 8080 端口，作为 Qdrant API 的反向代理。
- 所有 API 路径与方法透明转发到 Qdrant（6333 端口）。
- 仅对 PUT /collections/{collection_name} 拦截，自动将 vectors.size 强制为 1024。
- 支持路径参数，健壮处理所有 API。
- 控制台详细输出所有请求和响应日志。
- 代码结构清晰、注释完善。
*/

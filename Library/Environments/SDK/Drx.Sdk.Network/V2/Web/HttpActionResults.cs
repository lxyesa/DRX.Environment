using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.V2.Web
{
    /// <summary>
    /// 常用的 IActionResult 实现集合：ContentResult/HtmlResult/HtmlResultFromFile/JsonResult/FileResult/RedirectResult/StatusResult
    /// 这些实现会被框架在路由执行后通过 ExecuteAsync 转换为 HttpResponse，由框架统一发送。
    /// </summary>
    public static class ActionResults{

    }

    /// <summary>
    /// 文本内容结果（通用）
    /// </summary>
    public class ContentResult : IActionResult
    {
        /// <summary>
        /// 要返回的文本内容
        /// </summary>
        public string Content { get; }

        /// <summary>
        /// Content-Type，例如 "text/plain; charset=utf-8"
        /// </summary>
        public string ContentType { get; }

        /// <summary>
        /// HTTP 状态码
        /// </summary>
        public int StatusCode { get; }

        /// <summary>
        /// 构造一个通用文本结果
        /// </summary>
        /// <param name="content">文本内容</param>
        /// <param name="contentType">Content-Type，若为空默认为 text/plain; charset=utf-8</param>
        /// <param name="statusCode">HTTP 状态码，默认 200</param>
        public ContentResult(string content, string contentType = "text/plain; charset=utf-8", int statusCode = 200)
        {
            Content = content ?? string.Empty;
            ContentType = string.IsNullOrEmpty(contentType) ? "text/plain; charset=utf-8" : contentType;
            StatusCode = statusCode;
        }

        /// <summary>
        /// 将 ContentResult 转换为 HttpResponse。
        /// </summary>
        /// <param name="request">当前请求（可能包含 ListenerContext）</param>
        /// <param name="server">服务器实例，供实现者访问辅助方法或配置</param>
        /// <returns>转换后的 HttpResponse 对象（异步返回）</returns>
        public Task<HttpResponse> ExecuteAsync(HttpRequest request, DrxHttpServer server)
        {
            var resp = new HttpResponse(StatusCode, Content);
            try { resp.Headers.Add("Content-Type", ContentType); } catch { }
            return Task.FromResult(resp);
        }
    }

    /// <summary>
    /// HTML 专用结果（直接返回 HTML 字符串）
    /// </summary>
    public class HtmlResult : ContentResult
    {
        /// <summary>
        /// 构造 HTML 返回结果
        /// </summary>
        /// <param name="html">HTML 字符串</param>
        /// <param name="statusCode">HTTP 状态码，默认 200</param>
        public HtmlResult(string html, int statusCode = 200)
            : base(html ?? string.Empty, "text/html; charset=utf-8", statusCode)
        {
        }
    }

    /// <summary>
    /// HTML 文件结果：根据服务器的 FileRootPath/ResolveFilePath 加载并返回 HTML 文件内容
    /// </summary>
    public class HtmlResultFromFile : IActionResult
    {
        public string PathIndicator { get; }
        public int StatusCode { get; }

        public HtmlResultFromFile(string pathIndicator, int statusCode = 200)
        {
            PathIndicator = pathIndicator;
            StatusCode = statusCode;
        }

        public async Task<HttpResponse> ExecuteAsync(HttpRequest request, DrxHttpServer server)
        {
            var resolved = server?.ResolveFilePath(PathIndicator);
            if (string.IsNullOrEmpty(resolved))
            {
                return new HttpResponse(404, "Not Found");
            }

            try
            {
                using var fs = File.OpenRead(resolved);
                using var sr = new StreamReader(fs, Encoding.UTF8);
                var content = await sr.ReadToEndAsync().ConfigureAwait(false);
                var resp = new HttpResponse(StatusCode, content);
                try { resp.Headers.Add("Content-Type", "text/html; charset=utf-8"); } catch { }
                return resp;
            }
            catch (Exception ex)
            {
                var err = new HttpResponse(500, $"Internal Server Error: {ex.Message}");
                return err;
            }
        }
    }

    /// <summary>
    /// JSON 结果：将对象序列化为 application/json
    /// </summary>
    public class JsonResult : IActionResult
    {
        /// <summary>
        /// 要序列化的对象
        /// </summary>
        public object Value { get; }

        /// <summary>
        /// 序列化选项（可为 null，表示使用默认 System.Text.Json 选项）
        /// </summary>
        public JsonSerializerOptions? Options { get; }

        /// <summary>
        /// HTTP 状态码
        /// </summary>
        public int StatusCode { get; }

        /// <summary>
        /// 构造 JSON 结果
        /// </summary>
        /// <param name="value">要序列化的对象</param>
        /// <param name="statusCode">HTTP 状态码，默认 200</param>
        /// <param name="options">可选的 JsonSerializerOptions</param>
        public JsonResult(object value, int statusCode = 200, JsonSerializerOptions? options = null)
        {
            Value = value;
            StatusCode = statusCode;
            Options = options;
        }

        /// <summary>
        /// 将当前 JsonResult 转换为 HttpResponse（异步）
        /// </summary>
        /// <param name="request">当前请求</param>
        /// <param name="server">服务器实例</param>
        /// <returns>HttpResponse，Content-Type 为 application/json</returns>
        public Task<HttpResponse> ExecuteAsync(HttpRequest request, DrxHttpServer server)
        {
            try
            {
                var json = JsonSerializer.Serialize(Value, Options ?? new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var resp = new HttpResponse(StatusCode, json);
                try { resp.Headers.Add("Content-Type", "application/json; charset=utf-8"); } catch { }
                return Task.FromResult(resp);
            }
            catch (Exception ex)
            {
                // 序列化失败返回 500
                var err = new HttpResponse(500, $"Internal Server Error: {ex.Message}");
                return Task.FromResult(err);
            }
        }
    }

    /// <summary>
    /// 文件下载结果（通过 DrxHttpServer 的 CreateFileResponse 实现流式传输）
    /// </summary>
    public class FileResult : IActionResult
    {
        /// <summary>
        /// 本地文件路径或路径指示符（可由 server.ResolveFilePath 解析）
        /// </summary>
        public string FilePathIndicator { get; }

        /// <summary>
        /// 提示给客户端的文件名（可选）
        /// </summary>
        public string? FileName { get; }

        /// <summary>
        /// Content-Type
        /// </summary>
        public string ContentType { get; }

        /// <summary>
        /// 带宽限制 KB/s，0 表示不限制
        /// </summary>
        public int BandwidthLimitKb { get; }

        /// <summary>
        /// 构造文件结果
        /// </summary>
        public FileResult(string filePathIndicator, string? fileName = null, string contentType = "application/octet-stream", int bandwidthLimitKb = 0)
        {
            FilePathIndicator = filePathIndicator;
            FileName = fileName;
            ContentType = contentType;
            BandwidthLimitKb = bandwidthLimitKb;
        }

        /// <summary>
        /// 将 FileResult 转换为 HttpResponse，使用 DrxHttpServer.CreateFileResponse 以支持流式与带宽限制
        /// </summary>
        public Task<HttpResponse> ExecuteAsync(HttpRequest request, DrxHttpServer server)
        {
            try
            {
                var resolved = server?.ResolveFilePath(FilePathIndicator) ?? FilePathIndicator;
                if (string.IsNullOrEmpty(resolved) || !File.Exists(resolved))
                {
                    return Task.FromResult(new HttpResponse(404, "Not Found"));
                }

                var resp = DrxHttpServer.CreateFileResponse(resolved, FileName, ContentType, BandwidthLimitKb);
                return Task.FromResult(resp);
            }
            catch (Exception ex)
            {
                var err = new HttpResponse(500, $"Internal Server Error: {ex.Message}");
                return Task.FromResult(err);
            }
        }
    }

    /// <summary>
    /// 重定向结果（Location）
    /// </summary>
    public class RedirectResult : IActionResult
    {
        /// <summary>
        /// 目标 URL
        /// </summary>
        public string Location { get; }

        /// <summary>
        /// HTTP 状态码（默认为 302）
        /// </summary>
        public int StatusCode { get; }

        /// <summary>
        /// 构造重定向结果
        /// </summary>
        public RedirectResult(string location, int statusCode = 302)
        {
            Location = location;
            StatusCode = statusCode;
        }

        /// <summary>
        /// 将重定向转换为 HttpResponse（带 Location 头）
        /// </summary>
        public Task<HttpResponse> ExecuteAsync(HttpRequest request, DrxHttpServer server)
        {
            var resp = new HttpResponse(StatusCode, string.Empty);
            try { resp.Headers.Add("Location", Location); } catch { }
            return Task.FromResult(resp);
        }
    }

    /// <summary>
    /// 仅返回状态码的结果（无内容）
    /// </summary>
    public class StatusResult : IActionResult
    {
        /// <summary>
        /// HTTP 状态码
        /// </summary>
        public int StatusCode { get; }

        /// <summary>
        /// 可选的状态描述
        /// </summary>
        public string? StatusDescription { get; }

        /// <summary>
        /// 构造状态结果
        /// </summary>
        public StatusResult(int statusCode, string? statusDescription = null)
        {
            StatusCode = statusCode;
            StatusDescription = statusDescription;
        }

        /// <summary>
        /// 将当前 StatusResult 转换为 HttpResponse
        /// </summary>
        public Task<HttpResponse> ExecuteAsync(HttpRequest request, DrxHttpServer server)
        {
            var resp = new HttpResponse(StatusCode, string.Empty, StatusDescription);
            return Task.FromResult(resp);
        }
    }

    public class OkResult : IActionResult
    {
        public Task<HttpResponse> ExecuteAsync(HttpRequest request, DrxHttpServer server)
        {
            var resp = new HttpResponse(200);
            return Task.FromResult(resp);
        }
    }
}
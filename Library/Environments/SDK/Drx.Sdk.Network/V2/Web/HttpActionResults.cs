using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Drx.Sdk.Shared;

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
                    Logger.Warn($"文件不存在于路径：{resolved}");
                    return Task.FromResult(new HttpResponse(404, "File Not Found"));
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

    /// <summary>
    /// 200 OK 结果
    /// </summary>
    public class OkResult : IActionResult
    {
        public Task<HttpResponse> ExecuteAsync(HttpRequest request, DrxHttpServer server)
        {
            var resp = new HttpResponse(200);
            return Task.FromResult(resp);
        }
    }

    /// <summary>
    /// 404 Not Found 结果
    /// </summary>
    public class NotFoundResult : IActionResult
    {
        public string Message { get; }
        public NotFoundResult(string message = "Not Found") => Message = message;

        /// <summary>
        /// 将 NotFoundResult 转换为 HttpResponse
        /// </summary>
        /// <returns>状态码 404 的 HttpResponse</returns>
        public Task<HttpResponse> ExecuteAsync(HttpRequest request, DrxHttpServer server)
        {
            var resp = new HttpResponse(404, Message);
            return Task.FromResult(resp);
        }
    }

    /// <summary>
    /// 400 Bad Request 结果
    /// </summary>
    public class BadRequestResult : IActionResult
    {
        public string Message { get; }
        public BadRequestResult(string message = "Bad Request") => Message = message;

        /// <summary>
        /// 将 BadRequestResult 转换为 HttpResponse
        /// </summary>
        public Task<HttpResponse> ExecuteAsync(HttpRequest request, DrxHttpServer server)
        {
            var resp = new HttpResponse(400, Message);
            return Task.FromResult(resp);
        }
    }

    /// <summary>
    /// 401 Unauthorized 结果
    /// </summary>
    public class UnauthorizedResult : IActionResult
    {
        public string? Scheme { get; }
        public string? Parameter { get; }

        public UnauthorizedResult(string? scheme = null, string? parameter = null)
        {
            Scheme = scheme; Parameter = parameter;
        }

        /// <summary>
        /// 返回 401 并可附带 WWW-Authenticate 头
        /// </summary>
        public Task<HttpResponse> ExecuteAsync(HttpRequest request, DrxHttpServer server)
        {
            var resp = new HttpResponse(401, "Unauthorized");
            try
            {
                if (!string.IsNullOrEmpty(Scheme)) resp.Headers.Add("WWW-Authenticate", Scheme + (string.IsNullOrEmpty(Parameter) ? string.Empty : " " + Parameter));
            }
            catch { }
            return Task.FromResult(resp);
        }
    }

    /// <summary>
    /// 403 Forbid 结果（拒绝访问）
    /// </summary>
    public class ForbidResult : IActionResult
    {
        public string Message { get; }
        public ForbidResult(string message = "Forbidden") => Message = message;

        public Task<HttpResponse> ExecuteAsync(HttpRequest request, DrxHttpServer server)
        {
            var resp = new HttpResponse(403, Message);
            return Task.FromResult(resp);
        }
    }

    /// <summary>
    /// 201 Created 结果，带 Location 头和可选内容
    /// </summary>
    public class CreatedResult : IActionResult
    {
        public string Location { get; }
        public object? Content { get; }

        public CreatedResult(string location, object? content = null)
        {
            Location = location;
            Content = content;
        }

        /// <summary>
        /// 返回 201，并设置 Location 头；若提供内容则以 application/json 返回
        /// </summary>
        public Task<HttpResponse> ExecuteAsync(HttpRequest request, DrxHttpServer server)
        {
            try
            {
                var resp = new HttpResponse(201, string.Empty);
                try { resp.Headers.Add("Location", Location); } catch { }

                if (Content != null)
                {
                    var json = JsonSerializer.Serialize(Content, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    resp = new HttpResponse(201, json);
                    try { resp.Headers.Add("Content-Type", "application/json; charset=utf-8"); } catch { }
                    try { resp.Headers.Add("Location", Location); } catch { }
                }

                return Task.FromResult(resp);
            }
            catch (Exception ex)
            {
                return Task.FromResult(new HttpResponse(500, $"Internal Server Error: {ex.Message}"));
            }
        }
    }

    /// <summary>
    /// 202 Accepted 结果，可选包含描述或数据
    /// </summary>
    public class AcceptedResult : IActionResult
    {
        public object? Value { get; }
        public AcceptedResult(object? value = null) => Value = value;

        public Task<HttpResponse> ExecuteAsync(HttpRequest request, DrxHttpServer server)
        {
            if (Value == null) return Task.FromResult(new HttpResponse(202, string.Empty));
            try
            {
                var json = JsonSerializer.Serialize(Value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var resp = new HttpResponse(202, json);
                try { resp.Headers.Add("Content-Type", "application/json; charset=utf-8"); } catch { }
                return Task.FromResult(resp);
            }
            catch (Exception ex)
            {
                return Task.FromResult(new HttpResponse(500, $"Internal Server Error: {ex.Message}"));
            }
        }
    }

    /// <summary>
    /// 204 No Content 结果
    /// </summary>
    public class NoContentResult : IActionResult
    {
        public Task<HttpResponse> ExecuteAsync(HttpRequest request, DrxHttpServer server)
        {
            return Task.FromResult(new HttpResponse(204, string.Empty));
        }
    }

    /// <summary>
    /// Problem Details (RFC 7807) 结果，返回 application/problem+json
    /// </summary>
    public class ProblemDetailsResult : IActionResult
    {
        public string? Type { get; }
        public string? Title { get; }
        public int? Status { get; }
        public string? Detail { get; }
        public string? Instance { get; }
        public System.Collections.Generic.Dictionary<string, object?>? Extensions { get; }

        public ProblemDetailsResult(string? title = null, string? detail = null, int? status = null, string? type = null, string? instance = null, System.Collections.Generic.Dictionary<string, object?>? extensions = null)
        {
            Title = title; Detail = detail; Status = status; Type = type; Instance = instance; Extensions = extensions;
        }

        public Task<HttpResponse> ExecuteAsync(HttpRequest request, DrxHttpServer server)
        {
            try
            {
                var obj = new System.Collections.Generic.Dictionary<string, object?>();
                if (Type != null) obj["type"] = Type;
                if (Title != null) obj["title"] = Title;
                if (Status != null) obj["status"] = Status;
                if (Detail != null) obj["detail"] = Detail;
                if (Instance != null) obj["instance"] = Instance;
                if (Extensions != null)
                {
                    foreach (var kv in Extensions) obj[kv.Key] = kv.Value;
                }

                var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var resp = new HttpResponse(Status ?? 500, json);
                try { resp.Headers.Add("Content-Type", "application/problem+json; charset=utf-8"); } catch { }
                return Task.FromResult(resp);
            }
            catch (Exception ex)
            {
                return Task.FromResult(new HttpResponse(500, $"Internal Server Error: {ex.Message}"));
            }
        }
    }

    /// <summary>
    /// 301 Permanent Redirect 结果
    /// </summary>
    public class PermanentRedirectResult : IActionResult
    {
        public string Location { get; }
        public PermanentRedirectResult(string location) => Location = location;

        public Task<HttpResponse> ExecuteAsync(HttpRequest request, DrxHttpServer server)
        {
            var resp = new HttpResponse(301, string.Empty);
            try { resp.Headers.Add("Location", Location); } catch { }
            return Task.FromResult(resp);
        }
    }
}
using Drx.Sdk.Network.Http.Protocol;
using System.IO;
using System.Text;
using System.Text.Json;

namespace DrxPaperclip.Hosting;

/// <summary>
/// 脚本友好型 HTTP 响应工厂。
/// 在 JS/TS 中通过 <c>HttpResponse.file()</c> / <c>HttpResponse.json()</c> 等静态方法构建响应。
/// </summary>
/// <remarks>
/// <para>依赖：Drx.Sdk.Network.Http.Protocol.HttpResponse</para>
/// <para>注册名：HttpResponse（作为宿主类型注册到脚本引擎）</para>
/// </remarks>
public static class ScriptHttpResponse
{
    // ───────────────────────────── MIME 推断 ─────────────────────────────

    private static string GuessMimeType(string extension)
    {
        return (extension?.ToLowerInvariant()) switch
        {
            ".html" or ".htm" => "text/html; charset=utf-8",
            ".css"            => "text/css; charset=utf-8",
            ".js"             => "application/javascript; charset=utf-8",
            ".json"           => "application/json; charset=utf-8",
            ".xml"            => "application/xml; charset=utf-8",
            ".txt"            => "text/plain; charset=utf-8",
            ".csv"            => "text/csv; charset=utf-8",
            ".svg"            => "image/svg+xml",
            ".png"            => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif"            => "image/gif",
            ".ico"            => "image/x-icon",
            ".webp"           => "image/webp",
            ".woff"           => "font/woff",
            ".woff2"          => "font/woff2",
            ".ttf"            => "font/ttf",
            ".otf"            => "font/otf",
            ".pdf"            => "application/pdf",
            ".zip"            => "application/zip",
            ".mp4"            => "video/mp4",
            ".mp3"            => "audio/mpeg",
            ".wasm"           => "application/wasm",
            _                 => "application/octet-stream"
        };
    }

    // ───────────────────────────── 文件路径解析（内部） ─────────────────

    /// <summary>
    /// 当前绑定的服务器实例（用于 <see cref="file"/> 的路径解析）。
    /// 由 <see cref="ScriptHttpServer"/> 在 startAsync 前设置。
    /// </summary>
    internal static Drx.Sdk.Network.Http.DrxHttpServer? BoundServer { get; set; }

    /// <summary>
    /// 解析相对/绝对路径为物理文件路径。
    /// 优先使用绑定的服务器实例的 ResolveFilePath；无实例时回退到工作目录拼接。
    /// </summary>
    private static string? ResolvePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        // 优先用服务器实例解析（包含 ViewRoot / FileRoot 逻辑）
        if (BoundServer != null)
        {
            var resolved = BoundServer.ResolveFilePath(path);
            if (resolved != null) return resolved;
        }

        // 回退：绝对路径直接检查
        if (Path.IsPathRooted(path))
        {
            var abs = Path.GetFullPath(path);
            return File.Exists(abs) ? abs : null;
        }

        // 回退：当前工作目录
        var cwd = Path.GetFullPath(path);
        return File.Exists(cwd) ? cwd : null;
    }

    // ───────────────────────────── 工厂方法 ─────────────────────────────

    /// <summary>
    /// 返回文件内容响应，自动推断 Content-Type。
    /// 路径解析顺序：ViewRoot → FileRoot → 工作目录 → 绝对路径。
    /// </summary>
    /// <param name="path">文件路径（相对或绝对）。</param>
    /// <returns>文件内容的 200 响应；文件不存在返回 404。</returns>
    public static HttpResponse file(string path)
    {
        var resolved = ResolvePath(path);
        if (resolved == null)
            return new HttpResponse(404, $"File not found: {path}");

        try
        {
            var bytes = File.ReadAllBytes(resolved);
            var ext = Path.GetExtension(resolved);
            var resp = new HttpResponse(200) { BodyBytes = bytes };
            resp.Headers["Content-Type"] = GuessMimeType(ext);
            resp.Headers["Content-Length"] = bytes.Length.ToString();
            return resp;
        }
        catch (IOException ex)
        {
            return new HttpResponse(500, $"Failed to read file: {ex.Message}");
        }
    }

    /// <summary>
    /// 返回文件下载响应（Content-Disposition: attachment）。
    /// </summary>
    /// <param name="path">文件路径。</param>
    /// <param name="fileName">下载文件名，<c>null</c> 使用原始文件名。</param>
    public static HttpResponse download(string path, string? fileName = null)
    {
        var resolved = ResolvePath(path);
        if (resolved == null)
            return new HttpResponse(404, $"File not found: {path}");

        try
        {
            var fs = File.OpenRead(resolved);
            var resp = new HttpResponse(200) { FileStream = fs };
            var ext = Path.GetExtension(resolved);
            resp.Headers["Content-Type"] = GuessMimeType(ext);

            var name = fileName ?? Path.GetFileName(resolved);
            resp.Headers["Content-Disposition"] = $"attachment; filename=\"{name}\"";
            return resp;
        }
        catch (IOException ex)
        {
            return new HttpResponse(500, $"Failed to read file: {ex.Message}");
        }
    }

    /// <summary>
    /// 返回 JSON 响应（自动序列化对象）。
    /// </summary>
    /// <param name="data">要序列化的对象。</param>
    /// <param name="statusCode">状态码，默认 200。</param>
    public static HttpResponse json(object? data, int statusCode = 200)
    {
        string body;
        try
        {
            body = data is string s ? s : JsonSerializer.Serialize(data);
        }
        catch
        {
            body = data?.ToString() ?? "null";
        }

        var resp = new HttpResponse(statusCode, body);
        resp.Headers["Content-Type"] = "application/json; charset=utf-8";
        return resp;
    }

    /// <summary>
    /// 返回纯文本响应。
    /// </summary>
    /// <param name="text">文本内容。</param>
    /// <param name="statusCode">状态码，默认 200。</param>
    public static HttpResponse text(string text, int statusCode = 200)
    {
        var resp = new HttpResponse(statusCode, text);
        resp.Headers["Content-Type"] = "text/plain; charset=utf-8";
        return resp;
    }

    /// <summary>
    /// 返回 HTML 响应。
    /// </summary>
    /// <param name="html">HTML 内容字符串。</param>
    /// <param name="statusCode">状态码，默认 200。</param>
    public static HttpResponse html(string html, int statusCode = 200)
    {
        var resp = new HttpResponse(statusCode, html);
        resp.Headers["Content-Type"] = "text/html; charset=utf-8";
        return resp;
    }

    /// <summary>
    /// 返回重定向响应（302）。
    /// </summary>
    /// <param name="url">目标 URL。</param>
    /// <param name="permanent"><c>true</c> 使用 301 永久重定向，默认 302 临时重定向。</param>
    public static HttpResponse redirect(string url, bool permanent = false)
    {
        var code = permanent ? 301 : 302;
        var resp = new HttpResponse(code, string.Empty);
        resp.Headers["Location"] = url;
        return resp;
    }

    /// <summary>
    /// 返回指定状态码的空响应。
    /// </summary>
    /// <param name="statusCode">HTTP 状态码。</param>
    /// <param name="body">可选的响应体。</param>
    public static HttpResponse status(int statusCode, string? body = null)
    {
        return new HttpResponse(statusCode, body ?? string.Empty);
    }

    /// <summary>
    /// 返回 200 OK 响应。
    /// </summary>
    /// <param name="body">可选的响应体。</param>
    public static HttpResponse ok(string? body = null)
    {
        return new HttpResponse(200, body ?? string.Empty);
    }

    /// <summary>
    /// 返回 204 No Content 空响应。
    /// </summary>
    public static HttpResponse noContent()
    {
        return new HttpResponse(204, string.Empty);
    }

    /// <summary>
    /// 返回 400 Bad Request 响应。
    /// </summary>
    /// <param name="message">错误信息。</param>
    public static HttpResponse badRequest(string? message = null)
    {
        return new HttpResponse(400, message ?? "Bad Request");
    }

    /// <summary>
    /// 返回 401 Unauthorized 响应。
    /// </summary>
    /// <param name="message">错误信息。</param>
    public static HttpResponse unauthorized(string? message = null)
    {
        return new HttpResponse(401, message ?? "Unauthorized");
    }

    /// <summary>
    /// 返回 403 Forbidden 响应。
    /// </summary>
    /// <param name="message">错误信息。</param>
    public static HttpResponse forbidden(string? message = null)
    {
        return new HttpResponse(403, message ?? "Forbidden");
    }

    /// <summary>
    /// 返回 404 Not Found 响应。
    /// </summary>
    /// <param name="message">错误信息。</param>
    public static HttpResponse notFound(string? message = null)
    {
        return new HttpResponse(404, message ?? "Not Found");
    }

    /// <summary>
    /// 返回 500 Internal Server Error 响应。
    /// </summary>
    /// <param name="message">错误信息。</param>
    public static HttpResponse serverError(string? message = null)
    {
        return new HttpResponse(500, message ?? "Internal Server Error");
    }
}

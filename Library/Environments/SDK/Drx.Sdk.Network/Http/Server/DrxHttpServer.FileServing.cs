using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Drx.Sdk.Shared;
using Drx.Sdk.Network.Http.Protocol;

namespace Drx.Sdk.Network.Http
{
    /// <summary>
    /// DrxHttpServer 文件服务部分：文件流传输、静态文件服务、文件上传与下载
    /// </summary>
    public partial class DrxHttpServer
    {
        /// <summary>
        /// 尝试以流方式服务文件（支持 Range），如果处理则直接写入 context.Response 并返回 true。
        /// 注意：实际的流写入将在后台异步执行，不会阻塞请求处理线程。
        /// </summary>
        private bool TryServeFileStream(HttpListenerContext context)
        {
            try
            {
                var req = context.Request;
                var path = req.Url?.AbsolutePath ?? "/";

                foreach (var (Prefix, RootDir) in _fileRoutes)
                {
                    if (!path.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) continue;

                    var rel = path.Substring(Prefix.Length);
                    rel = rel.Replace('/', Path.DirectorySeparatorChar);
                    if (rel.Contains(".."))
                    {
                        context.Response.StatusCode = 400;
                        context.Response.StatusDescription = "Bad Request";
                        context.Response.OutputStream.Close();
                        return true;
                    }

                    var filePath = Path.Combine(RootDir, rel);
                    if (!File.Exists(filePath))
                    {
                        context.Response.StatusCode = 404;
                        context.Response.StatusDescription = "Not Found";
                        context.Response.OutputStream.Close();
                        return true;
                    }

                    var fileInfo = new FileInfo(filePath);
                    long totalLength = fileInfo.Length;
                    var rangeHeader = req.Headers["Range"];
                    long start = 0, end = totalLength - 1;
                    bool isPartial = false;

                    if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
                    {
                        var rng = rangeHeader.Substring("bytes=".Length);
                        var parts = rng.Split('-');
                        if (long.TryParse(parts[0], out var s)) start = s;
                        if (parts.Length > 1 && long.TryParse(parts[1], out var e)) end = e;
                        if (start < 0) start = 0;
                        if (end >= totalLength) end = totalLength - 1;
                        if (start <= end) isPartial = true;
                    }

                    var resp = context.Response;
                    resp.AddHeader("Accept-Ranges", "bytes");
                    resp.ContentType = GetMimeType(Path.GetExtension(filePath));
                    resp.SendChunked = false;

                    if (isPartial)
                    {
                        resp.StatusCode = 206;
                        resp.StatusDescription = "Partial Content";
                        resp.AddHeader("Content-Range", $"bytes {start}-{end}/{totalLength}");
                        resp.ContentLength64 = end - start + 1;
                    }
                    else
                    {
                        resp.StatusCode = 200;
                        resp.StatusDescription = "OK";
                        resp.ContentLength64 = totalLength;
                    }

                    try
                    {
                        var fileName = Path.GetFileName(filePath);
                        resp.AddHeader("Content-Disposition", $"attachment; filename=\"{fileName}\"");
                    }
                    catch { }

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await StreamFileToResponseAsync(context, filePath, start, end, isPartial, totalLength).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"后台流式传输文件时发生错误: {ex}");
                            try { context.Response.StatusCode = 500; context.Response.OutputStream.Close(); } catch { }
                        }
                    });

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"TryServeFileStream发生错误: {ex}");
                try { context.Response.StatusCode = 500; context.Response.OutputStream.Close(); } catch { }
                return true;
            }
        }

        /// <summary>
        /// 将指定文件的指定范围异步写入到响应输出流
        /// </summary>
        private async Task StreamFileToResponseAsync(HttpListenerContext context, string filePath, long start, long end, bool isPartial, long totalLength)
        {
            const int BufferSize = 256 * 1024;
            var resp = context.Response;

            try
            {
                var fi = new FileInfo(filePath);
                if (string.IsNullOrEmpty(resp.ContentType))
                {
                    try { resp.ContentType = GetMimeType(Path.GetExtension(filePath)); } catch { }
                }

                try { if (resp.Headers["Accept-Ranges"] == null) resp.AddHeader("Accept-Ranges", "bytes"); } catch { }

                bool hasContentDisposition = false;
                try
                {
                    for (int i = 0; i < resp.Headers.Count; i++)
                    {
                        var k = resp.Headers.GetKey(i);
                        if (string.Equals(k, "Content-Disposition", StringComparison.OrdinalIgnoreCase)) { hasContentDisposition = true; break; }
                    }
                }
                catch { }

                if (!hasContentDisposition)
                {
                    try
                    {
                        var fileName = Path.GetFileName(filePath);
                        var safeName = SanitizeFileNameForHeader(fileName);
                        var disposition = $"attachment; filename=\"{safeName}\"; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
                        resp.AddHeader("Content-Disposition", disposition);
                    }
                    catch { }
                }

                try { if (resp.Headers["Last-Modified"] == null) resp.AddHeader("Last-Modified", fi.LastWriteTimeUtc.ToString("R")); } catch { }
                try { if (resp.Headers["ETag"] == null) resp.AddHeader("ETag", $"\"{fi.Length}-{fi.LastWriteTimeUtc.Ticks}\""); } catch { }
                try { if (resp.Headers["Cache-Control"] == null) resp.AddHeader("Cache-Control", "private, no-cache"); } catch { }
            }
            catch (Exception ex)
            {
                Logger.Warn($"自动附加响应头时发生错误: {ex.Message}");
            }

            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous))
                {
                    fs.Seek(start, SeekOrigin.Begin);
                    var remaining = (isPartial ? (end - start + 1) : totalLength);
                    var buffer = new byte[BufferSize];
                    while (remaining > 0)
                    {
                        int toRead = (int)Math.Min(buffer.Length, remaining);
                        var read = await fs.ReadAsync(buffer.AsMemory(0, toRead)).ConfigureAwait(false);
                        if (read <= 0) break;
                        try
                        {
                            await resp.OutputStream.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            if (IsClientDisconnect(ex))
                            {
                                Logger.Warn($"客户端在流式传输期间断开连接: {ex.Message}");
                                break;
                            }
                            Logger.Warn($"写入响应输出流时发生错误（文件流）: {ex}");
                            break;
                        }
                        remaining -= read;
                    }
                }
            }
            catch (Exception ex)
            {
                if (IsClientDisconnect(ex))
                {
                    Logger.Warn($"流式传输文件时客户端断开: {ex.Message}");
                }
                else
                {
                    Logger.Error($"后台流式传输文件时发生错误: {ex}");
                }
            }

            try { resp.OutputStream.Close(); } catch { }
        }

        private bool TryServeStaticFile(string path, out HttpResponse? response)
        {
            response = null;
            if (string.IsNullOrEmpty(_staticFileRoot) || !path.StartsWith("/static/"))
                return false;

            var filePath = Path.Combine(_staticFileRoot, path.Substring("/static/".Length));
            if (!File.Exists(filePath))
                return false;

            try
            {
                var content = File.ReadAllText(filePath);
                var mimeType = GetMimeType(Path.GetExtension(filePath));
                response = new HttpResponse(200, content);
                response.Headers.Add("Content-Type", mimeType);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"服务静态文件 {filePath} 时发生错误: {ex}");
                return false;
            }
        }

        /// <summary>
        /// 将请求中的上传流保存为文件。
        /// 若未提供 savePath，则使用默认目录 AppContext.BaseDirectory/uploads/mods。
        /// </summary>
        public HttpResponse SaveUploadFile(HttpRequest request, string savePath, string fileName = "upload_")
        {
            if (request?.UploadFile == null || request.UploadFile.Stream == null)
            {
                return new HttpResponse(400, "缺少上传的文件流");
            }

            try
            {
                var upload = request.UploadFile;

                var defaultUploadsDir = Path.Combine(AppContext.BaseDirectory, "uploads");
                if (string.IsNullOrEmpty(savePath))
                {
                    savePath = defaultUploadsDir;
                }

                Directory.CreateDirectory(savePath);

                if (fileName == "upload_")
                {
                    fileName += DateTime.UtcNow.Ticks;
                }

                var filePath = Path.Combine(savePath, fileName);

                using (var outFs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    upload.Stream.CopyTo(outFs);
                }

                return new HttpResponse(200, "上传成功");
            }
            catch (Exception ex)
            {
                Logger.Error($"处理上传时发生错误: {ex}");
                return new HttpResponse(500, $"上传处理失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建一个用于流式下载的 HttpResponse（快捷方法）。
        /// </summary>
        public static HttpResponse CreateFileResponse(string filePath, string? fileName = null, string contentType = "application/octet-stream", int bandwidthLimitKb = 0)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return new HttpResponse(404, "Not Found");
            }

            FileStream fs = null;
            try
            {
                fs = File.OpenRead(filePath);
                var resp = new HttpResponse(200) { FileStream = fs };
                resp.BandwidthLimitKb = bandwidthLimitKb;

                try { resp.Headers.Add("Content-Type", contentType); } catch { }

                if (string.IsNullOrEmpty(fileName)) fileName = Path.GetFileName(filePath);
                var safeName = SanitizeFileNameForHeader(fileName);

                try
                {
                    var disposition = $"attachment; filename=\"{safeName}\"; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
                    resp.Headers.Add("Content-Disposition", disposition);
                }
                catch { }

                return resp;
            }
            catch (Exception ex)
            {
                try { fs?.Dispose(); } catch { }
                Logger.Error($"创建文件响应时发生错误: {ex}");
                return new HttpResponse(500, "Internal Server Error");
            }
        }

        /// <summary>
        /// 打开指定的文件用于读取，并返回一个用于访问其内容的流。
        /// </summary>
        public static Stream GetFileStream(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return null;
            }
            try
            {
                return File.OpenRead(filePath);
            }
            catch (Exception ex)
            {
                Logger.Error($"获取文件流时发生错误: {ex}");
                return null;
            }
        }

        private string GetMimeType(string extension)
        {
            return extension.ToLower() switch
            {
                ".html" => "text/html",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".json" => "application/json",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                _ => "text/plain"
            };
        }
    }
}

using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Drx.Sdk.Shared;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.Performance;

namespace Drx.Sdk.Network.Http
{
    /// <summary>
    /// DrxHttpClient 发送请求部分：SendAsync 重载、SendAsyncInternal、便捷方法（Get/Post/Put/Delete）及辅助方法
    /// </summary>
    public partial class DrxHttpClient
    {
        /// <summary>
        /// 发送 HTTP 请求，使用字符串作为请求体（将以 application/json 发送）。
        /// </summary>
        public async Task<HttpResponse> SendAsync(System.Net.Http.HttpMethod method, string url, string? body = null, NameValueCollection? headers = null, NameValueCollection? query = null)
        {
            return await SendAsyncInternal(method, url, body, null, null, headers, query, null);
        }

        /// <summary>
        /// 发送 HTTP 请求，使用字节数组作为请求体。
        /// </summary>
        public async Task<HttpResponse> SendAsync(System.Net.Http.HttpMethod method, string url, byte[]? bodyBytes, NameValueCollection? headers = null, NameValueCollection? query = null)
        {
            return await SendAsyncInternal(method, url, null, bodyBytes, null, headers, query, null);
        }

        /// <summary>
        /// 发送 HTTP 请求，使用对象作为请求体（将被序列化为 JSON）。
        /// </summary>
        public async Task<HttpResponse> SendAsync(System.Net.Http.HttpMethod method, string url, object? bodyObject, NameValueCollection? headers = null, NameValueCollection? query = null)
        {
            return await SendAsyncInternal(method, url, null, null, bodyObject, headers, query, null);
        }

        /// <summary>
        /// 发送 GET 请求。
        /// </summary>
        public Task<HttpResponse> GetAsync(string url, NameValueCollection? headers = null, NameValueCollection? query = null)
        {
            try
            {
                return SendAsync(System.Net.Http.HttpMethod.Get, url, (string)null, headers, query);
            }
            catch (Exception ex)
            {
                Logger.Error($"发送 GET 请求时发生错误: {url}, 错误: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 发送 POST 请求，支持传入 string/byte[]/object 类型的请求体。
        /// </summary>
        public Task<HttpResponse> PostAsync(string url, object? body = null, NameValueCollection? headers = null, NameValueCollection? query = null)
        {
            try
            {
                if (body is string str)
                    return SendAsync(System.Net.Http.HttpMethod.Post, url, str, headers, query);
                else if (body is byte[] bytes)
                    return SendAsync(System.Net.Http.HttpMethod.Post, url, bytes, headers, query);
                else if (body != null)
                    return SendAsync(System.Net.Http.HttpMethod.Post, url, body, headers, query);
                else
                    return SendAsync(System.Net.Http.HttpMethod.Post, url, (string)null, headers, query);
            }
            catch (Exception ex)
            {
                Logger.Error($"发送 POST 请求时发生错误: {url}, 错误: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 发送 PUT 请求，支持传入 string/byte[]/object 类型的请求体。
        /// </summary>
        public Task<HttpResponse> PutAsync(string url, object? body = null, NameValueCollection? headers = null, NameValueCollection? query = null)
        {
            try
            {
                if (body is string str)
                    return SendAsync(System.Net.Http.HttpMethod.Put, url, str, headers, query);
                else if (body is byte[] bytes)
                    return SendAsync(System.Net.Http.HttpMethod.Put, url, bytes, headers, query);
                else if (body != null)
                    return SendAsync(System.Net.Http.HttpMethod.Put, url, body, headers, query);
                else
                    return SendAsync(System.Net.Http.HttpMethod.Put, url, (string)null, headers, query);
            }
            catch (Exception ex)
            {
                Logger.Error($"发送 PUT 请求时发生错误: {url}, 错误: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 发送 DELETE 请求。
        /// </summary>
        public Task<HttpResponse> DeleteAsync(string url, NameValueCollection? headers = null, NameValueCollection? query = null)
        {
            try
            {
                return SendAsync(System.Net.Http.HttpMethod.Delete, url, (string)null, headers, query);
            }
            catch (Exception ex)
            {
                Logger.Error($"发送 DELETE 请求时发生错误: {url}, 错误: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 统一的 SendAsync 接口，接受自定义 HttpRequest 对象并返回 HttpResponse。
        /// 如果需要上传文件，请设置 HttpRequest.UploadFile 或让方法根据 Body 隐式构建上传描述。
        /// </summary>
        public async Task<HttpResponse> SendAsync(HttpRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            try
            {
                if (request.UploadFile == null)
                {
                    if (request.BodyObject is Stream bodyStream)
                    {
                        var fileName = request.Headers?[Protocol.HttpHeaders.X_FILE_NAME];
                        if (string.IsNullOrEmpty(fileName) && request.Headers != null)
                        {
                            if (!string.IsNullOrEmpty(request.Headers[Protocol.HttpHeaders.X_FILE_NAME_BASE64]))
                            {
                                try { fileName = Encoding.UTF8.GetString(Convert.FromBase64String(request.Headers[Protocol.HttpHeaders.X_FILE_NAME_BASE64]!)); } catch { fileName = request.Headers[Protocol.HttpHeaders.X_FILE_NAME_BASE64]; }
                            }
                            else if (!string.IsNullOrEmpty(request.Headers[Protocol.HttpHeaders.X_FILE_NAME_ENCODED]))
                            {
                                fileName = Uri.UnescapeDataString(request.Headers[Protocol.HttpHeaders.X_FILE_NAME_ENCODED]!);
                            }
                        }

                        request.UploadFile = new HttpRequest.UploadFileDescriptor
                        {
                            Stream = bodyStream,
                            FileName = string.IsNullOrEmpty(fileName) ? "file" : fileName,
                            FieldName = "file",
                            Progress = request.UploadFile?.Progress ?? null,
                            CancellationToken = request.UploadFile?.CancellationToken ?? CancellationToken.None
                        };
                        request.BodyObject = null;
                    }
                    else if (request.BodyBytes != null && request.BodyBytes.Length > 0)
                    {
                        var ms = new MemoryStream(request.BodyBytes, writable: false);
                        var fileName = request.Headers?[Protocol.HttpHeaders.X_FILE_NAME] ?? "file";
                        request.UploadFile = new HttpRequest.UploadFileDescriptor
                        {
                            Stream = ms,
                            FileName = fileName,
                            FieldName = "file",
                            CancellationToken = CancellationToken.None
                        };
                        request.BodyBytes = null;
                    }
                    else if (!string.IsNullOrEmpty(request.Body) && File.Exists(request.Body))
                    {
                        var fs = File.OpenRead(request.Body);
                        var fileName = request.Headers?[Protocol.HttpHeaders.X_FILE_NAME] ?? Path.GetFileName(request.Body);
                        request.UploadFile = new HttpRequest.UploadFileDescriptor
                        {
                            Stream = fs,
                            FileName = fileName,
                            FieldName = "file",
                            CancellationToken = CancellationToken.None
                        };
                        request.Body = null;
                    }
                }
            }
            catch (Exception ex)
            {
                try { Logger.Error($"构建隐式 UploadFile 时发生错误: {ex.Message}"); } catch { }
            }

            System.Net.Http.HttpMethod method;
            try
            {
                method = ParseMethod(request.Method);
            }
            catch
            {
                method = System.Net.Http.HttpMethod.Get;
            }

            return await SendAsyncInternal(method, request.Path ?? request.Url ?? "", request.Body, request.BodyBytes, request.BodyObject, request.Headers, request.Query, request.UploadFile);
        }

        private async Task<HttpResponse> SendAsyncInternal(System.Net.Http.HttpMethod method, string url, string? body, byte[]? bodyBytes, object? bodyObject, NameValueCollection? headers, NameValueCollection? query, HttpRequest.UploadFileDescriptor? uploadFile)
        {
            try
            {
                if (uploadFile != null && uploadFile.Stream != null)
                {
                    var requestUrl = BuildUrl(url, query);
                    using var content = new MultipartFormDataContent();

                    var progressContent = new ProgressableStreamContent(uploadFile.Stream, 81920, uploadFile.Progress, uploadFile.CancellationToken);
                    var streamContent = new StreamContent(progressContent, 81920);
                    streamContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = $"\"{uploadFile.FieldName}\"", FileNameStar = uploadFile.FileName ?? "file" };
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                    content.Add(streamContent, uploadFile.FieldName, uploadFile.FileName ?? "file");

                    try
                    {
                        if (bodyBytes != null && bodyBytes.Length > 0)
                        {
                            var metaBytesContent = new ByteArrayContent(bodyBytes);
                            metaBytesContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "metadata_bytes" };
                            content.Add(metaBytesContent, "metadata_bytes");
                        }
                        else if (bodyObject != null)
                        {
                            var json = JsonSerializer.Serialize(bodyObject);
                            var metaString = new StringContent(json, Encoding.UTF8, "application/json");
                            metaString.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "metadata" };
                            content.Add(metaString, "metadata");
                        }
                        else if (!string.IsNullOrEmpty(body))
                        {
                            var metaString = new StringContent(body, Encoding.UTF8, "application/json");
                            metaString.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "metadata" };
                            content.Add(metaString, "metadata");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"附加 metadata 到 multipart 时发生错误: {ex}");
                    }

                    var uploadRequestMessage = new HttpRequestMessage(method, requestUrl)
                    {
                        Content = content
                    };

                    if (headers != null)
                    {
                        foreach (string key in headers)
                        {
                            if (headers[key] != null)
                                uploadRequestMessage.Headers.Add(key, EnsureAsciiHeaderValue(headers[key]));
                        }
                    }

                    ApplySessionToRequest(uploadRequestMessage);
                    var uploadServerResponse = await _httpClient.SendAsync(uploadRequestMessage, HttpCompletionOption.ResponseContentRead, uploadFile.CancellationToken);
                    var uploadServerResponseBody = await uploadServerResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var uploadServerResponseBytes = await uploadServerResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                    var uploadHttpResponse = new HttpResponse((int)uploadServerResponse.StatusCode, uploadServerResponseBody, uploadServerResponse.ReasonPhrase ?? "");
                    uploadHttpResponse.BodyBytes = uploadServerResponseBytes;
                    try
                    {
                        uploadHttpResponse.BodyObject = JsonSerializer.Deserialize<object>(uploadServerResponseBody);
                    }
                    catch { }

                    foreach (var header in uploadServerResponse.Headers)
                    {
                        uploadHttpResponse.Headers.Add(header.Key, string.Join(",", header.Value));
                    }
                    foreach (var header in uploadServerResponse.Content.Headers)
                    {
                        uploadHttpResponse.Headers.Add(header.Key, string.Join(",", header.Value));
                    }

                    return uploadHttpResponse;
                }

                var requestMessage = new HttpRequestMessage(method, BuildUrl(url, query));

                if (headers != null)
                {
                    foreach (string key in headers)
                    {
                        requestMessage.Headers.Add(key, EnsureAsciiHeaderValue(headers[key]));
                    }
                }

                if (bodyBytes != null)
                {
                    requestMessage.Content = new ByteArrayContent(bodyBytes);
                }
                else if (bodyObject != null)
                {
                    var json = JsonSerializer.Serialize(bodyObject);
                    requestMessage.Content = new StringContent(json, Encoding.UTF8, new MediaTypeHeaderValue("application/json"));
                }
                else if (!string.IsNullOrEmpty(body))
                {
                    requestMessage.Content = new StringContent(body, Encoding.UTF8, "application/json");
                }

                var response = await _httpClient.SendAsync(requestMessage);
                var responseBody = await response.Content.ReadAsStringAsync();
                var responseBytes = await response.Content.ReadAsByteArrayAsync();

                var httpResponse = new HttpResponse((int)response.StatusCode, responseBody, response.ReasonPhrase ?? "");
                httpResponse.BodyBytes = responseBytes;

                try
                {
                    httpResponse.BodyObject = JsonSerializer.Deserialize<object>(responseBody);
                }
                catch
                {
                }

                foreach (var header in response.Headers)
                {
                    httpResponse.Headers.Add(header.Key, string.Join(",", header.Value));
                }

                foreach (var header in response.Content.Headers)
                {
                    httpResponse.Headers.Add(header.Key, string.Join(",", header.Value));
                }

                return httpResponse;
            }
            catch (HttpRequestException ex)
            {
                Logger.Error($"发送 HTTP 请求时发生网络错误: {method}, 错误: {ex.Message}");
                throw;
            }
            catch (TaskCanceledException ex)
            {
                Logger.Error($"发送 HTTP 请求超时: {method}, 错误: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"发送 HTTP 请求时发生未知错误: {method}, 错误: {ex.Message}");
                throw;
            }
        }

        private static System.Net.Http.HttpMethod ParseMethod(string method)
        {
            if (string.IsNullOrEmpty(method)) return System.Net.Http.HttpMethod.Get;
            return method.ToUpper() switch
            {
                "GET" => System.Net.Http.HttpMethod.Get,
                "POST" => System.Net.Http.HttpMethod.Post,
                "PUT" => System.Net.Http.HttpMethod.Put,
                "DELETE" => System.Net.Http.HttpMethod.Delete,
                "PATCH" => System.Net.Http.HttpMethod.Patch,
                _ => new System.Net.Http.HttpMethod(method)
            };
        }

        private string BuildUrl(string url, NameValueCollection? query)
        {
            try
            {
                if (query == null || query.Count == 0)
                    return url;

                var queryString = string.Join("&", query.AllKeys.Where(k => k != null && query[k] != null).Select(key => $"{Uri.EscapeDataString(key!)}={Uri.EscapeDataString(query[key!]!)}"));
                return url.Contains("?") ? $"{url}&{queryString}" : $"{url}?{queryString}";
            }
            catch (Exception ex)
            {
                Logger.Error($"构建 URL 时发生错误: {ex.Message}");
                return url;
            }
        }
    }
}

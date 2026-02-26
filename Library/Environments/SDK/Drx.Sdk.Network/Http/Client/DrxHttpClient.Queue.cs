using System;
using System.Collections.Specialized;
using System.IO;
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
    /// DrxHttpClient 请求队列部分：后台请求处理、队列化执行
    /// </summary>
    public partial class DrxHttpClient
    {
        /// <summary>
        /// 后台请求处理循环，从通道中读取请求任务并分发执行
        /// </summary>
        private async Task ProcessRequestsAsync(CancellationToken token)
        {
            await foreach (var requestTask in _requestChannel.Reader.ReadAllAsync(token))
            {
                await _semaphore.WaitAsync(token);
                _ = Task.Run(() => ExecuteRequestAsync(requestTask), token).ContinueWith(t => _semaphore.Release());
            }
        }

        /// <summary>
        /// 执行单个队列化请求任务
        /// </summary>
        private async Task ExecuteRequestAsync(HttpRequestTask requestTask)
        {
            try
            {
                if (requestTask.UploadFile != null && requestTask.UploadFile.Stream != null)
                {
                    var requestUrl = BuildUrl(requestTask.Url, requestTask.Query);
                    using var content = new MultipartFormDataContent();

                    var progressContent = new ProgressableStreamContent(requestTask.UploadFile.Stream, 81920, requestTask.UploadFile.Progress, requestTask.UploadFile.CancellationToken);
                    var streamContent = new StreamContent(progressContent, 81920);
                    streamContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = $"\"{requestTask.UploadFile.FieldName}\"", FileNameStar = requestTask.UploadFile.FileName ?? "file" };
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                    content.Add(streamContent, requestTask.UploadFile.FieldName, requestTask.UploadFile.FileName ?? "file");

                    try
                    {
                        if (requestTask.BodyBytes != null && requestTask.BodyBytes.Length > 0)
                        {
                            var metaBytesContent = new ByteArrayContent(requestTask.BodyBytes);
                            metaBytesContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "metadata_bytes" };
                            content.Add(metaBytesContent, "metadata_bytes");
                        }
                        else if (requestTask.BodyObject != null)
                        {
                            var json = JsonSerializer.Serialize(requestTask.BodyObject);
                            var metaString = new StringContent(json, Encoding.UTF8, "application/json");
                            metaString.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "metadata" };
                            content.Add(metaString, "metadata");
                        }
                        else if (!string.IsNullOrEmpty(requestTask.Body))
                        {
                            var metaString = new StringContent(requestTask.Body, Encoding.UTF8, "application/json");
                            metaString.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "metadata" };
                            content.Add(metaString, "metadata");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"附加 metadata 到 multipart 时发生错误: {ex}");
                    }

                    var uploadRequestMessage = new HttpRequestMessage(requestTask.Method, requestUrl)
                    {
                        Content = content
                    };

                    if (requestTask.Headers != null)
                    {
                        foreach (string key in requestTask.Headers)
                        {
                            uploadRequestMessage.Headers.Add(key, EnsureAsciiHeaderValue(requestTask.Headers[key]));
                        }
                    }

                    ApplySessionToRequest(uploadRequestMessage);

                    var serverResponse = await _httpClient.SendAsync(uploadRequestMessage, HttpCompletionOption.ResponseContentRead, requestTask.UploadFile.CancellationToken);
                    var serverResponseBody = await serverResponse.Content.ReadAsStringAsync();
                    var serverResponseBytes = await serverResponse.Content.ReadAsByteArrayAsync();

                    var uploadResult = new HttpResponse((int)serverResponse.StatusCode, serverResponseBody, serverResponse.ReasonPhrase ?? "");
                    uploadResult.BodyBytes = serverResponseBytes;

                    try
                    {
                        uploadResult.BodyObject = JsonSerializer.Deserialize<object>(serverResponseBody);
                    }
                    catch { }

                    foreach (var header in serverResponse.Headers)
                    {
                        uploadResult.Headers.Add(header.Key, string.Join(",", header.Value));
                    }
                    foreach (var header in serverResponse.Content.Headers)
                    {
                        uploadResult.Headers.Add(header.Key, string.Join(",", header.Value));
                    }

                    Logger.Info($"上传队列请求完成: {requestTask.Url}, 文件: {requestTask.UploadFile.FileName}, 状态码: {serverResponse.StatusCode}");
                    requestTask.Tcs.SetResult(uploadResult);
                    return;
                }

                var requestMessage = new HttpRequestMessage(requestTask.Method, BuildUrl(requestTask.Url, requestTask.Query));

                if (requestTask.Headers != null)
                {
                    foreach (string key in requestTask.Headers)
                    {
                        requestMessage.Headers.Add(key, EnsureAsciiHeaderValue(requestTask.Headers[key]));
                    }
                }

                ApplySessionToRequest(requestMessage);

                if (requestTask.BodyBytes != null)
                {
                    requestMessage.Content = new ByteArrayContent(requestTask.BodyBytes);
                }
                else if (requestTask.BodyObject != null)
                {
                    var json = JsonSerializer.Serialize(requestTask.BodyObject);
                    requestMessage.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }
                else if (!string.IsNullOrEmpty(requestTask.Body))
                {
                    requestMessage.Content = new StringContent(requestTask.Body, Encoding.UTF8, "application/json");
                }

                var responseNormal = await _httpClient.SendAsync(requestMessage);
                var responseBodyNormal = await responseNormal.Content.ReadAsStringAsync();
                var responseBytesNormal = await responseNormal.Content.ReadAsByteArrayAsync();

                var httpResponseNormal = new HttpResponse((int)responseNormal.StatusCode, responseBodyNormal, responseNormal.ReasonPhrase ?? "");
                httpResponseNormal.BodyBytes = responseBytesNormal;

                try
                {
                    httpResponseNormal.BodyObject = JsonSerializer.Deserialize<object>(responseBodyNormal);
                }
                catch
                {
                }

                foreach (var header in responseNormal.Headers)
                {
                    httpResponseNormal.Headers.Add(header.Key, string.Join(",", header.Value));
                }

                foreach (var header in responseNormal.Content.Headers)
                {
                    httpResponseNormal.Headers.Add(header.Key, string.Join(",", header.Value));
                }

                Logger.Info($"发送请求成功: {requestTask.Method} {requestTask.Url}, 状态码: {responseNormal.StatusCode}");
                requestTask.Tcs.SetResult(httpResponseNormal);
            }
            catch (HttpRequestException ex)
            {
                Logger.Error($"发送 HTTP 请求时发生网络错误: {requestTask.Method} {requestTask.Url}, 错误: {ex.Message}");
                requestTask.Tcs.SetException(ex);
            }
            catch (TaskCanceledException ex)
            {
                Logger.Error($"发送 HTTP 请求超时: {requestTask.Method} {requestTask.Url}, 错误: {ex.Message}");
                requestTask.Tcs.SetException(ex);
            }
            catch (Exception ex)
            {
                Logger.Error($"发送 HTTP 请求时发生未知错误: {requestTask.Method} {requestTask.Url}, 错误: {ex.Message}");
                requestTask.Tcs.SetException(ex);
            }
        }
    }
}

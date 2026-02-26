using System;
using System.IO;
using Drx.Sdk.Shared;
using Drx.Sdk.Network.Http.Protocol;

namespace Drx.Sdk.Network.Http
{
    /// <summary>
    /// DrxHttpClient 下载部分：文件下载与流式下载
    /// </summary>
    public partial class DrxHttpClient
    {
        /// <summary>
        /// 下载远程文件到指定本地路径，支持进度回调和取消操作，并在可能时进行原子替换目标文件。
        /// </summary>
        /// <param name="url">文件的远程 URL。</param>
        /// <param name="destPath">本地目标路径，下载完成后会尝试原子替换该文件（若已存在）。</param>
        /// <param name="progress">可选进度回调，报告已下载字节数。</param>
        /// <param name="cancellationToken">可选取消令牌。</param>
        /// <returns>异步任务，完成或抛出异常以指示失败。</returns>
        /// <exception cref="OperationCanceledException">下载被取消时抛出。</exception>
        /// <exception cref="System.Exception">下载或写入文件时发生错误会向上抛出。</exception>
        public async Task DownloadFileAsync(string url, string destPath, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
        {
            try
            {
                using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
                var response = await _httpClient.SendAsync(request, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength ?? -1L;
                var tempFile = destPath + ".download" + Guid.NewGuid().ToString("N");

                var destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

                using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    var buffer = new byte[81920];
                    long totalRead = 0;
                    int read;
                    while ((read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                        totalRead += read;
                        progress?.Report(totalRead);
                    }
                }

                try
                {
                    if (File.Exists(destPath))
                    {
                        try
                        {
                            File.Replace(tempFile, destPath, null);
                        }
                        catch (PlatformNotSupportedException)
                        {
                            File.Delete(destPath);
                            File.Move(tempFile, destPath);
                        }
                        catch (IOException)
                        {
                            try
                            {
                                File.Delete(destPath);
                                File.Move(tempFile, destPath);
                            }
                            catch (Exception inner)
                            {
                                Logger.Error($"替换文件失败（尝试 File.Delete+Move）: {inner.Message}");
                                throw;
                            }
                        }
                    }
                    else
                    {
                        File.Move(tempFile, destPath);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"下载完成后替换目标文件时发生错误: {ex.Message}");
                    try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
                    throw;
                }

                Logger.Info($"下载文件成功: {url} -> {destPath} (总字节: {total})");
            }
            catch (OperationCanceledException)
            {
                Logger.Warn($"下载被取消: {url}");
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"下载文件失败: {url}, 错误: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 将远程文件流写入到目标流，支持进度回调与取消操作。方法不会关闭传入的目标流，调用方负责流的生命周期。
        /// </summary>
        /// <param name="url">文件的远程 URL。</param>
        /// <param name="destination">目标写入流，不能为 null。</param>
        /// <param name="progress">可选进度回调，报告已下载字节数。</param>
        /// <param name="cancellationToken">可选取消令牌。</param>
        /// <returns>异步任务，完成或抛出异常以指示失败。</returns>
        /// <exception cref="System.ArgumentNullException">当 destination 为 null 时抛出。</exception>
        public async Task DownloadToStreamAsync(string url, Stream destination, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            try
            {
                using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
                var response = await _httpClient.SendAsync(request, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

#if DEBUG
                Logger.Debug($"响应头: {response.Headers}");
                Logger.Debug($"内容头: {response.Content.Headers}");
                Logger.Debug($"状态码: {response.StatusCode}");
#endif

                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength ?? -1L;

                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                var buffer = new byte[81920];
                long totalRead = 0;
                int read;
                while ((read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    totalRead += read;
                    progress?.Report(totalRead);
                }

                Logger.Info($"DownloadToStreamAsync 完成: {url} (总字节: {total})");
            }
            catch (OperationCanceledException)
            {
                Logger.Warn($"下载被取消: {url}");
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error($"下载流式文件失败: {url}, 错误: {ex.Message}");
                throw;
            }
        }
    }
}

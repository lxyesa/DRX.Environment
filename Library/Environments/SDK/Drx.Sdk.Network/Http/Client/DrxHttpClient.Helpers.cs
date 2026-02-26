using System;
using Drx.Sdk.Shared;

namespace Drx.Sdk.Network.Http
{
    /// <summary>
    /// DrxHttpClient 辅助工具部分：默认头设置、超时配置、资源释放
    /// </summary>
    public partial class DrxHttpClient
    {
        /// <summary>
        /// 设置默认请求头，影响后续发送的请求。
        /// </summary>
        /// <param name="name">头部名称。</param>
        /// <param name="value">头部值（会确保为 ASCII，可自动转义不可 ASCII 字符）。</param>
        public void SetDefaultHeader(string name, string value)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Add(name, EnsureAsciiHeaderValue(value));
                Logger.Info($"设置默认请求头: {name} = {value}");
            }
            catch (Exception ex)
            {
                Logger.Error($"设置默认请求头时发生错误: {name}, 错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置底层 HttpClient 的超时时间。
        /// </summary>
        /// <param name="timeout">超时时间。</param>
        public void SetTimeout(TimeSpan timeout)
        {
            try
            {
                _httpClient.Timeout = timeout;
                Logger.Info($"设置超时时间: {timeout.TotalSeconds} 秒");
            }
            catch (Exception ex)
            {
                Logger.Error($"设置超时时间时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 异步释放本实例占用的资源并停止后台请求处理。
        /// </summary>
        /// <returns>表示释放完成的可等待结构。</returns>
        public async ValueTask DisposeAsync()
        {
            try
            {
                _requestChannel.Writer.TryComplete();
                _cts.Cancel();

                if (_processingTask != null)
                {
                    try
                    {
                        await _processingTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }
            }
            finally
            {
                _httpClient?.Dispose();
                _semaphore?.Dispose();
                _cts?.Dispose();
            }
        }
    }
}

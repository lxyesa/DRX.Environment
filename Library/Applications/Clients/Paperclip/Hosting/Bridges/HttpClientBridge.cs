// Copyright (c) DRX SDK — Paperclip HttpClient 脚本桥接层
// 职责：将 DrxHttpClient 的常用 HTTP 请求能力导出到 JS/TS 脚本
// 关键依赖：Drx.Sdk.Network.Http.DrxHttpClient, Drx.Sdk.Network.Http.Protocol.HttpResponse

using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Drx.Sdk.Network.Http;
using Drx.Sdk.Network.Http.Protocol;

namespace DrxPaperclip.Hosting;

/// <summary>
/// HTTP 客户端脚本桥接层，提供 fetch 风格的静态 API 供 JS/TS 使用。
/// 内部持有一个共享 <see cref="DrxHttpClient"/> 实例，脚本也可通过 <see cref="create"/> 创建独立实例。
/// </summary>
public static class HttpClientBridge
{
    private static readonly Lazy<DrxHttpClient> SharedClient = new(() => new DrxHttpClient());

    #region 工厂方法

    /// <summary>
    /// 创建一个新的独立 HttpClient 实例（可指定 baseAddress）。
    /// </summary>
    public static DrxHttpClient create(string? baseAddress = null)
        => string.IsNullOrWhiteSpace(baseAddress) ? new DrxHttpClient() : new DrxHttpClient(baseAddress);

    #endregion

    #region 便捷请求 — 共享实例

    /// <summary>发送 GET 请求。</summary>
    public static Task<HttpResponse> get(string url)
        => SharedClient.Value.GetAsync(url);

    /// <summary>发送 GET 请求（附自定义头）。</summary>
    public static Task<HttpResponse> getWithHeaders(string url, object? headers)
        => SharedClient.Value.GetAsync(url, ToHeaders(headers));

    /// <summary>发送 POST 请求（body 可为 string/object）。</summary>
    public static Task<HttpResponse> post(string url, object? body = null)
        => SharedClient.Value.PostAsync(url, body);

    /// <summary>发送 POST 请求（附自定义头）。</summary>
    public static Task<HttpResponse> postWithHeaders(string url, object? body, object? headers)
        => SharedClient.Value.PostAsync(url, body, ToHeaders(headers));

    /// <summary>发送 PUT 请求。</summary>
    public static Task<HttpResponse> put(string url, object? body = null)
        => SharedClient.Value.PutAsync(url, body);

    /// <summary>发送 PUT 请求（附自定义头）。</summary>
    public static Task<HttpResponse> putWithHeaders(string url, object? body, object? headers)
        => SharedClient.Value.PutAsync(url, body, ToHeaders(headers));

    /// <summary>发送 DELETE 请求。</summary>
    public static Task<HttpResponse> del(string url)
        => SharedClient.Value.DeleteAsync(url);

    /// <summary>发送 DELETE 请求（附自定义头）。</summary>
    public static Task<HttpResponse> delWithHeaders(string url, object? headers)
        => SharedClient.Value.DeleteAsync(url, null, ToHeaders(headers));

    #endregion

    #region 文件传输 — 共享实例

    /// <summary>下载文件到指定路径。</summary>
    public static Task downloadFile(string url, string destPath)
        => SharedClient.Value.DownloadFileAsync(url, destPath, progress: null, cancellationToken: default);

    /// <summary>上传本地文件。</summary>
    public static Task<HttpResponse> uploadFile(string url, string filePath, string fieldName = "file")
        => SharedClient.Value.UploadFileAsync(url, filePath, fieldName);

    #endregion

    #region 实例方法代理 — 让脚本可对 create() 返回的实例操作

    /// <summary>用指定实例发送 GET。</summary>
    public static Task<HttpResponse> instanceGet(DrxHttpClient client, string url)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.GetAsync(url);
    }

    /// <summary>用指定实例发送 POST。</summary>
    public static Task<HttpResponse> instancePost(DrxHttpClient client, string url, object? body = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.PostAsync(url, body);
    }

    /// <summary>用指定实例发送 PUT。</summary>
    public static Task<HttpResponse> instancePut(DrxHttpClient client, string url, object? body = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.PutAsync(url, body);
    }

    /// <summary>用指定实例发送 DELETE。</summary>
    public static Task<HttpResponse> instanceDelete(DrxHttpClient client, string url)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.DeleteAsync(url);
    }

    /// <summary>设置默认请求头。</summary>
    public static void setDefaultHeader(DrxHttpClient client, string name, string value)
    {
        ArgumentNullException.ThrowIfNull(client);
        client.SetDefaultHeader(name, value);
    }

    /// <summary>设置超时（秒）。</summary>
    public static void setTimeout(DrxHttpClient client, double seconds)
    {
        ArgumentNullException.ThrowIfNull(client);
        client.SetTimeout(TimeSpan.FromSeconds(seconds));
    }

    /// <summary>用指定实例下载文件。</summary>
    public static Task instanceDownloadFile(DrxHttpClient client, string url, string destPath)
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.DownloadFileAsync(url, destPath, progress: null, cancellationToken: default);
    }

    /// <summary>用指定实例上传文件。</summary>
    public static Task<HttpResponse> instanceUploadFile(DrxHttpClient client, string url, string filePath, string fieldName = "file")
    {
        ArgumentNullException.ThrowIfNull(client);
        return client.UploadFileAsync(url, filePath, fieldName);
    }

    #endregion

    #region 辅助

    /// <summary>
    /// 将脚本传入的 headers 对象（JS 对象 / ExpandoObject / IDictionary）转为 NameValueCollection。
    /// </summary>
    private static NameValueCollection? ToHeaders(object? headersObj)
    {
        if (headersObj is null) return null;

        var nvc = new NameValueCollection();

        if (headersObj is System.Collections.IDictionary dict)
        {
            foreach (var key in dict.Keys)
            {
                var k = key?.ToString();
                if (!string.IsNullOrEmpty(k))
                    nvc[k] = dict[key!]?.ToString() ?? string.Empty;
            }
            return nvc;
        }

        // ExpandoObject / 其他动态类型
        if (headersObj is System.Collections.Generic.IDictionary<string, object?> expandoDict)
        {
            foreach (var kvp in expandoDict)
            {
                if (!string.IsNullOrEmpty(kvp.Key))
                    nvc[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
            }
            return nvc;
        }

        return null;
    }

    #endregion
}

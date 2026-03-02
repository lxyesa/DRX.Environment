using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Drx.Sdk.Network.Http;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Shared;

/// <summary>
/// DrxHttpClient 原生导出函数，供 C++ 通过 LoadLibrary / GetProcAddress 调用。
/// <para>
/// 所有字符串参数通过 UTF-8 byte* + 长度传入。
/// 请求头通过 JSON 字符串传入，格式: {"Header-Name":"value", ...}
/// 响应通过句柄模式返回，由 C++ 端按需读取后销毁。
/// </para>
/// <para>
/// 导出表索引（供 GetDrxHttpClientExports 使用）：
///  [0]  DrxHttp_Create
///  [1]  DrxHttp_CreateWithBaseUrl
///  [2]  DrxHttp_SetDefaultHeader
///  [3]  DrxHttp_SetTimeout
///  [4]  DrxHttp_Destroy
///  [5]  DrxHttp_Get
///  [6]  DrxHttp_Post
///  [7]  DrxHttp_PostBytes
///  [8]  DrxHttp_Put
///  [9]  DrxHttp_Delete
///  [10] DrxHttp_Send
///  [11] DrxHttp_DownloadFile
///  [12] DrxHttp_UploadFile
///  [13] DrxHttp_Response_GetStatusCode
///  [14] DrxHttp_Response_GetBodyLength
///  [15] DrxHttp_Response_ReadBody
///  [16] DrxHttp_Response_ReadHeader
///  [17] DrxHttp_Response_GetHeaderLength
///  [18] DrxHttp_Response_Destroy
///  [19] DrxHttp_FreeBuffer
///  [20] DrxHttp_GetLastError
///  [21] GetDrxHttpClientExports
/// </para>
/// </summary>
public static class DrxHttpClientExport
{
    // ========================================================================
    // 日志记录工具
    // ========================================================================
    private enum LogLevel { Trace, Debug, Info, Warning, Error }

    private static void LogMessage(LogLevel level, string functionName, string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var prefix = $"[{timestamp}][DrxHttpClientExport.{level.ToString().ToUpper()}]";
        var fullMessage = $"{prefix} {functionName}: {message}";
        try { Console.WriteLine(fullMessage); } catch { }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LogTrace(string func, string msg) => LogMessage(LogLevel.Trace, func, msg);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LogDebug(string func, string msg) => LogMessage(LogLevel.Debug, func, msg);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LogInfo(string func, string msg) => LogMessage(LogLevel.Info, func, msg);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LogWarning(string func, string msg) => LogMessage(LogLevel.Warning, func, msg);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void LogError(string func, string msg) => LogMessage(LogLevel.Error, func, msg);
    // ========================================================================
    // 内部响应包装
    // ========================================================================
    private sealed class NativeHttpResponse
    {
        public int StatusCode;
        public byte[] BodyBytes;
        public string Body;
        public NameValueCollection Headers;

        public NativeHttpResponse(HttpResponse resp)
        {
            StatusCode = resp.StatusCode;
            BodyBytes = resp.BodyBytes ?? (resp.Body != null ? Encoding.UTF8.GetBytes(resp.Body) : Array.Empty<byte>());
            Body = resp.Body ?? string.Empty;
            Headers = resp.Headers ?? new NameValueCollection();
        }
    }

    // ========================================================================
    // 句柄封送（与 DrxTcpClientExport 一致）
    // ========================================================================
    private static IntPtr ToPtr(object obj)
    {
        var h = GCHandle.Alloc(obj, GCHandleType.Normal);
        return GCHandle.ToIntPtr(h);
    }

    private static T? FromPtr<T>(IntPtr ptr) where T : class
    {
        if (ptr == IntPtr.Zero) return null;
        var h = GCHandle.FromIntPtr(ptr);
        return h.Target as T;
    }

    private static void FreePtr(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero) return;
        var h = GCHandle.FromIntPtr(ptr);
        if (h.IsAllocated) h.Free();
    }

    // ========================================================================
    // UTF-8 辅助
    // ========================================================================
    private static unsafe string PtrToString(byte* data, int len)
    {
        if (data == null || len <= 0) return string.Empty;
        return Encoding.UTF8.GetString(new ReadOnlySpan<byte>(data, len));
    }

    // ========================================================================
    // 最后一次错误（线程局部）
    // ========================================================================
    [ThreadStatic]
    private static string? s_lastError;

    private static void SetLastError(string msg)
    {
        s_lastError = msg;
        LogError("SetLastError", msg);
    }

    // ========================================================================
    // Headers JSON 解析辅助
    // ========================================================================
    private static unsafe NameValueCollection? ParseHeadersJson(byte* headersJson, int headersJsonLen)
    {
        if (headersJson == null || headersJsonLen <= 0) return null;
        try
        {
            var json = PtrToString(headersJson, headersJsonLen);
            var node = JsonNode.Parse(json) as JsonObject;
            if (node == null) return null;

            var nvc = new NameValueCollection();
            foreach (var kv in node)
            {
                nvc.Add(kv.Key, kv.Value?.ToString() ?? string.Empty);
            }
            return nvc;
        }
        catch (Exception ex)
        {
            SetLastError($"ParseHeadersJson failed: {ex.Message}");
            return null;
        }
    }

    // ========================================================================
    // 客户端生命周期
    // ========================================================================

    /// <summary>
    /// 创建 HTTP 客户端实例，返回句柄。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxHttp_Create")]
    public static IntPtr Create()
    {
        const string funcName = "DrxHttp_Create";
        LogTrace(funcName, "Entering");
        try
        {
            var client = new DrxHttpClient();
            LogInfo(funcName, "DrxHttpClient instance created successfully");
            var ptr = ToPtr(client);
            LogTrace(funcName, $"Returning handle: 0x{ptr:X}");
            return ptr;
        }
        catch (Exception ex)
        {
            var errMsg = $"Failed to create client: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// 创建带有基础地址的 HTTP 客户端实例。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxHttp_CreateWithBaseUrl")]
    public static unsafe IntPtr CreateWithBaseUrl(byte* urlUtf8, int urlLen)
    {
        const string funcName = "DrxHttp_CreateWithBaseUrl";
        LogTrace(funcName, "Entering");
        try
        {
            var url = PtrToString(urlUtf8, urlLen);
            LogDebug(funcName, $"BaseUrl: '{url}' (length={urlLen})");
            var client = new DrxHttpClient(url);
            LogInfo(funcName, "DrxHttpClient with base URL created successfully");
            var ptr = ToPtr(client);
            LogTrace(funcName, $"Returning handle: 0x{ptr:X}");
            return ptr;
        }
        catch (Exception ex)
        {
            var errMsg = $"Failed to create client with base URL: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// 设置默认请求头。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxHttp_SetDefaultHeader")]
    public static unsafe void SetDefaultHeader(IntPtr clientPtr, byte* nameUtf8, int nameLen, byte* valueUtf8, int valueLen)
    {
        const string funcName = "DrxHttp_SetDefaultHeader";
        LogTrace(funcName, $"Entering with client=0x{clientPtr:X}");
        try
        {
            var client = FromPtr<DrxHttpClient>(clientPtr);
            if (client == null) { LogWarning(funcName, "Invalid client handle"); SetLastError("SetDefaultHeader: invalid client handle"); return; }
            
            var name = PtrToString(nameUtf8, nameLen);
            var value = PtrToString(valueUtf8, valueLen);
            LogDebug(funcName, $"Setting header: {name} = {value}");
            
            client.SetDefaultHeader(name, value);
            LogInfo(funcName, $"Default header '{name}' set successfully");
        }
        catch (Exception ex)
        {
            var errMsg = $"Failed to set default header: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
        }
    }

    /// <summary>
    /// 设置请求超时时间（毫秒）。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxHttp_SetTimeout")]
    public static void SetTimeout(IntPtr clientPtr, int timeoutMs)
    {
        const string funcName = "DrxHttp_SetTimeout";
        LogTrace(funcName, $"Entering with client=0x{clientPtr:X}, timeout={timeoutMs}ms");
        try
        {
            var client = FromPtr<DrxHttpClient>(clientPtr);
            if (client == null) { LogWarning(funcName, "Invalid client handle"); SetLastError("SetTimeout: invalid client handle"); return; }
            
            LogDebug(funcName, $"Setting timeout to {timeoutMs}ms");
            client.SetTimeout(TimeSpan.FromMilliseconds(timeoutMs));
            LogInfo(funcName, $"Timeout set to {timeoutMs}ms");
        }
        catch (Exception ex)
        {
            var errMsg = $"Failed to set timeout: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
        }
    }

    /// <summary>
    /// 销毁 HTTP 客户端实例并释放句柄。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxHttp_Destroy")]
    public static void Destroy(IntPtr clientPtr)
    {
        const string funcName = "DrxHttp_Destroy";
        LogTrace(funcName, $"Entering with client=0x{clientPtr:X}");
        try
        {
            var client = FromPtr<DrxHttpClient>(clientPtr);
            if (client != null)
            {
                try { 
                    LogDebug(funcName, "Disposing HTTP client");
                    client.DisposeAsync().AsTask().Wait(5000);
                    LogInfo(funcName, "HTTP client disposed successfully");
                } 
                catch (Exception disposalEx) 
                { 
                    LogWarning(funcName, $"Error during disposal: {disposalEx.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            LogError(funcName, $"Unexpected error: {ex.GetType().Name} - {ex.Message}");
        }
        finally
        {
            FreePtr(clientPtr);
            LogInfo(funcName, $"Handle 0x{clientPtr:X} freed successfully");
        }
    }

    // ========================================================================
    // HTTP 请求方法（全部返回响应句柄 IntPtr）
    // ========================================================================

    /// <summary>
    /// 发送 GET 请求。headersJson 为 UTF-8 JSON 字符串 {"key":"value",...}，可为 null/0。
    /// 返回响应句柄，失败返回 IntPtr.Zero。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxHttp_Get")]
    public static unsafe IntPtr Get(IntPtr clientPtr, byte* urlUtf8, int urlLen, byte* headersJson, int headersJsonLen)
    {
        const string funcName = "DrxHttp_Get";
        LogTrace(funcName, $"Entering with client=0x{clientPtr:X}");
        var sw = Stopwatch.StartNew();
        try
        {
            var client = FromPtr<DrxHttpClient>(clientPtr);
            if (client == null) { LogWarning(funcName, "Invalid client handle"); SetLastError("Get: invalid client handle"); return IntPtr.Zero; }

            var url = PtrToString(urlUtf8, urlLen);
            LogDebug(funcName, $"URL: {url} (length={urlLen})");
            
            var headers = ParseHeadersJson(headersJson, headersJsonLen);
            if (headers != null) LogDebug(funcName, $"Headers count: {headers.Count}");

            var resp = Task.Run(() => client.GetAsync(url, headers)).GetAwaiter().GetResult();
            sw.Stop();
            
            LogInfo(funcName, $"GET request completed in {sw.ElapsedMilliseconds}ms with status {resp.StatusCode}");
            
            return ToPtr(new NativeHttpResponse(resp));
        }
        catch (Exception ex)
        {
            sw.Stop();
            var errMsg = $"GET request failed after {sw.ElapsedMilliseconds}ms: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// 发送 POST 请求，请求体为 UTF-8 字符串（通常是 JSON）。
    /// 返回响应句柄。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxHttp_Post")]
    public static unsafe IntPtr Post(IntPtr clientPtr, byte* urlUtf8, int urlLen, byte* bodyUtf8, int bodyLen, byte* headersJson, int headersJsonLen)
    {
        const string funcName = "DrxHttp_Post";
        LogTrace(funcName, $"Entering with client=0x{clientPtr:X}");
        var sw = Stopwatch.StartNew();
        try
        {
            var client = FromPtr<DrxHttpClient>(clientPtr);
            if (client == null) { LogWarning(funcName, "Invalid client handle"); SetLastError("Post: invalid client handle"); return IntPtr.Zero; }

            var url = PtrToString(urlUtf8, urlLen);
            LogDebug(funcName, $"URL: {url}, bodyLen={bodyLen}");
            
            var body = PtrToString(bodyUtf8, bodyLen);
            var headers = ParseHeadersJson(headersJson, headersJsonLen);
            if (headers != null) LogDebug(funcName, $"Headers count: {headers.Count}");

            var resp = Task.Run(() => client.PostAsync(url, body, headers)).GetAwaiter().GetResult();
            sw.Stop();
            
            LogInfo(funcName, $"POST request completed in {sw.ElapsedMilliseconds}ms with status {resp.StatusCode}");
            
            return ToPtr(new NativeHttpResponse(resp));
        }
        catch (Exception ex)
        {
            sw.Stop();
            var errMsg = $"POST request failed after {sw.ElapsedMilliseconds}ms: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// 发送 POST 请求，请求体为原始字节数组。
    /// 返回响应句柄。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxHttp_PostBytes")]
    public static unsafe IntPtr PostBytes(IntPtr clientPtr, byte* urlUtf8, int urlLen, byte* bodyData, int bodyLen, byte* headersJson, int headersJsonLen)
    {
        const string funcName = "DrxHttp_PostBytes";
        LogTrace(funcName, $"Entering with client=0x{clientPtr:X}");
        var sw = Stopwatch.StartNew();
        try
        {
            var client = FromPtr<DrxHttpClient>(clientPtr);
            if (client == null) { LogWarning(funcName, "Invalid client handle"); SetLastError("PostBytes: invalid client handle"); return IntPtr.Zero; }

            var url = PtrToString(urlUtf8, urlLen);
            LogDebug(funcName, $"URL: {url}, bodyLen={bodyLen}");
            
            byte[] bodyBytes = Array.Empty<byte>();
            if (bodyData != null && bodyLen > 0)
            {
                bodyBytes = new byte[bodyLen];
                new ReadOnlySpan<byte>(bodyData, bodyLen).CopyTo(bodyBytes);
            }
            var headers = ParseHeadersJson(headersJson, headersJsonLen);
            if (headers != null) LogDebug(funcName, $"Headers count: {headers.Count}");

            var resp = Task.Run(() => client.SendAsync(System.Net.Http.HttpMethod.Post, url, bodyBytes, headers)).GetAwaiter().GetResult();
            sw.Stop();
            
            LogInfo(funcName, $"POST (bytes) request completed in {sw.ElapsedMilliseconds}ms with status {resp.StatusCode}");
            
            return ToPtr(new NativeHttpResponse(resp));
        }
        catch (Exception ex)
        {
            sw.Stop();
            var errMsg = $"POST (bytes) request failed after {sw.ElapsedMilliseconds}ms: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// 发送 PUT 请求，请求体为 UTF-8 字符串。
    /// 返回响应句柄。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxHttp_Put")]
    public static unsafe IntPtr Put(IntPtr clientPtr, byte* urlUtf8, int urlLen, byte* bodyUtf8, int bodyLen, byte* headersJson, int headersJsonLen)
    {
        const string funcName = "DrxHttp_Put";
        LogTrace(funcName, $"Entering with client=0x{clientPtr:X}");
        var sw = Stopwatch.StartNew();
        try
        {
            var client = FromPtr<DrxHttpClient>(clientPtr);
            if (client == null) { LogWarning(funcName, "Invalid client handle"); SetLastError("Put: invalid client handle"); return IntPtr.Zero; }

            var url = PtrToString(urlUtf8, urlLen);
            LogDebug(funcName, $"URL: {url}, bodyLen={bodyLen}");
            
            var body = PtrToString(bodyUtf8, bodyLen);
            var headers = ParseHeadersJson(headersJson, headersJsonLen);

            var resp = Task.Run(() => client.PutAsync(url, body, headers)).GetAwaiter().GetResult();
            sw.Stop();
            
            LogInfo(funcName, $"PUT request completed in {sw.ElapsedMilliseconds}ms with status {resp.StatusCode}");
            
            return ToPtr(new NativeHttpResponse(resp));
        }
        catch (Exception ex)
        {
            sw.Stop();
            var errMsg = $"PUT request failed after {sw.ElapsedMilliseconds}ms: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// 发送 DELETE 请求。
    /// 返回响应句柄。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxHttp_Delete")]
    public static unsafe IntPtr Delete(IntPtr clientPtr, byte* urlUtf8, int urlLen, byte* headersJson, int headersJsonLen)
    {
        const string funcName = "DrxHttp_Delete";
        LogTrace(funcName, $"Entering with client=0x{clientPtr:X}");
        var sw = Stopwatch.StartNew();
        try
        {
            var client = FromPtr<DrxHttpClient>(clientPtr);
            if (client == null) { LogWarning(funcName, "Invalid client handle"); SetLastError("Delete: invalid client handle"); return IntPtr.Zero; }

            var url = PtrToString(urlUtf8, urlLen);
            LogDebug(funcName, $"URL: {url}");
            
            var headers = ParseHeadersJson(headersJson, headersJsonLen);

            var resp = Task.Run(() => client.DeleteAsync(url, headers)).GetAwaiter().GetResult();
            sw.Stop();
            
            LogInfo(funcName, $"DELETE request completed in {sw.ElapsedMilliseconds}ms with status {resp.StatusCode}");
            
            return ToPtr(new NativeHttpResponse(resp));
        }
        catch (Exception ex)
        {
            sw.Stop();
            var errMsg = $"DELETE request failed after {sw.ElapsedMilliseconds}ms: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// 发送任意方法的 HTTP 请求。method 为 UTF-8 字符串（GET/POST/PUT/DELETE/PATCH/...）。
    /// body 为 UTF-8 字符串，可为 null/0。
    /// 返回响应句柄。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxHttp_Send")]
    public static unsafe IntPtr Send(IntPtr clientPtr, byte* methodUtf8, int methodLen, byte* urlUtf8, int urlLen,
                                     byte* bodyUtf8, int bodyLen, byte* headersJson, int headersJsonLen)
    {
        const string funcName = "DrxHttp_Send";
        LogTrace(funcName, $"Entering with client=0x{clientPtr:X}");
        var sw = Stopwatch.StartNew();
        try
        {
            var client = FromPtr<DrxHttpClient>(clientPtr);
            if (client == null) { LogWarning(funcName, "Invalid client handle"); SetLastError("Send: invalid client handle"); return IntPtr.Zero; }

            var methodStr = PtrToString(methodUtf8, methodLen).ToUpperInvariant();
            var url = PtrToString(urlUtf8, urlLen);
            LogDebug(funcName, $"Method: {methodStr}, URL: {url}, bodyLen={bodyLen}");
            
            var body = PtrToString(bodyUtf8, bodyLen);
            var headers = ParseHeadersJson(headersJson, headersJsonLen);

            var method = methodStr switch
            {
                "GET" => System.Net.Http.HttpMethod.Get,
                "POST" => System.Net.Http.HttpMethod.Post,
                "PUT" => System.Net.Http.HttpMethod.Put,
                "DELETE" => System.Net.Http.HttpMethod.Delete,
                "PATCH" => System.Net.Http.HttpMethod.Patch,
                "HEAD" => System.Net.Http.HttpMethod.Head,
                "OPTIONS" => System.Net.Http.HttpMethod.Options,
                _ => new System.Net.Http.HttpMethod(methodStr)
            };

            var resp = Task.Run(() => client.SendAsync(method, url, body, headers)).GetAwaiter().GetResult();
            sw.Stop();
            
            LogInfo(funcName, $"{methodStr} request completed in {sw.ElapsedMilliseconds}ms with status {resp.StatusCode}");
            
            return ToPtr(new NativeHttpResponse(resp));
        }
        catch (Exception ex)
        {
            sw.Stop();
            var errMsg = $"Send request failed after {sw.ElapsedMilliseconds}ms: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
            return IntPtr.Zero;
        }
    }

    // ========================================================================
    // 文件操作
    // ========================================================================

    /// <summary>
    /// 下载文件到本地路径。成功返回 0，失败返回 -1（可通过 DrxHttp_GetLastError 获取详情）。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxHttp_DownloadFile")]
    public static unsafe int DownloadFile(IntPtr clientPtr, byte* urlUtf8, int urlLen, byte* destPathUtf8, int destPathLen)
    {
        const string funcName = "DrxHttp_DownloadFile";
        LogTrace(funcName, $"Entering with client=0x{clientPtr:X}");
        var sw = Stopwatch.StartNew();
        try
        {
            var client = FromPtr<DrxHttpClient>(clientPtr);
            if (client == null) { LogWarning(funcName, "Invalid client handle"); SetLastError("DownloadFile: invalid client handle"); return -1; }

            var url = PtrToString(urlUtf8, urlLen);
            var destPath = PtrToString(destPathUtf8, destPathLen);
            LogDebug(funcName, $"URL: {url} -> {destPath}");

            Task.Run(() => client.DownloadFileAsync(url, destPath, progress: null, cancellationToken: default)).GetAwaiter().GetResult();
            sw.Stop();
            
            LogInfo(funcName, $"File downloaded successfully in {sw.ElapsedMilliseconds}ms from {url}");
            return 0;
        }
        catch (Exception ex)
        {
            sw.Stop();
            var errMsg = $"Download failed after {sw.ElapsedMilliseconds}ms: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
            return -1;
        }
    }

    /// <summary>
    /// 上传本地文件到指定 URL。返回响应句柄，失败返回 IntPtr.Zero。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxHttp_UploadFile")]
    public static unsafe IntPtr UploadFile(IntPtr clientPtr, byte* urlUtf8, int urlLen, byte* filePathUtf8, int filePathLen)
    {
        const string funcName = "DrxHttp_UploadFile";
        LogTrace(funcName, $"Entering with client=0x{clientPtr:X}");
        var sw = Stopwatch.StartNew();
        try
        {
            var client = FromPtr<DrxHttpClient>(clientPtr);
            if (client == null) { LogWarning(funcName, "Invalid client handle"); SetLastError("UploadFile: invalid client handle"); return IntPtr.Zero; }

            var url = PtrToString(urlUtf8, urlLen);
            var filePath = PtrToString(filePathUtf8, filePathLen);
            LogDebug(funcName, $"FilePath: {filePath} -> URL: {url}");

            var resp = Task.Run(() => client.UploadFileAsync(url, filePath, fieldName: "file", headers: null, query: null, progress: null, cancellationToken: default)).GetAwaiter().GetResult();
            sw.Stop();
            
            LogInfo(funcName, $"File uploaded successfully in {sw.ElapsedMilliseconds}ms to {url} with status {resp.StatusCode}");
            
            return ToPtr(new NativeHttpResponse(resp));
        }
        catch (Exception ex)
        {
            sw.Stop();
            var errMsg = $"Upload failed after {sw.ElapsedMilliseconds}ms: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
            return IntPtr.Zero;
        }
    }

    // ========================================================================
    // 响应读取
    // ========================================================================

    /// <summary>
    /// 获取响应状态码。无效句柄返回 -1。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxHttp_Response_GetStatusCode")]
    public static int ResponseGetStatusCode(IntPtr respPtr)
    {
        var resp = FromPtr<NativeHttpResponse>(respPtr);
        if (resp == null) { SetLastError("ResponseGetStatusCode: invalid response handle"); return -1; }
        return resp.StatusCode;
    }

    /// <summary>
    /// 获取响应体字节长度。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxHttp_Response_GetBodyLength")]
    public static int ResponseGetBodyLength(IntPtr respPtr)
    {
        var resp = FromPtr<NativeHttpResponse>(respPtr);
        if (resp == null) { SetLastError("ResponseGetBodyLength: invalid response handle"); return 0; }
        return resp.BodyBytes?.Length ?? 0;
    }

    /// <summary>
    /// 将响应体拷贝到调用方缓冲区。返回实际写入的字节数。
    /// 调用方应先调用 GetBodyLength 获取长度，分配足够缓冲区后调用此函数。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxHttp_Response_ReadBody")]
    public static int ResponseReadBody(IntPtr respPtr, IntPtr outBuffer, int outCapacity)
    {
        var resp = FromPtr<NativeHttpResponse>(respPtr);
        if (resp == null || outBuffer == IntPtr.Zero || outCapacity <= 0) return 0;
        if (resp.BodyBytes == null || resp.BodyBytes.Length == 0) return 0;

        var n = Math.Min(resp.BodyBytes.Length, outCapacity);
        Marshal.Copy(resp.BodyBytes, 0, outBuffer, n);
        return n;
    }

    /// <summary>
    /// 获取指定响应头的 UTF-8 字节长度（不含 null 终止符）。若不存在返回 0。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxHttp_Response_GetHeaderLength")]
    public static unsafe int ResponseGetHeaderLength(IntPtr respPtr, byte* nameUtf8, int nameLen)
    {
        var resp = FromPtr<NativeHttpResponse>(respPtr);
        if (resp == null || nameUtf8 == null || nameLen <= 0) return 0;
        var name = PtrToString(nameUtf8, nameLen);
        var value = resp.Headers?[name];
        if (value == null) return 0;
        return Encoding.UTF8.GetByteCount(value);
    }

    /// <summary>
    /// 读取指定响应头值到调用方缓冲区。返回实际写入的字节数。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxHttp_Response_ReadHeader")]
    public static unsafe int ResponseReadHeader(IntPtr respPtr, byte* nameUtf8, int nameLen, IntPtr outBuffer, int outCapacity)
    {
        var resp = FromPtr<NativeHttpResponse>(respPtr);
        if (resp == null || nameUtf8 == null || nameLen <= 0 || outBuffer == IntPtr.Zero || outCapacity <= 0) return 0;

        var name = PtrToString(nameUtf8, nameLen);
        var value = resp.Headers?[name];
        if (value == null) return 0;

        var bytes = Encoding.UTF8.GetBytes(value);
        var n = Math.Min(bytes.Length, outCapacity);
        Marshal.Copy(bytes, 0, outBuffer, n);
        return n;
    }

    /// <summary>
    /// 销毁响应句柄，释放内存。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxHttp_Response_Destroy")]
    public static void ResponseDestroy(IntPtr respPtr)
    {
        FreePtr(respPtr);
    }

    // ========================================================================
    // 内存与错误
    // ========================================================================

    /// <summary>
    /// 释放由导出函数通过 CoTaskMemAlloc 分配的非托管内存（如果有的话）。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxHttp_FreeBuffer")]
    public static void FreeBuffer(IntPtr buffer)
    {
        if (buffer != IntPtr.Zero)
            Marshal.FreeCoTaskMem(buffer);
    }

    /// <summary>
    /// 获取最后一次错误信息（当前线程）。写入调用方缓冲区，返回字节数。
    /// 无错误时返回 0。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxHttp_GetLastError")]
    public static int GetLastError(IntPtr outBuffer, int outCapacity)
    {
        if (s_lastError == null || outBuffer == IntPtr.Zero || outCapacity <= 0) return 0;
        var bytes = Encoding.UTF8.GetBytes(s_lastError);
        var n = Math.Min(bytes.Length, outCapacity);
        Marshal.Copy(bytes, 0, outBuffer, n);
        return n;
    }

    // ========================================================================
    // 导出表（函数指针表，供 GetProcAddress("GetDrxHttpClientExports") 一次性获取所有入口）
    // ========================================================================
    private static nint[] s_exportTable = Array.Empty<nint>();
    private static GCHandle s_pinnedHandle;
    private static IntPtr s_tablePtr = IntPtr.Zero;

    /// <summary>
    /// 获取导出函数指针表首地址。outCount 非 null 时写入表项数量。
    /// C++ 端通过此地址按固定索引偏移取得各函数指针。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "GetDrxHttpClientExports")]
    public static unsafe IntPtr GetExports(int* outCount)
    {
        if (s_tablePtr != IntPtr.Zero)
        {
            if (outCount != null) *outCount = s_exportTable.Length;
            return s_tablePtr;
        }

        s_exportTable = BuildExportTable();

        if (s_pinnedHandle.IsAllocated) s_pinnedHandle.Free();
        s_pinnedHandle = GCHandle.Alloc(s_exportTable, GCHandleType.Pinned);
        s_tablePtr = s_pinnedHandle.AddrOfPinnedObject();

        if (outCount != null) *outCount = s_exportTable.Length;
        return s_tablePtr;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static unsafe nint[] BuildExportTable()
    {
        // 客户端生命周期
        static delegate* unmanaged[Cdecl]<IntPtr>                                                   p_Create()              => &Create;
        static delegate* unmanaged[Cdecl]<byte*, int, IntPtr>                                       p_CreateWithBaseUrl()   => &CreateWithBaseUrl;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, byte*, int, void>                     p_SetDefaultHeader()    => &SetDefaultHeader;
        static delegate* unmanaged[Cdecl]<IntPtr, int, void>                                        p_SetTimeout()          => &SetTimeout;
        static delegate* unmanaged[Cdecl]<IntPtr, void>                                             p_Destroy()             => &Destroy;

        // 请求方法
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, byte*, int, IntPtr>                   p_Get()                 => &Get;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, byte*, int, byte*, int, IntPtr>       p_Post()                => &Post;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, byte*, int, byte*, int, IntPtr>       p_PostBytes()           => &PostBytes;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, byte*, int, byte*, int, IntPtr>       p_Put()                 => &Put;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, byte*, int, IntPtr>                   p_Delete()              => &Delete;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, byte*, int, byte*, int, byte*, int, IntPtr> p_Send()          => &Send;

        // 文件操作
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, byte*, int, int>                      p_DownloadFile()        => &DownloadFile;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, byte*, int, IntPtr>                   p_UploadFile()          => &UploadFile;

        // 响应读取
        static delegate* unmanaged[Cdecl]<IntPtr, int>                                              p_RespGetStatusCode()   => &ResponseGetStatusCode;
        static delegate* unmanaged[Cdecl]<IntPtr, int>                                              p_RespGetBodyLength()   => &ResponseGetBodyLength;
        static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, int>                                 p_RespReadBody()        => &ResponseReadBody;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, IntPtr, int, int>                     p_RespReadHeader()      => &ResponseReadHeader;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, int>                                  p_RespGetHeaderLen()    => &ResponseGetHeaderLength;
        static delegate* unmanaged[Cdecl]<IntPtr, void>                                             p_RespDestroy()         => &ResponseDestroy;

        // 内存与错误
        static delegate* unmanaged[Cdecl]<IntPtr, void>                                             p_FreeBuffer()          => &FreeBuffer;
        static delegate* unmanaged[Cdecl]<IntPtr, int, int>                                         p_GetLastError()        => &GetLastError;

        return new nint[]
        {
            (nint)p_Create(),               // [0]
            (nint)p_CreateWithBaseUrl(),     // [1]
            (nint)p_SetDefaultHeader(),     // [2]
            (nint)p_SetTimeout(),           // [3]
            (nint)p_Destroy(),              // [4]
            (nint)p_Get(),                  // [5]
            (nint)p_Post(),                 // [6]
            (nint)p_PostBytes(),            // [7]
            (nint)p_Put(),                  // [8]
            (nint)p_Delete(),               // [9]
            (nint)p_Send(),                 // [10]
            (nint)p_DownloadFile(),         // [11]
            (nint)p_UploadFile(),           // [12]
            (nint)p_RespGetStatusCode(),    // [13]
            (nint)p_RespGetBodyLength(),    // [14]
            (nint)p_RespReadBody(),         // [15]
            (nint)p_RespReadHeader(),       // [16]
            (nint)p_RespGetHeaderLen(),     // [17]
            (nint)p_RespDestroy(),          // [18]
            (nint)p_FreeBuffer(),           // [19]
            (nint)p_GetLastError(),         // [20]
        };
    }
}

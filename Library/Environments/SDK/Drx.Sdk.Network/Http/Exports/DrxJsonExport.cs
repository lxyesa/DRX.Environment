using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// DrxJson 原生导出函数，供 C++ 通过 LoadLibrary / GetProcAddress 调用。
/// <para>
/// 使用与服务端完全一致的 System.Text.Json，确保序列化结果可被服务端正确解析。
/// 所有字符串参数通过 UTF-8 byte* + 长度传入/传出。
/// JSON 对象/数组通过句柄管理，使用完毕需调用 DrxJson_Destroy 释放。
/// </para>
/// <para>
/// 导出表索引（供 GetDrxJsonExports 使用）：
///  [0]  DrxJson_CreateObject       - 创建 JSON 对象 {}
///  [1]  DrxJson_CreateArray        - 创建 JSON 数组 []
///  [2]  DrxJson_Parse              - 解析 JSON 字符串
///  [3]  DrxJson_Destroy            - 销毁句柄
///  [4]  DrxJson_SetString          - 设置字符串字段
///  [5]  DrxJson_SetInt             - 设置整数字段
///  [6]  DrxJson_SetDouble          - 设置浮点数字段
///  [7]  DrxJson_SetBool            - 设置布尔字段
///  [8]  DrxJson_SetNull            - 设置 null 字段
///  [9]  DrxJson_SetObject          - 设置子对象字段
///  [10] DrxJson_ArrayPushString    - 向数组追加字符串
///  [11] DrxJson_ArrayPushInt       - 向数组追加整数
///  [12] DrxJson_ArrayPushDouble    - 向数组追加浮点数
///  [13] DrxJson_ArrayPushBool      - 向数组追加布尔值
///  [14] DrxJson_ArrayPushObject    - 向数组追加子对象
///  [15] DrxJson_GetString          - 读取字符串字段（写入缓冲区，返回字节数）
///  [16] DrxJson_GetInt             - 读取整数字段
///  [17] DrxJson_GetDouble          - 读取浮点数字段
///  [18] DrxJson_GetBool            - 读取布尔字段
///  [19] DrxJson_HasKey             - 是否包含指定键（1=有，0=无）
///  [20] DrxJson_GetLength          - 获取序列化 UTF-8 字节长度
///  [21] DrxJson_Serialize          - 序列化到调用方缓冲区
///  [22] GetDrxJsonExports          - 获取导出表
/// </para>
/// </summary>
public static class DrxJsonExport
{
    // ========================================================================
    // 日志记录工具
    // ========================================================================
    private enum LogLevel { Trace, Debug, Info, Warning, Error }

    private static void LogMessage(LogLevel level, string functionName, string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var prefix = $"[{timestamp}][DrxJsonExport.{level.ToString().ToUpper()}]";
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
    // 句柄管理
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

    [ThreadStatic]
    private static string? s_lastError;

    private static void SetLastError(string msg)
    {
        s_lastError = msg;
        LogError("SetLastError", msg);
    }

    // ========================================================================
    // 创建与解析
    // ========================================================================

    /// <summary>创建空 JSON 对象 {}，返回句柄。</summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxJson_CreateObject")]
    public static IntPtr CreateObject()
    {
        const string funcName = "DrxJson_CreateObject";
        LogTrace(funcName, "Entering");
        try
        {
            var obj = new JsonObject();
            var ptr = ToPtr(obj);
            LogInfo(funcName, $"JSON object created, handle: 0x{ptr:X}");
            return ptr;
        }
        catch (Exception ex)
        {
            var errMsg = $"Failed: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
            return IntPtr.Zero;
        }
    }

    /// <summary>创建空 JSON 数组 []，返回句柄。</summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxJson_CreateArray")]
    public static IntPtr CreateArray()
    {
        const string funcName = "DrxJson_CreateArray";
        LogTrace(funcName, "Entering");
        try
        {
            var arr = new JsonArray();
            var ptr = ToPtr(arr);
            LogInfo(funcName, $"JSON array created, handle: 0x{ptr:X}");
            return ptr;
        }
        catch (Exception ex)
        {
            var errMsg = $"Failed: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// 解析 UTF-8 JSON 字符串，返回 JsonNode 句柄（对象/数组/值均可）。
    /// 失败返回 IntPtr.Zero，可通过 DrxJson_GetLastError 获取错误信息。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxJson_Parse")]
    public static unsafe IntPtr Parse(byte* jsonUtf8, int jsonLen)
    {
        const string funcName = "DrxJson_Parse";
        LogTrace(funcName, $"Entering with jsonLen={jsonLen}");
        try
        {
            var json = PtrToString(jsonUtf8, jsonLen);
            LogDebug(funcName, $"Parsing JSON: {(json.Length > 100 ? json.Substring(0, 100) + "..." : json)}");
            
            var node = JsonNode.Parse(json);
            if (node == null)
            {
                var errMsg = "Parse result is null";
                SetLastError(errMsg);
                LogWarning(funcName, errMsg);
                return IntPtr.Zero;
            }
            
            var ptr = ToPtr(node);
            LogInfo(funcName, $"JSON parsed successfully, handle: 0x{ptr:X}");
            return ptr;
        }
        catch (Exception ex)
        {
            var errMsg = $"Parse failed: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
            return IntPtr.Zero;
        }
    }

    /// <summary>销毁 JSON 句柄，释放托管引用。</summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxJson_Destroy")]
    public static void Destroy(IntPtr nodePtr)
    {
        const string funcName = "DrxJson_Destroy";
        LogTrace(funcName, $"Entering with handle: 0x{nodePtr:X}");
        try
        {
            FreePtr(nodePtr);
            LogInfo(funcName, $"Handle freed successfully");
        }
        catch (Exception ex)
        {
            var errMsg = $"Unexpected error: {ex.GetType().Name} - {ex.Message}";
            LogError(funcName, errMsg);
        }
    }

    // ========================================================================
    // 对象字段写入
    // ========================================================================

    /// <summary>在 JSON 对象上设置字符串字段。失败返回 0，成功返回 1。</summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxJson_SetString")]
    public static unsafe int SetString(IntPtr objPtr, byte* keyUtf8, int keyLen, byte* valueUtf8, int valueLen)
    {
        const string funcName = "DrxJson_SetString";
        try
        {
            var obj = FromPtr<JsonObject>(objPtr);
            if (obj == null) { LogWarning(funcName, "Invalid handle"); SetLastError("SetString: invalid handle"); return 0; }
            var key = PtrToString(keyUtf8, keyLen);
            var value = PtrToString(valueUtf8, valueLen);
            LogDebug(funcName, $"Setting {key} = {(value.Length > 50 ? value.Substring(0, 50) + "..." : value)}");
            obj[key] = value;
            LogTrace(funcName, "Set successfully");
            return 1;
        }
        catch (Exception ex)
        {
            var errMsg = $"Failed: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
            return 0;
        }
    }

    /// <summary>在 JSON 对象上设置 64 位整数字段。</summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxJson_SetInt")]
    public static unsafe int SetInt(IntPtr objPtr, byte* keyUtf8, int keyLen, long value)
    {
        const string funcName = "DrxJson_SetInt";
        try
        {
            var obj = FromPtr<JsonObject>(objPtr);
            if (obj == null) { LogWarning(funcName, "Invalid handle"); SetLastError("SetInt: invalid handle"); return 0; }
            var key = PtrToString(keyUtf8, keyLen);
            LogDebug(funcName, $"Setting {key} = {value}");
            obj[key] = value;
            LogTrace(funcName, "Set successfully");
            return 1;
        }
        catch (Exception ex)
        {
            var errMsg = $"Failed: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
            return 0;
        }
    }

    /// <summary>在 JSON 对象上设置双精度浮点字段。</summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxJson_SetDouble")]
    public static unsafe int SetDouble(IntPtr objPtr, byte* keyUtf8, int keyLen, double value)
    {
        const string funcName = "DrxJson_SetDouble";
        try
        {
            var obj = FromPtr<JsonObject>(objPtr);
            if (obj == null) { LogWarning(funcName, "Invalid handle"); SetLastError("SetDouble: invalid handle"); return 0; }
            var key = PtrToString(keyUtf8, keyLen);
            LogDebug(funcName, $"Setting {key} = {value}");
            obj[key] = value;
            LogTrace(funcName, "Set successfully");
            return 1;
        }
        catch (Exception ex)
        {
            var errMsg = $"Failed: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
            return 0;
        }
    }

    /// <summary>在 JSON 对象上设置布尔字段。value 非 0 为 true。</summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxJson_SetBool")]
    public static unsafe int SetBool(IntPtr objPtr, byte* keyUtf8, int keyLen, int value)
    {
        const string funcName = "DrxJson_SetBool";
        try
        {
            var obj = FromPtr<JsonObject>(objPtr);
            if (obj == null) { LogWarning(funcName, "Invalid handle"); SetLastError("SetBool: invalid handle"); return 0; }
            var key = PtrToString(keyUtf8, keyLen);
            var boolValue = value != 0;
            LogDebug(funcName, $"Setting {key} = {boolValue}");
            obj[key] = boolValue;
            LogTrace(funcName, "Set successfully");
            return 1;
        }
        catch (Exception ex)
        {
            var errMsg = $"Failed: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
            return 0;
        }
    }

    /// <summary>在 JSON 对象上设置 null 字段。</summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxJson_SetNull")]
    public static unsafe int SetNull(IntPtr objPtr, byte* keyUtf8, int keyLen)
    {
        const string funcName = "DrxJson_SetNull";
        try
        {
            var obj = FromPtr<JsonObject>(objPtr);
            if (obj == null) { LogWarning(funcName, "Invalid handle"); SetLastError("SetNull: invalid handle"); return 0; }
            var key = PtrToString(keyUtf8, keyLen);
            LogDebug(funcName, $"Setting {key} = null");
            obj[key] = null;
            LogTrace(funcName, "Set successfully");
            return 1;
        }
        catch (Exception ex)
        {
            var errMsg = $"Failed: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
            return 0;
        }
    }

    /// <summary>
    /// 在 JSON 对象上嵌套另一个 JSON 节点（对象或数组）。
    /// 注意：childPtr 的 GCHandle 将被释放（所有权转移给父对象的深拷贝）。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxJson_SetObject")]
    public static unsafe int SetObject(IntPtr objPtr, byte* keyUtf8, int keyLen, IntPtr childPtr)
    {
        const string funcName = "DrxJson_SetObject";
        try
        {
            var obj = FromPtr<JsonObject>(objPtr);
            var child = FromPtr<JsonNode>(childPtr);
            if (obj == null) { LogWarning(funcName, "Invalid parent handle"); SetLastError("SetObject: invalid parent handle"); return 0; }
            if (child == null) { LogWarning(funcName, "Invalid child handle"); SetLastError("SetObject: invalid child handle"); return 0; }
            
            var key = PtrToString(keyUtf8, keyLen);
            LogDebug(funcName, $"Setting nested object at key: {key}");
            
            // 深拷贝以避免 JsonNode 不能同时属于两个父节点的限制
            var copied = JsonNode.Parse(child.ToJsonString());
            obj[key] = copied;
            FreePtr(childPtr);
            LogInfo(funcName, $"Object set and child handle freed");
            return 1;
        }
        catch (Exception ex)
        {
            var errMsg = $"Failed: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
            return 0;
        }
    }

    // ========================================================================
    // 数组元素追加
    // ========================================================================

    /// <summary>向 JSON 数组追加字符串。</summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxJson_ArrayPushString")]
    public static unsafe int ArrayPushString(IntPtr arrPtr, byte* valueUtf8, int valueLen)
    {
        const string funcName = "DrxJson_ArrayPushString";
        try
        {
            var arr = FromPtr<JsonArray>(arrPtr);
            if (arr == null) { LogWarning(funcName, "Invalid handle"); SetLastError("ArrayPushString: invalid handle"); return 0; }
            var value = PtrToString(valueUtf8, valueLen);
            LogDebug(funcName, $"Pushing string (length={valueLen})");
            arr.Add(value);
            LogTrace(funcName, $"String pushed, array size now {arr.Count}");
            return 1;
        }
        catch (Exception ex)
        {
            var errMsg = $"Failed: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
            return 0;
        }
    }

    /// <summary>向 JSON 数组追加 64 位整数。</summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxJson_ArrayPushInt")]
    public static int ArrayPushInt(IntPtr arrPtr, long value)
    {
        const string funcName = "DrxJson_ArrayPushInt";
        try
        {
            var arr = FromPtr<JsonArray>(arrPtr);
            if (arr == null) { LogWarning(funcName, "Invalid handle"); SetLastError("ArrayPushInt: invalid handle"); return 0; }
            LogDebug(funcName, $"Pushing int: {value}");
            arr.Add(value);
            LogTrace(funcName, $"Int pushed, array size now {arr.Count}");
            return 1;
        }
        catch (Exception ex)
        {
            var errMsg = $"Failed: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
            return 0;
        }
    }

    /// <summary>向 JSON 数组追加双精度浮点数。</summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxJson_ArrayPushDouble")]
    public static int ArrayPushDouble(IntPtr arrPtr, double value)
    {
        const string funcName = "DrxJson_ArrayPushDouble";
        try
        {
            var arr = FromPtr<JsonArray>(arrPtr);
            if (arr == null) { LogWarning(funcName, "Invalid handle"); SetLastError("ArrayPushDouble: invalid handle"); return 0; }
            LogDebug(funcName, $"Pushing double: {value}");
            arr.Add(value);
            LogTrace(funcName, $"Double pushed, array size now {arr.Count}");
            return 1;
        }
        catch (Exception ex)
        {
            var errMsg = $"Failed: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
            return 0;
        }
    }

    /// <summary>向 JSON 数组追加布尔值。value 非 0 为 true。</summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxJson_ArrayPushBool")]
    public static int ArrayPushBool(IntPtr arrPtr, int value)
    {
        const string funcName = "DrxJson_ArrayPushBool";
        try
        {
            var arr = FromPtr<JsonArray>(arrPtr);
            if (arr == null) { LogWarning(funcName, "Invalid handle"); SetLastError("ArrayPushBool: invalid handle"); return 0; }
            var boolValue = value != 0;
            LogDebug(funcName, $"Pushing bool: {boolValue}");
            arr.Add(boolValue);
            LogTrace(funcName, $"Bool pushed, array size now {arr.Count}");
            return 1;
        }
        catch (Exception ex)
        {
            var errMsg = $"Failed: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
            return 0;
        }
    }

    /// <summary>
    /// 向 JSON 数组追加子 JSON 节点（对象或数组）。childPtr 所有权转移，句柄自动释放。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxJson_ArrayPushObject")]
    public static int ArrayPushObject(IntPtr arrPtr, IntPtr childPtr)
    {
        const string funcName = "DrxJson_ArrayPushObject";
        try
        {
            var arr = FromPtr<JsonArray>(arrPtr);
            var child = FromPtr<JsonNode>(childPtr);
            if (arr == null) { LogWarning(funcName, "Invalid array handle"); SetLastError("ArrayPushObject: invalid array handle"); return 0; }
            if (child == null) { LogWarning(funcName, "Invalid child handle"); SetLastError("ArrayPushObject: invalid child handle"); return 0; }
            
            LogDebug(funcName, "Pushing nested object/array");
            var copied = JsonNode.Parse(child.ToJsonString());
            arr.Add(copied);
            FreePtr(childPtr);
            LogInfo(funcName, $"Object pushed and child handle freed, array size now {arr.Count}");
            return 1;
        }
        catch (Exception ex)
        {
            var errMsg = $"Failed: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
            return 0;
        }
    }

    // ========================================================================
    // 字段读取（用于解析响应体）
    // ========================================================================

    /// <summary>
    /// 读取 JSON 对象的字符串字段，写入调用方缓冲区。
    /// 返回实际写入字节数；字段不存在或类型不符返回 -1。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxJson_GetString")]
    public static unsafe int GetString(IntPtr nodePtr, byte* keyUtf8, int keyLen, IntPtr outBuffer, int outCapacity)
    {
        const string funcName = "DrxJson_GetString";
        try
        {
            var node = FromPtr<JsonNode>(nodePtr);
            if (node == null) { LogWarning(funcName, "Invalid handle"); SetLastError("GetString: invalid handle"); return -1; }
            var key = PtrToString(keyUtf8, keyLen);
            LogDebug(funcName, $"Reading field: {key} (outCapacity={outCapacity})");
            
            var value = node[key]?.GetValue<string>();
            if (value == null) { LogDebug(funcName, $"Field is null or not found"); return -1; }
            
            var bytes = Encoding.UTF8.GetBytes(value);
            var n = Math.Min(bytes.Length, outCapacity);
            if (outBuffer != IntPtr.Zero && n > 0)
                Marshal.Copy(bytes, 0, outBuffer, n);
            LogTrace(funcName, $"Read {n} bytes");
            return n;
        }
        catch (Exception ex)
        {
            var errMsg = $"Failed: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
            return -1;
        }
    }

    /// <summary>读取 JSON 对象的整数字段。字段不存在或类型不符返回 long.MinValue。</summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxJson_GetInt")]
    public static unsafe long GetInt(IntPtr nodePtr, byte* keyUtf8, int keyLen)
    {
        const string funcName = "DrxJson_GetInt";
        try
        {
            var node = FromPtr<JsonNode>(nodePtr);
            if (node == null) { LogWarning(funcName, "Invalid handle"); SetLastError("GetInt: invalid handle"); return long.MinValue; }
            var key = PtrToString(keyUtf8, keyLen);
            LogDebug(funcName, $"Reading field: {key}");
            var result = node[key]?.GetValue<long>() ?? long.MinValue;
            LogTrace(funcName, $"Read value: {result}");
            return result;
        }
        catch (Exception ex)
        {
            var errMsg = $"Failed: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
            return long.MinValue;
        }
    }

    /// <summary>读取 JSON 对象的浮点字段。字段不存在或类型不符返回 double.NaN。</summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxJson_GetDouble")]
    public static unsafe double GetDouble(IntPtr nodePtr, byte* keyUtf8, int keyLen)
    {
        const string funcName = "DrxJson_GetDouble";
        try
        {
            var node = FromPtr<JsonNode>(nodePtr);
            if (node == null) { LogWarning(funcName, "Invalid handle"); SetLastError("GetDouble: invalid handle"); return double.NaN; }
            var key = PtrToString(keyUtf8, keyLen);
            LogDebug(funcName, $"Reading field: {key}");
            var result = node[key]?.GetValue<double>() ?? double.NaN;
            LogTrace(funcName, $"Read value: {result}");
            return result;
        }
        catch (Exception ex)
        {
            var errMsg = $"Failed: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
            return double.NaN;
        }
    }

    /// <summary>读取 JSON 对象的布尔字段。字段不存在返回 -1，true 返回 1，false 返回 0。</summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxJson_GetBool")]
    public static unsafe int GetBool(IntPtr nodePtr, byte* keyUtf8, int keyLen)
    {
        const string funcName = "DrxJson_GetBool";
        try
        {
            var node = FromPtr<JsonNode>(nodePtr);
            if (node == null) { LogWarning(funcName, "Invalid handle"); SetLastError("GetBool: invalid handle"); return -1; }
            var key = PtrToString(keyUtf8, keyLen);
            LogDebug(funcName, $"Reading field: {key}");
            var val = node[key];
            if (val == null) { LogDebug(funcName, "Field is null"); return -1; }
            var result = val.GetValue<bool>() ? 1 : 0;
            LogTrace(funcName, $"Read value: {(result == 1 ? "true" : "false")}");
            return result;
        }
        catch (Exception ex)
        {
            var errMsg = $"Failed: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
            return -1;
        }
    }

    /// <summary>检查 JSON 对象是否包含指定键。包含返回 1，不包含返回 0。</summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxJson_HasKey")]
    public static unsafe int HasKey(IntPtr nodePtr, byte* keyUtf8, int keyLen)
    {
        const string funcName = "DrxJson_HasKey";
        try
        {
            var node = FromPtr<JsonNode>(nodePtr) as JsonObject;
            if (node == null) { LogWarning(funcName, "Invalid handle or not an object"); SetLastError("HasKey: invalid handle or not an object"); return 0; }
            var key = PtrToString(keyUtf8, keyLen);
            var hasKey = node.ContainsKey(key) ? 1 : 0;
            LogDebug(funcName, $"Key '{key}' exists: {(hasKey == 1 ? "yes" : "no")}");
            return hasKey;
        }
        catch (Exception ex)
        {
            var errMsg = $"Failed: {ex.GetType().Name} - {ex.Message}";
            SetLastError(errMsg);
            LogError(funcName, errMsg);
            return 0;
        }
    }

    // ========================================================================
    // 序列化
    // ========================================================================

    /// <summary>
    /// 获取 JSON 节点序列化为 UTF-8 字符串后的字节长度（不含 null 终止符）。
    /// C++ 端先调用此函数获取长度，分配缓冲区，再调用 DrxJson_Serialize。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxJson_GetLength")]
    public static int GetLength(IntPtr nodePtr)
    {
        try
        {
            var node = FromPtr<JsonNode>(nodePtr);
            if (node == null) { SetLastError("GetLength: invalid handle"); return 0; }
            return Encoding.UTF8.GetByteCount(node.ToJsonString());
        }
        catch (Exception ex) { SetLastError($"GetLength failed: {ex.Message}"); return 0; }
    }

    /// <summary>
    /// 将 JSON 节点序列化为 UTF-8 字节串，写入调用方缓冲区。
    /// 返回实际写入字节数。建议先调用 DrxJson_GetLength 获取所需长度。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxJson_Serialize")]
    public static int Serialize(IntPtr nodePtr, IntPtr outBuffer, int outCapacity)
    {
        try
        {
            var node = FromPtr<JsonNode>(nodePtr);
            if (node == null || outBuffer == IntPtr.Zero || outCapacity <= 0)
            {
                SetLastError("Serialize: invalid arguments");
                return 0;
            }
            var bytes = Encoding.UTF8.GetBytes(node.ToJsonString());
            var n = Math.Min(bytes.Length, outCapacity);
            Marshal.Copy(bytes, 0, outBuffer, n);
            return n;
        }
        catch (Exception ex) { SetLastError($"Serialize failed: {ex.Message}"); return 0; }
    }

    /// <summary>
    /// 获取最后一次错误信息。写入调用方缓冲区，返回字节数。无错误返回 0。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "DrxJson_GetLastError")]
    public static int GetLastError(IntPtr outBuffer, int outCapacity)
    {
        if (s_lastError == null || outBuffer == IntPtr.Zero || outCapacity <= 0) return 0;
        var bytes = Encoding.UTF8.GetBytes(s_lastError);
        var n = Math.Min(bytes.Length, outCapacity);
        Marshal.Copy(bytes, 0, outBuffer, n);
        return n;
    }

    // ========================================================================
    // 导出表
    // ========================================================================
    private static nint[] s_exportTable = Array.Empty<nint>();
    private static GCHandle s_pinnedHandle;
    private static IntPtr s_tablePtr = IntPtr.Zero;

    /// <summary>
    /// 获取导出函数指针表首地址，outCount 非 null 时写入表项数量。
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) }, EntryPoint = "GetDrxJsonExports")]
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
        static delegate* unmanaged[Cdecl]<IntPtr>                                           p_CreateObject()        => &CreateObject;
        static delegate* unmanaged[Cdecl]<IntPtr>                                           p_CreateArray()         => &CreateArray;
        static delegate* unmanaged[Cdecl]<byte*, int, IntPtr>                               p_Parse()               => &Parse;
        static delegate* unmanaged[Cdecl]<IntPtr, void>                                     p_Destroy()             => &Destroy;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, byte*, int, int>              p_SetString()           => &SetString;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, long, int>                    p_SetInt()              => &SetInt;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, double, int>                  p_SetDouble()           => &SetDouble;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, int, int>                     p_SetBool()             => &SetBool;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, int>                          p_SetNull()             => &SetNull;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, IntPtr, int>                  p_SetObject()           => &SetObject;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, int>                          p_ArrayPushString()     => &ArrayPushString;
        static delegate* unmanaged[Cdecl]<IntPtr, long, int>                                p_ArrayPushInt()        => &ArrayPushInt;
        static delegate* unmanaged[Cdecl]<IntPtr, double, int>                              p_ArrayPushDouble()     => &ArrayPushDouble;
        static delegate* unmanaged[Cdecl]<IntPtr, int, int>                                 p_ArrayPushBool()       => &ArrayPushBool;
        static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int>                              p_ArrayPushObject()     => &ArrayPushObject;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, IntPtr, int, int>             p_GetString()           => &GetString;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, long>                         p_GetInt()              => &GetInt;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, double>                       p_GetDouble()           => &GetDouble;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, int>                          p_GetBool()             => &GetBool;
        static delegate* unmanaged[Cdecl]<IntPtr, byte*, int, int>                          p_HasKey()              => &HasKey;
        static delegate* unmanaged[Cdecl]<IntPtr, int>                                      p_GetLength()           => &GetLength;
        static delegate* unmanaged[Cdecl]<IntPtr, IntPtr, int, int>                         p_Serialize()           => &Serialize;

        return new nint[]
        {
            (nint)p_CreateObject(),         // [0]
            (nint)p_CreateArray(),          // [1]
            (nint)p_Parse(),                // [2]
            (nint)p_Destroy(),              // [3]
            (nint)p_SetString(),            // [4]
            (nint)p_SetInt(),               // [5]
            (nint)p_SetDouble(),            // [6]
            (nint)p_SetBool(),              // [7]
            (nint)p_SetNull(),              // [8]
            (nint)p_SetObject(),            // [9]
            (nint)p_ArrayPushString(),      // [10]
            (nint)p_ArrayPushInt(),         // [11]
            (nint)p_ArrayPushDouble(),      // [12]
            (nint)p_ArrayPushBool(),        // [13]
            (nint)p_ArrayPushObject(),      // [14]
            (nint)p_GetString(),            // [15]
            (nint)p_GetInt(),               // [16]
            (nint)p_GetDouble(),            // [17]
            (nint)p_GetBool(),              // [18]
            (nint)p_HasKey(),               // [19]
            (nint)p_GetLength(),            // [20]
            (nint)p_Serialize(),            // [21]
        };
    }
}

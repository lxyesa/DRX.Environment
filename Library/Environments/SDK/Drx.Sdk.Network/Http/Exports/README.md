# DrxHttpClientExport 导出说明

DrxHttpClient 的原生导出层，使 C++ 应用程序可通过 `LoadLibrary` / `GetProcAddress` 调用 .NET HTTP 客户端功能。

## 编译方式

本文件包含在 `Drx.Sdk.Network` 项目中，使用 **NativeAOT** 发布为原生 DLL：

```powershell
dotnet publish -r win-x64 -c Release
```

输出 DLL 位于 `bin/Release/net9.0-windows/win-x64/publish/Drx.Sdk.Network.dll`。

---

## 导出函数一览

| 索引 | 入口名称 | 签名 | 说明 |
|------|--------|------|------|
| 0 | `DrxHttp_Create` | `() → HANDLE` | 创建 HTTP 客户端 |
| 1 | `DrxHttp_CreateWithBaseUrl` | `(url, urlLen) → HANDLE` | 创建带基地址的客户端 |
| 2 | `DrxHttp_SetDefaultHeader` | `(client, name, nameLen, value, valueLen) → void` | 设置默认请求头 |
| 3 | `DrxHttp_SetTimeout` | `(client, timeoutMs) → void` | 设置超时（毫秒） |
| 4 | `DrxHttp_Destroy` | `(client) → void` | 销毁客户端 |
| 5 | `DrxHttp_Get` | `(client, url, urlLen, headersJson, headersJsonLen) → RESP` | GET 请求 |
| 6 | `DrxHttp_Post` | `(client, url, urlLen, body, bodyLen, headersJson, headersJsonLen) → RESP` | POST（字符串体） |
| 7 | `DrxHttp_PostBytes` | `(client, url, urlLen, body, bodyLen, headersJson, headersJsonLen) → RESP` | POST（字节体） |
| 8 | `DrxHttp_Put` | `(client, url, urlLen, body, bodyLen, headersJson, headersJsonLen) → RESP` | PUT 请求 |
| 9 | `DrxHttp_Delete` | `(client, url, urlLen, headersJson, headersJsonLen) → RESP` | DELETE 请求 |
| 10 | `DrxHttp_Send` | `(client, method, methodLen, url, urlLen, body, bodyLen, headersJson, headersJsonLen) → RESP` | 任意方法请求 |
| 11 | `DrxHttp_DownloadFile` | `(client, url, urlLen, destPath, destPathLen) → int` | 下载文件（0=成功） |
| 12 | `DrxHttp_UploadFile` | `(client, url, urlLen, filePath, filePathLen) → RESP` | 上传文件 |
| 13 | `DrxHttp_Response_GetStatusCode` | `(resp) → int` | 获取响应状态码 |
| 14 | `DrxHttp_Response_GetBodyLength` | `(resp) → int` | 获取响应体字节长度 |
| 15 | `DrxHttp_Response_ReadBody` | `(resp, outBuffer, outCapacity) → int` | 读取响应体 |
| 16 | `DrxHttp_Response_ReadHeader` | `(resp, name, nameLen, outBuffer, outCapacity) → int` | 读取响应头 |
| 17 | `DrxHttp_Response_GetHeaderLength` | `(resp, name, nameLen) → int` | 获取响应头值长度 |
| 18 | `DrxHttp_Response_Destroy` | `(resp) → void` | 销毁响应 |
| 19 | `DrxHttp_FreeBuffer` | `(buffer) → void` | 释放 CoTaskMem 缓冲区 |
| 20 | `DrxHttp_GetLastError` | `(outBuffer, outCapacity) → int` | 获取最后错误信息 |

> `HANDLE` = `void*`（句柄），`RESP` = 响应句柄（也是 `void*`），失败返回 `nullptr`。

---

## C++ 头文件

```cpp
// DrxHttpClientExports.hpp
#pragma once
#include <windows.h>
#include <cstdint>
#include <string>
#include <vector>

// ============================================================================
// 方式 A：逐一 GetProcAddress 绑定
// ============================================================================

// 客户端生命周期
typedef void*   (__cdecl* fn_DrxHttp_Create)();
typedef void*   (__cdecl* fn_DrxHttp_CreateWithBaseUrl)(const uint8_t* url, int urlLen);
typedef void    (__cdecl* fn_DrxHttp_SetDefaultHeader)(void* client, const uint8_t* name, int nameLen, const uint8_t* value, int valueLen);
typedef void    (__cdecl* fn_DrxHttp_SetTimeout)(void* client, int timeoutMs);
typedef void    (__cdecl* fn_DrxHttp_Destroy)(void* client);

// 请求
typedef void*   (__cdecl* fn_DrxHttp_Get)(void* client, const uint8_t* url, int urlLen, const uint8_t* headersJson, int headersJsonLen);
typedef void*   (__cdecl* fn_DrxHttp_Post)(void* client, const uint8_t* url, int urlLen, const uint8_t* body, int bodyLen, const uint8_t* headersJson, int headersJsonLen);
typedef void*   (__cdecl* fn_DrxHttp_PostBytes)(void* client, const uint8_t* url, int urlLen, const uint8_t* body, int bodyLen, const uint8_t* headersJson, int headersJsonLen);
typedef void*   (__cdecl* fn_DrxHttp_Put)(void* client, const uint8_t* url, int urlLen, const uint8_t* body, int bodyLen, const uint8_t* headersJson, int headersJsonLen);
typedef void*   (__cdecl* fn_DrxHttp_Delete)(void* client, const uint8_t* url, int urlLen, const uint8_t* headersJson, int headersJsonLen);
typedef void*   (__cdecl* fn_DrxHttp_Send)(void* client, const uint8_t* method, int methodLen, const uint8_t* url, int urlLen, const uint8_t* body, int bodyLen, const uint8_t* headersJson, int headersJsonLen);

// 文件操作
typedef int     (__cdecl* fn_DrxHttp_DownloadFile)(void* client, const uint8_t* url, int urlLen, const uint8_t* destPath, int destPathLen);
typedef void*   (__cdecl* fn_DrxHttp_UploadFile)(void* client, const uint8_t* url, int urlLen, const uint8_t* filePath, int filePathLen);

// 响应读取
typedef int     (__cdecl* fn_DrxHttp_Response_GetStatusCode)(void* resp);
typedef int     (__cdecl* fn_DrxHttp_Response_GetBodyLength)(void* resp);
typedef int     (__cdecl* fn_DrxHttp_Response_ReadBody)(void* resp, void* outBuffer, int outCapacity);
typedef int     (__cdecl* fn_DrxHttp_Response_ReadHeader)(void* resp, const uint8_t* name, int nameLen, void* outBuffer, int outCapacity);
typedef int     (__cdecl* fn_DrxHttp_Response_GetHeaderLength)(void* resp, const uint8_t* name, int nameLen);
typedef void    (__cdecl* fn_DrxHttp_Response_Destroy)(void* resp);

// 工具
typedef void    (__cdecl* fn_DrxHttp_FreeBuffer)(void* buffer);
typedef int     (__cdecl* fn_DrxHttp_GetLastError)(void* outBuffer, int outCapacity);

// 导出表
typedef void*   (__cdecl* fn_GetDrxHttpClientExports)(int* outCount);


// ============================================================================
// 方式 B：通过导出表批量绑定（推荐）
// ============================================================================
struct DrxHttpClientExports
{
    fn_DrxHttp_Create                   Create;                 // [0]
    fn_DrxHttp_CreateWithBaseUrl        CreateWithBaseUrl;      // [1]
    fn_DrxHttp_SetDefaultHeader         SetDefaultHeader;       // [2]
    fn_DrxHttp_SetTimeout               SetTimeout;             // [3]
    fn_DrxHttp_Destroy                  Destroy;                // [4]
    fn_DrxHttp_Get                      Get;                    // [5]
    fn_DrxHttp_Post                     Post;                   // [6]
    fn_DrxHttp_PostBytes                PostBytes;              // [7]
    fn_DrxHttp_Put                      Put;                    // [8]
    fn_DrxHttp_Delete                   Delete;                 // [9]
    fn_DrxHttp_Send                     Send;                   // [10]
    fn_DrxHttp_DownloadFile             DownloadFile;           // [11]
    fn_DrxHttp_UploadFile               UploadFile;             // [12]
    fn_DrxHttp_Response_GetStatusCode   Response_GetStatusCode; // [13]
    fn_DrxHttp_Response_GetBodyLength   Response_GetBodyLength; // [14]
    fn_DrxHttp_Response_ReadBody        Response_ReadBody;      // [15]
    fn_DrxHttp_Response_ReadHeader      Response_ReadHeader;    // [16]
    fn_DrxHttp_Response_GetHeaderLength Response_GetHeaderLength;// [17]
    fn_DrxHttp_Response_Destroy         Response_Destroy;       // [18]
    fn_DrxHttp_FreeBuffer               FreeBuffer;             // [19]
    fn_DrxHttp_GetLastError             GetLastError;           // [20]
};

inline bool LoadDrxHttpClientExports(HMODULE hModule, DrxHttpClientExports& out)
{
    auto pfn = (fn_GetDrxHttpClientExports)::GetProcAddress(hModule, "GetDrxHttpClientExports");
    if (!pfn) return false;

    int count = 0;
    void** table = (void**)pfn(&count);
    if (!table || count < 21) return false;

    out.Create                  = (fn_DrxHttp_Create)                table[0];
    out.CreateWithBaseUrl       = (fn_DrxHttp_CreateWithBaseUrl)     table[1];
    out.SetDefaultHeader        = (fn_DrxHttp_SetDefaultHeader)     table[2];
    out.SetTimeout              = (fn_DrxHttp_SetTimeout)            table[3];
    out.Destroy                 = (fn_DrxHttp_Destroy)               table[4];
    out.Get                     = (fn_DrxHttp_Get)                   table[5];
    out.Post                    = (fn_DrxHttp_Post)                  table[6];
    out.PostBytes               = (fn_DrxHttp_PostBytes)             table[7];
    out.Put                     = (fn_DrxHttp_Put)                   table[8];
    out.Delete                  = (fn_DrxHttp_Delete)                table[9];
    out.Send                    = (fn_DrxHttp_Send)                  table[10];
    out.DownloadFile            = (fn_DrxHttp_DownloadFile)          table[11];
    out.UploadFile              = (fn_DrxHttp_UploadFile)            table[12];
    out.Response_GetStatusCode  = (fn_DrxHttp_Response_GetStatusCode)table[13];
    out.Response_GetBodyLength  = (fn_DrxHttp_Response_GetBodyLength)table[14];
    out.Response_ReadBody       = (fn_DrxHttp_Response_ReadBody)     table[15];
    out.Response_ReadHeader     = (fn_DrxHttp_Response_ReadHeader)   table[16];
    out.Response_GetHeaderLength= (fn_DrxHttp_Response_GetHeaderLength)table[17];
    out.Response_Destroy        = (fn_DrxHttp_Response_Destroy)      table[18];
    out.FreeBuffer              = (fn_DrxHttp_FreeBuffer)            table[19];
    out.GetLastError            = (fn_DrxHttp_GetLastError)          table[20];
    return true;
}
```

---

## C++ 使用示例

### 方式 A：逐一 GetProcAddress

```cpp
#include <windows.h>
#include <cstdio>
#include <cstdint>
#include <string>
#include <vector>
#include "DrxHttpClientExports.hpp"

int main()
{
    HMODULE hDll = LoadLibraryA("Drx.Sdk.Network.dll");
    if (!hDll) { printf("LoadLibrary failed\n"); return 1; }

    auto Create  = (fn_DrxHttp_Create)GetProcAddress(hDll, "DrxHttp_Create");
    auto Get     = (fn_DrxHttp_Get)GetProcAddress(hDll, "DrxHttp_Get");
    auto GetCode = (fn_DrxHttp_Response_GetStatusCode)GetProcAddress(hDll, "DrxHttp_Response_GetStatusCode");
    auto GetLen  = (fn_DrxHttp_Response_GetBodyLength)GetProcAddress(hDll, "DrxHttp_Response_GetBodyLength");
    auto ReadBody= (fn_DrxHttp_Response_ReadBody)GetProcAddress(hDll, "DrxHttp_Response_ReadBody");
    auto RespDel = (fn_DrxHttp_Response_Destroy)GetProcAddress(hDll, "DrxHttp_Response_Destroy");
    auto Destroy = (fn_DrxHttp_Destroy)GetProcAddress(hDll, "DrxHttp_Destroy");

    // 创建客户端
    void* client = Create();

    // GET 请求
    std::string url = "https://httpbin.org/get";
    void* resp = Get(client,
                     (const uint8_t*)url.c_str(), (int)url.size(),
                     nullptr, 0);

    if (resp) {
        int status = GetCode(resp);
        int bodyLen = GetLen(resp);
        printf("Status: %d, Body length: %d\n", status, bodyLen);

        std::vector<uint8_t> body(bodyLen);
        ReadBody(resp, body.data(), bodyLen);
        printf("Body: %.*s\n", bodyLen, (const char*)body.data());

        RespDel(resp);
    }

    Destroy(client);
    FreeLibrary(hDll);
    return 0;
}
```

### 方式 B：导出表批量绑定（推荐）

```cpp
#include <windows.h>
#include <cstdio>
#include <string>
#include <vector>
#include "DrxHttpClientExports.hpp"

int main()
{
    HMODULE hDll = LoadLibraryA("Drx.Sdk.Network.dll");
    if (!hDll) { printf("LoadLibrary failed\n"); return 1; }

    DrxHttpClientExports http{};
    if (!LoadDrxHttpClientExports(hDll, http)) {
        printf("Failed to load export table\n");
        FreeLibrary(hDll);
        return 1;
    }

    // 创建客户端
    void* client = http.Create();

    // 设置超时 10 秒
    http.SetTimeout(client, 10000);

    // 设置默认头
    std::string headerName = "User-Agent";
    std::string headerValue = "DrxNativeClient/1.0";
    http.SetDefaultHeader(client,
                          (const uint8_t*)headerName.c_str(), (int)headerName.size(),
                          (const uint8_t*)headerValue.c_str(), (int)headerValue.size());

    // GET 请求
    std::string url = "https://httpbin.org/get";
    void* resp = http.Get(client,
                          (const uint8_t*)url.c_str(), (int)url.size(),
                          nullptr, 0);

    if (resp) {
        int status = http.Response_GetStatusCode(resp);
        int bodyLen = http.Response_GetBodyLength(resp);

        std::vector<uint8_t> body(bodyLen);
        http.Response_ReadBody(resp, body.data(), bodyLen);

        printf("GET %s => %d\n%.*s\n", url.c_str(), status, bodyLen, (const char*)body.data());
        http.Response_Destroy(resp);
    }

    // POST 请求（JSON 体）
    std::string postUrl = "https://httpbin.org/post";
    std::string jsonBody = R"({"name":"test","value":42})";
    std::string headersJson = R"({"Content-Type":"application/json"})";

    void* postResp = http.Post(client,
                               (const uint8_t*)postUrl.c_str(), (int)postUrl.size(),
                               (const uint8_t*)jsonBody.c_str(), (int)jsonBody.size(),
                               (const uint8_t*)headersJson.c_str(), (int)headersJson.size());

    if (postResp) {
        int status = http.Response_GetStatusCode(postResp);
        int bodyLen = http.Response_GetBodyLength(postResp);

        std::vector<uint8_t> body(bodyLen);
        http.Response_ReadBody(postResp, body.data(), bodyLen);

        printf("POST %s => %d\n%.*s\n", postUrl.c_str(), status, bodyLen, (const char*)body.data());
        http.Response_Destroy(postResp);
    }

    // 下载文件
    std::string dlUrl = "https://httpbin.org/image/png";
    std::string dlPath = "C:\\temp\\test.png";
    int dlResult = http.DownloadFile(client,
                                     (const uint8_t*)dlUrl.c_str(), (int)dlUrl.size(),
                                     (const uint8_t*)dlPath.c_str(), (int)dlPath.size());
    printf("Download: %s\n", dlResult == 0 ? "OK" : "FAILED");

    // 检查错误
    if (dlResult != 0) {
        char errBuf[1024]{};
        int errLen = http.GetLastError(errBuf, sizeof(errBuf));
        if (errLen > 0) printf("Last error: %.*s\n", errLen, errBuf);
    }

    // 读取响应头
    std::string getUrl2 = "https://httpbin.org/response-headers?Content-Type=text/plain";
    void* resp2 = http.Get(client,
                           (const uint8_t*)getUrl2.c_str(), (int)getUrl2.size(),
                           nullptr, 0);
    if (resp2) {
        std::string hdrName = "Content-Type";
        int hdrLen = http.Response_GetHeaderLength(resp2,
                                                   (const uint8_t*)hdrName.c_str(), (int)hdrName.size());
        if (hdrLen > 0) {
            std::vector<uint8_t> hdrBuf(hdrLen);
            http.Response_ReadHeader(resp2,
                                     (const uint8_t*)hdrName.c_str(), (int)hdrName.size(),
                                     hdrBuf.data(), hdrLen);
            printf("Content-Type: %.*s\n", hdrLen, (const char*)hdrBuf.data());
        }
        http.Response_Destroy(resp2);
    }

    // 销毁客户端
    http.Destroy(client);
    FreeLibrary(hDll);
    return 0;
}
```

---

## 注意事项

1. **线程安全**：每个客户端句柄可在单线程中安全使用。不同线程应创建各自的客户端实例。
2. **内存管理**：
   - 每个 `DrxHttp_Create*` 必须对应一个 `DrxHttp_Destroy`
   - 每个请求返回的响应句柄必须调用 `DrxHttp_Response_Destroy` 释放
3. **错误处理**：请求失败时返回 `nullptr`（0），可通过 `DrxHttp_GetLastError` 获取 UTF-8 错误信息
4. **字符串编码**：所有字符串参数必须为 UTF-8 编码的字节指针 + 长度
5. **Headers 编码格式**：请求头通过 JSON 对象传入，例如 `{"Authorization":"Bearer xxx"}`
6. **同步阻塞**：所有请求在内部使用异步转同步，调用线程会阻塞直到请求完成

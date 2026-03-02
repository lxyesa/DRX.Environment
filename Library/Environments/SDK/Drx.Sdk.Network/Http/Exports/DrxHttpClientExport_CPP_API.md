# DrxHttpClient C++ API 文档

## 概述

**DrxHttpClientExport** 是 `.NET 9.0` 导出的原生 HTTP 客户端库，供 C++ 开发者通过 P/Invoke (`LoadLibrary` / `GetProcAddress`) 调用。

- ✅ 完整的 HTTP 方法支持 (GET, POST, PUT, DELETE, PATCH 等)
- ✅ 文件上传/下载功能
- ✅ 自定义请求头与超时控制
- ✅ 句柄管理模式，安全的响应生命周期
- ✅ UTF-8 编码，支持国际化

---

## 快速开始

### 1. 加载库并获取函数

```cpp
typedef nint IntPtr;

// 加载 .NET 生成的 DLL
HMODULE hLib = LoadLibrary(L"Drx.Sdk.Network.dll");
if (!hLib) {
    printf("Failed to load DLL\n");
    return;
}

// 方式一：通过导出表获取所有函数指针
typedef IntPtr (*GetDrxHttpClientExportsPtr)(int* outCount);
auto GetDrxHttpClientExports = 
    (GetDrxHttpClientExportsPtr)GetProcAddress(hLib, "GetDrxHttpClientExports");

int count = 0;
nint* exportTable = (nint*)GetDrxHttpClientExports(&count);
// count 应为 22
// 之后通过 exportTable[index] 获取函数指针

// 方式二：直接获取单个函数
typedef IntPtr (*CreateFn)(void);
auto DrxHttp_Create = (CreateFn)GetProcAddress(hLib, "DrxHttp_Create");
```

### 2. 发送 GET 请求

```cpp
// 创建客户端
IntPtr client = DrxHttp_Create();

// 发送 GET 请求（无请求头）
const char* url = "https://api.example.com/users";
IntPtr response = DrxHttp_Get(client, (byte*)url, strlen(url), NULL, 0);

if (response != NULL) {
    // 读取响应
    int statusCode = DrxHttp_Response_GetStatusCode(response);
    int bodyLen = DrxHttp_Response_GetBodyLength(response);
    
    char* body = new char[bodyLen];
    DrxHttp_Response_ReadBody(response, (IntPtr)body, bodyLen);
    
    printf("Status: %d\n", statusCode);
    printf("Body (%d bytes):\n%.*s\n", bodyLen, bodyLen, body);
    
    delete[] body;
    DrxHttp_Response_Destroy(response);
}

// 清理
DrxHttp_Destroy(client);
```

### 3. 发送 POST 请求

```cpp
// 创建客户端
IntPtr client = DrxHttp_Create();

// 准备请求
const char* url = "https://api.example.com/login";
const char* body = R"({"username":"alice","password":"secret"})";
const char* headers = R"({"Content-Type":"application/json"})";

// 发送 POST
IntPtr response = DrxHttp_Post(
    client,
    (byte*)url, strlen(url),
    (byte*)body, strlen(body),
    (byte*)headers, strlen(headers)
);

if (response != NULL) {
    int statusCode = DrxHttp_Response_GetStatusCode(response);
    printf("Response status: %d\n", statusCode);
    
    DrxHttp_Response_Destroy(response);
}

DrxHttp_Destroy(client);
```

---

## 函数参考

### 核心概念

| 概念 | 说明 |
|------|------|
| **IntPtr** | 64 位指针，指向托管的 HTTP 客户端或响应对象 |
| **UTF-8** | 所有字符串以 UTF-8 字节表示 + 长度传入 |
| **句柄** | 必须配对 `Create/Destroy`，响应需配对 `Destroy` |
| **请求头** | JSON 格式 `{"Header-Name":"value", ...}` |

---

### 客户端生命周期

#### `DrxHttp_Create`

创建新的 HTTP 客户端实例。

```cpp
typedef IntPtr (*CreateFn)(void);

IntPtr client = DrxHttp_Create();
if (client == NULL) {
    printf("Failed to create HTTP client\n");
    return;
}
```

**返回值**  
- `IntPtr`: 新客户端句柄  
- `NULL`: 创建失败

---

#### `DrxHttp_CreateWithBaseUrl`

创建带有基础 URL 的 HTTP 客户端实例。

```cpp
typedef IntPtr (*CreateWithBaseUrlFn)(byte* urlUtf8, int urlLen);

const char* baseUrl = "https://api.example.com";
IntPtr client = DrxHttp_CreateWithBaseUrl(
    (byte*)baseUrl, strlen(baseUrl)
);

// 后续请求可使用相对 URL
IntPtr response = DrxHttp_Get(
    client,
    (byte*)"/users/list", 11,  // 相对 URL
    NULL, 0
);
```

**参数**  
- `urlUtf8`: 基础 URL (UTF-8)  
- `urlLen`: URL 字节长度  

**返回值**  
- `IntPtr`: 新客户端句柄  
- `NULL`: 创建失败

---

#### `DrxHttp_SetDefaultHeader`

为客户端设置默认请求头（对所有请求生效）。

```cpp
typedef void (*SetDefaultHeaderFn)(
    IntPtr clientPtr,
    byte* nameUtf8,
    int nameLen,
    byte* valueUtf8,
    int valueLen
);

// 设置 User-Agent
const char* key = "User-Agent";
const char* val = "MyApp/1.0";
DrxHttp_SetDefaultHeader(client, 
    (byte*)key, strlen(key),
    (byte*)val, strlen(val)
);

// 设置 Authorization
const char* auth = "Bearer abc123def456";
DrxHttp_SetDefaultHeader(client,
    (byte*)"Authorization", 13,
    (byte*)auth, strlen(auth)
);
```

**参数**  
- `clientPtr`: 客户端句柄  
- `nameUtf8`: 请求头名  
- `nameLen`: 请求头名长度  
- `valueUtf8`: 请求头值  
- `valueLen`: 请求头值长度  

⚠️ **注意**：对同一个 header 调用多次会覆盖之前的值。

---

#### `DrxHttp_SetTimeout`

设置请求超时时间。

```cpp
typedef void (*SetTimeoutFn)(IntPtr clientPtr, int timeoutMs);

// 设置 30 秒超时
DrxHttp_SetTimeout(client, 30000);
```

**参数**  
- `clientPtr`: 客户端句柄  
- `timeoutMs`: 超时时间（毫秒）  

**示例**
```cpp
DrxHttp_SetTimeout(client, 5000);  // 5 秒
```

---

#### `DrxHttp_Destroy`

销毁 HTTP 客户端实例并释放资源。

```cpp
typedef void (*DestroyFn)(IntPtr clientPtr);

DrxHttp_Destroy(client);
// client 不再可用
```

⚠️ **必须调用**：每个 `Create()` 返回的客户端都需要配对的 `Destroy()` 调用。

---

### HTTP 请求方法

#### `DrxHttp_Get`

发送 GET 请求。

```cpp
typedef IntPtr (*GetFn)(
    IntPtr clientPtr,
    byte* urlUtf8,
    int urlLen,
    byte* headersJson,      // 可为 NULL
    int headersJsonLen
);

// 基础 GET（无请求头）
IntPtr response = DrxHttp_Get(
    client,
    (byte*)"https://api.example.com/data", 29,
    NULL, 0
);

// 带自定义请求头的 GET
const char* headers = R"({"X-Custom":"value","Accept":"application/json"})";
IntPtr response = DrxHttp_Get(
    client,
    (byte*)"https://api.example.com/data", 29,
    (byte*)headers, strlen(headers)
);
```

**参数**  
- `clientPtr`: 客户端句柄  
- `urlUtf8`: 请求 URL  
- `urlLen`: URL 字节长度  
- `headersJson`: JSON 格式的请求头，可为 NULL/0  
- `headersJsonLen`: 请求头 JSON 长度  

**返回值**  
- `IntPtr`: 响应句柄  
- `NULL`: 请求失败

---

#### `DrxHttp_Post`

发送 POST 请求，请求体为 UTF-8 字符串（通常是 JSON）。

```cpp
typedef IntPtr (*PostFn)(
    IntPtr clientPtr,
    byte* urlUtf8,
    int urlLen,
    byte* bodyUtf8,
    int bodyLen,
    byte* headersJson,
    int headersJsonLen
);

const char* url = "https://api.example.com/users";
const char* body = R"({"name":"Alice","age":30})";
const char* headers = R"({"Content-Type":"application/json"})";

IntPtr response = DrxHttp_Post(
    client,
    (byte*)url, strlen(url),
    (byte*)body, strlen(body),
    (byte*)headers, strlen(headers)
);
```

**参数**  
- `clientPtr`: 客户端句柄  
- `urlUtf8`: 请求 URL  
- `urlLen`: URL 长度  
- `bodyUtf8`: 请求体 (UTF-8 字符串)  
- `bodyLen`: 请求体长度  
- `headersJson`: JSON 格式请求头  
- `headersJsonLen`: 请求头长度  

**返回值**: 响应句柄或 NULL

---

#### `DrxHttp_PostBytes`

发送 POST 请求，请求体为原始字节数组（如二进制数据、图片等）。

```cpp
typedef IntPtr (*PostBytesFn)(
    IntPtr clientPtr,
    byte* urlUtf8,
    int urlLen,
    byte* bodyData,
    int bodyLen,
    byte* headersJson,
    int headersJsonLen
);

// 发送图片数据
FILE* file = fopen("image.png", "rb");
fseek(file, 0, SEEK_END);
int fileSize = ftell(file);
fseek(file, 0, SEEK_SET);

byte* imageData = new byte[fileSize];
fread(imageData, 1, fileSize, file);
fclose(file);

const char* headers = R"({"Content-Type":"image/png"})";

IntPtr response = DrxHttp_PostBytes(
    client,
    (byte*)"https://api.example.com/upload", 30,
    imageData, fileSize,
    (byte*)headers, strlen(headers)
);

delete[] imageData;
```

**参数**: 同 `DrxHttp_Post`，但 `bodyData` 为二进制字节

**返回值**: 响应句柄或 NULL

---

#### `DrxHttp_Put`

发送 PUT 请求，请求体为 UTF-8 字符串。

```cpp
typedef IntPtr (*PutFn)(
    IntPtr clientPtr,
    byte* urlUtf8,
    int urlLen,
    byte* bodyUtf8,
    int bodyLen,
    byte* headersJson,
    int headersJsonLen
);

const char* url = "https://api.example.com/users/123";
const char* body = R"({"name":"Bob","age":25})";
const char* headers = R"({"Content-Type":"application/json"})";

IntPtr response = DrxHttp_Put(
    client,
    (byte*)url, strlen(url),
    (byte*)body, strlen(body),
    (byte*)headers, strlen(headers)
);
```

**返回值**: 响应句柄或 NULL

---

#### `DrxHttp_Delete`

发送 DELETE 请求。

```cpp
typedef IntPtr (*DeleteFn)(
    IntPtr clientPtr,
    byte* urlUtf8,
    int urlLen,
    byte* headersJson,
    int headersJsonLen
);

const char* url = "https://api.example.com/users/123";

IntPtr response = DrxHttp_Delete(
    client,
    (byte*)url, strlen(url),
    NULL, 0
);
```

**返回值**: 响应句柄或 NULL

---

#### `DrxHttp_Send`

发送任意 HTTP 方法的请求。

```cpp
typedef IntPtr (*SendFn)(
    IntPtr clientPtr,
    byte* methodUtf8,
    int methodLen,
    byte* urlUtf8,
    int urlLen,
    byte* bodyUtf8,
    int bodyLen,
    byte* headersJson,
    int headersJsonLen
);

// 发送 PATCH 请求
const char* method = "PATCH";
const char* url = "https://api.example.com/users/123";
const char* body = R"({"status":"active"})";
const char* headers = R"({"Content-Type":"application/json"})";

IntPtr response = DrxHttp_Send(
    client,
    (byte*)method, strlen(method),
    (byte*)url, strlen(url),
    (byte*)body, strlen(body),
    (byte*)headers, strlen(headers)
);

// 也可以发送 HEAD 请求
const char* head = "HEAD";
IntPtr response = DrxHttp_Send(
    client,
    (byte*)head, strlen(head),
    (byte*)url, strlen(url),
    NULL, 0,
    NULL, 0
);
```

**参数**  
- `methodUtf8`: HTTP 方法名 (GET, POST, PATCH, OPTIONS 等)  
- `methodLen`: 方法名长度  
- 其他参数同 POST  

**支持方法**: GET, POST, PUT, DELETE, PATCH, HEAD, OPTIONS 等

**返回值**: 响应句柄或 NULL

---

### 文件操作

#### `DrxHttp_DownloadFile`

下载文件到本地路径。

```cpp
typedef int (*DownloadFileFn)(
    IntPtr clientPtr,
    byte* urlUtf8,
    int urlLen,
    byte* destPathUtf8,
    int destPathLen
);

const char* url = "https://cdn.example.com/file.zip";
const char* destPath = "C:\\Downloads\\file.zip";

int result = DrxHttp_DownloadFile(
    client,
    (byte*)url, strlen(url),
    (byte*)destPath, strlen(destPath)
);

if (result == 0) {
    printf("下载成功\n");
} else {
    printf("下载失败\n");
    // 调用 DrxHttp_GetLastError 获取详细错误信息
}
```

**参数**  
- `clientPtr`: 客户端句柄  
- `urlUtf8`: 文件 URL  
- `urlLen`: URL 长度  
- `destPathUtf8`: 本地保存路径 (UTF-8)  
- `destPathLen`: 路径长度  

**返回值**  
- `0`: 成功  
- `-1`: 失败（调用 `DrxHttp_GetLastError()` 获取原因）

**示例：支持中文路径**
```cpp
const char* path = u8"D:\\下载\\文件.zip";  // UTF-8 编码
DrxHttp_DownloadFile(client, 
    (byte*)url, strlen(url),
    (byte*)path, strlen(path)
);
```

---

#### `DrxHttp_UploadFile`

上传本地文件到指定 URL（使用 multipart/form-data）。

```cpp
typedef IntPtr (*UploadFileFn)(
    IntPtr clientPtr,
    byte* urlUtf8,
    int urlLen,
    byte* filePathUtf8,
    int filePathLen
);

const char* url = "https://api.example.com/upload";
const char* filePath = "C:\\path\\to\\file.pdf";

IntPtr response = DrxHttp_UploadFile(
    client,
    (byte*)url, strlen(url),
    (byte*)filePath, strlen(filePath)
);

if (response != NULL) {
    int statusCode = DrxHttp_Response_GetStatusCode(response);
    printf("Upload response: %d\n", statusCode);
    
    DrxHttp_Response_Destroy(response);
}
```

**参数**  
- `clientPtr`: 客户端句柄  
- `urlUtf8`: 上传 URL  
- `urlLen`: URL 长度  
- `filePathUtf8`: 本地文件路径  
- `filePathLen`: 路径长度  

**返回值**: 响应句柄或 NULL

**注意**  
- 在线程中自动使用 `multipart/form-data` 编码  
- 字段名为 `file`（服务端应配置为接收此名称的字段）

---

### 响应读取

#### `DrxHttp_Response_GetStatusCode`

获取响应状态码。

```cpp
typedef int (*ResponseGetStatusCodeFn)(IntPtr respPtr);

int statusCode = DrxHttp_Response_GetStatusCode(response);
printf("Status: %d\n", statusCode);  // 200, 404, 500 等
```

**返回值**  
- HTTP 状态码  
- `-1`: 无效句柄

---

#### `DrxHttp_Response_GetBodyLength`

获取响应体字节长度。

```cpp
typedef int (*ResponseGetBodyLengthFn)(IntPtr respPtr);

int bodyLen = DrxHttp_Response_GetBodyLength(response);
printf("Body size: %d bytes\n", bodyLen);
```

**返回值**: 响应体字节长度（0 表示空体或出错）

---

#### `DrxHttp_Response_ReadBody`

将响应体拷贝到调用方缓冲区。

```cpp
typedef int (*ResponseReadBodyFn)(
    IntPtr respPtr,
    IntPtr outBuffer,
    int outCapacity
);

// 完整的读取流程
int bodyLen = DrxHttp_Response_GetBodyLength(response);
char* body = new char[bodyLen];

int copied = DrxHttp_Response_ReadBody(
    response,
    (IntPtr)body,
    bodyLen
);

if (copied > 0) {
    printf("Body (%d bytes):\n%.*s\n", copied, copied, body);
} else {
    printf("Failed to read body\n");
}

delete[] body;
```

**参数**  
- `respPtr`: 响应句柄  
- `outBuffer`: 调用方分配的输出缓冲区  
- `outCapacity`: 缓冲区容量  

**返回值**: 实际拷贝的字节数

**推荐流程**  
1. 调用 `GetBodyLength()` 获取长度  
2. 分配足够的缓冲区  
3. 调用 `ReadBody()` 拷贝数据

---

#### `DrxHttp_Response_GetHeaderLength`

获取指定响应头的字节长度。

```cpp
typedef int (*ResponseGetHeaderLengthFn)(
    IntPtr respPtr,
    byte* nameUtf8,
    int nameLen
);

// 获取 Content-Type 长度
int headerLen = DrxHttp_Response_GetHeaderLength(
    response,
    (byte*)"Content-Type", 12
);

if (headerLen > 0) {
    printf("Content-Type 头长度: %d\n", headerLen);
} else {
    printf("Content-Type 不存在\n");
}
```

**参数**  
- `respPtr`: 响应句柄  
- `nameUtf8`: 请求头名  
- `nameLen`: 请求头名长度  

**返回值**: 请求头值的字节长度（不存在返回 0）

---

#### `DrxHttp_Response_ReadHeader`

读取指定响应头的值。

```cpp
typedef int (*ResponseReadHeaderFn)(
    IntPtr respPtr,
    byte* nameUtf8,
    int nameLen,
    IntPtr outBuffer,
    int outCapacity
);

// 读取 Content-Type
int headerLen = DrxHttp_Response_GetHeaderLength(response, 
    (byte*)"Content-Type", 12);

if (headerLen > 0) {
    char* header = new char[headerLen + 1];
    
    int copied = DrxHttp_Response_ReadHeader(
        response,
        (byte*)"Content-Type", 12,
        (IntPtr)header,
        headerLen
    );
    
    header[copied] = '\0';
    printf("Content-Type: %s\n", header);
    
    delete[] header;
}
```

**参数**  
- `respPtr`: 响应句柄  
- `nameUtf8`: 请求头名  
- `nameLen`: 请求头名长度  
- `outBuffer`: 输出缓冲区  
- `outCapacity`: 缓冲区容量  

**返回值**: 实际拷贝的字节数

---

#### `DrxHttp_Response_Destroy`

销毁响应句柄，释放内存。

```cpp
typedef void (*ResponseDestroyFn)(IntPtr respPtr);

DrxHttp_Response_Destroy(response);
// response 不再可用
```

⚠️ **必须调用**：每个请求返回的响应都需要配对的 `Destroy()` 调用。

---

### 错误处理

#### `DrxHttp_GetLastError`

获取最后一次错误信息（当前线程）。

```cpp
typedef int (*GetLastErrorFn)(IntPtr outBuffer, int outCapacity);

// 获取错误消息
// 第一步：先用 NULL 缓冲获取长度
int errLen = DrxHttp_GetLastError(NULL, 0);
if (errLen == 0) {
    printf("无错误\n");
    return;
}

// 第二步：分配缓冲并读取
char* errMsg = new char[errLen + 1];
int copied = DrxHttp_GetLastError((IntPtr)errMsg, errLen);
errMsg[copied] = '\0';

printf("错误: %s\n", errMsg);
delete[] errMsg;
```

**参数**  
- `outBuffer`: 输出缓冲区（可为 NULL 来探测长度）  
- `outCapacity`: 缓冲区容量  

**返回值**: 错误消息字节长度（无错误返回 0）

---

#### `DrxHttp_FreeBuffer`

释放由导出函数通过 `CoTaskMemAlloc` 分配的非托管内存。

```cpp
typedef void (*FreeBufferFn)(IntPtr buffer);

// 一般情况下不需要手动调用
// 但如果导出函数返回了托管分配的指针，使用此函数释放
DrxHttp_FreeBuffer(buffer);
```

---

## 导出函数表

通过 `GetDrxHttpClientExports()` 一次性获取所有函数指针表。

```cpp
typedef IntPtr (*GetDrxHttpClientExportsFn)(int* outCount);
auto GetDrxHttpClientExports = 
    (GetDrxHttpClientExportsFn)GetProcAddress(hLib, "GetDrxHttpClientExports");

int count = 0;
nint* table = (nint*)GetDrxHttpClientExports(&count);
// count 应为 22
```

**导出表索引**

| 索引 | 函数名 | 说明 |
|------|--------|------|
| 0 | `DrxHttp_Create` | 创建客户端 |
| 1 | `DrxHttp_CreateWithBaseUrl` | 创建带基础URL的客户端 |
| 2 | `DrxHttp_SetDefaultHeader` | 设置默认请求头 |
| 3 | `DrxHttp_SetTimeout` | 设置超时 |
| 4 | `DrxHttp_Destroy` | 销毁客户端 |
| 5 | `DrxHttp_Get` | GET 请求 |
| 6 | `DrxHttp_Post` | POST 请求 |
| 7 | `DrxHttp_PostBytes` | POST 二进制请求 |
| 8 | `DrxHttp_Put` | PUT 请求 |
| 9 | `DrxHttp_Delete` | DELETE 请求 |
| 10 | `DrxHttp_Send` | 任意 HTTP 方法 |
| 11 | `DrxHttp_DownloadFile` | 下载文件 |
| 12 | `DrxHttp_UploadFile` | 上传文件 |
| 13 | `DrxHttp_Response_GetStatusCode` | 获取状态码 |
| 14 | `DrxHttp_Response_GetBodyLength` | 获取响应体长度 |
| 15 | `DrxHttp_Response_ReadBody` | 读取响应体 |
| 16 | `DrxHttp_Response_ReadHeader` | 读取响应头 |
| 17 | `DrxHttp_Response_GetHeaderLength` | 获取响应头长度 |
| 18 | `DrxHttp_Response_Destroy` | 销毁响应 |
| 19 | `DrxHttp_FreeBuffer` | 释放缓冲区 |
| 20 | `DrxHttp_GetLastError` | 获取错误信息 |

---

## 完整示例

### 基础请求流程

```cpp
#include <stdio.h>
#include <string.h>
#include <windows.h>

// 类型定义
typedef void* IntPtr;

typedef IntPtr (*CreateClientFn)(void);
typedef IntPtr (*HttpGetFn)(IntPtr, byte*, int, byte*, int);
typedef int (*GetStatusFn)(IntPtr);
typedef int (*GetBodyLenFn)(IntPtr);
typedef int (*ReadBodyFn)(IntPtr, IntPtr, int);
typedef void (*DestroyRespFn)(IntPtr);
typedef void (*DestroyClientFn)(IntPtr);
typedef int (*GetLastErrorFn)(IntPtr, int);

int main() {
    // 加载库
    HMODULE hLib = LoadLibrary(L"Drx.Sdk.Network.dll");
    if (!hLib) {
        printf("Failed to load DLL\n");
        return 1;
    }

    // 获取函数
    auto CreateClient = (CreateClientFn)GetProcAddress(hLib, "DrxHttp_Create");
    auto HttpGet = (HttpGetFn)GetProcAddress(hLib, "DrxHttp_Get");
    auto GetStatus = (GetStatusFn)GetProcAddress(hLib, "DrxHttp_Response_GetStatusCode");
    auto GetBodyLen = (GetBodyLenFn)GetProcAddress(hLib, "DrxHttp_Response_GetBodyLength");
    auto ReadBody = (ReadBodyFn)GetProcAddress(hLib, "DrxHttp_Response_ReadBody");
    auto DestroyResp = (DestroyRespFn)GetProcAddress(hLib, "DrxHttp_Response_Destroy");
    auto DestroyClient = (DestroyClientFn)GetProcAddress(hLib, "DrxHttp_Destroy");

    // 创建客户端
    IntPtr client = CreateClient();
    if (!client) {
        printf("Failed to create client\n");
        FreeLibrary(hLib);
        return 1;
    }

    // 发送请求
    const char* url = "https://api.example.com/status";
    IntPtr response = HttpGet(client, (byte*)url, strlen(url), NULL, 0);

    if (response) {
        // 读取响应
        int statusCode = GetStatus(response);
        int bodyLen = GetBodyLen(response);

        printf("Status: %d\n", statusCode);
        printf("Body length: %d\n", bodyLen);

        if (bodyLen > 0) {
            char* body = new char[bodyLen];
            int copied = ReadBody(response, (IntPtr)body, bodyLen);
            printf("Body:\n%.*s\n", copied, body);
            delete[] body;
        }

        DestroyResp(response);
    }

    // 清理
    DestroyClient(client);
    FreeLibrary(hLib);

    return 0;
}
```

### 发送 JSON 数据

```cpp
// 发送 POST 请求
IntPtr client = DrxHttp_Create();

const char* url = "https://api.example.com/data";
const char* body = R"({
    "name": "Alice",
    "age": 30,
    "email": "alice@example.com"
})";
const char* headers = R"({
    "Content-Type": "application/json",
    "User-Agent": "MyApp/1.0"
})";

IntPtr response = DrxHttp_Post(
    client,
    (byte*)url, strlen(url),
    (byte*)body, strlen(body),
    (byte*)headers, strlen(headers)
);

if (response) {
    int status = DrxHttp_Response_GetStatusCode(response);
    if (status == 200 || status == 201) {
        printf("请求成功\n");
    } else {
        printf("请求失败，状态码: %d\n", status);
    }
    
    DrxHttp_Response_Destroy(response);
}

DrxHttp_Destroy(client);
```

### 下载文件

```cpp
IntPtr client = DrxHttp_Create();

const char* url = "https://cdn.example.com/archive.zip";
const char* savePath = u8"D:\\Downloads\\archive.zip";  // UTF-8 中文路径

int result = DrxHttp_DownloadFile(
    client,
    (byte*)url, strlen(url),
    (byte*)savePath, strlen(savePath)
);

if (result == 0) {
    printf("文件下载成功\n");
} else {
    printf("文件下载失败\n");
}

DrxHttp_Destroy(client);
```

### 实现重试机制

```cpp
IntPtr PerformRequestWithRetry(
    IntPtr client,
    const char* url,
    int maxRetries = 3,
    int retryDelayMs = 1000
) {
    for (int i = 0; i < maxRetries; ++i) {
        IntPtr response = DrxHttp_Get(
            client,
            (byte*)url, strlen(url),
            NULL, 0
        );

        if (response != NULL) {
            int status = DrxHttp_Response_GetStatusCode(response);
            
            if (status == 200 || status == 201 || status == 204) {
                // 成功
                return response;
            } else if (status >= 500) {
                // 服务器错误，可重试
                DrxHttp_Response_Destroy(response);
                if (i < maxRetries - 1) {
                    printf("服务器错误，%d ms 后重试...\n", retryDelayMs);
                    Sleep(retryDelayMs);
                }
            } else if (status >= 400) {
                // 客户端错误，不重试
                return response;
            }
        }
    }

    return NULL;
}
```

---

## 最佳实践

### 1. 资源管理（RAII 风格）

```cpp
// RAII 包装器示例
class HttpClientWrapper {
private:
    IntPtr client;
    
public:
    HttpClientWrapper() {
        client = DrxHttp_Create();
    }
    
    ~HttpClientWrapper() {
        if (client) {
            DrxHttp_Destroy(client);
        }
    }
    
    IntPtr GetHandle() const { return client; }
};

// 使用
void SafeRequest() {
    HttpClientWrapper client;  // 自动创建
    // ... 使用 client
    // 自动销毁，不用手动调用 Destroy
}
```

### 2. 响应内容读取

```cpp
// 安全的响应读取
bool ReadResponseBody(IntPtr response, std::string& outBody) {
    int len = DrxHttp_Response_GetBodyLength(response);
    if (len <= 0) {
        outBody.clear();
        return true;  // 空响应体
    }
    
    char* buffer = new char[len];
    try {
        int copied = DrxHttp_Response_ReadBody(response, (IntPtr)buffer, len);
        outBody.assign(buffer, copied);
        return true;
    } catch (...) {
        return false;
    } finally {
        delete[] buffer;
    }
}
```

### 3. 错误处理

```cpp
// 获取详细错误信息
void PrintLastError() {
    int errLen = DrxHttp_GetLastError(NULL, 0);
    if (errLen > 0) {
        char* errMsg = new char[errLen + 1];
        try {
            DrxHttp_GetLastError((IntPtr)errMsg, errLen);
            errMsg[errLen] = '\0';
            printf("错误: %s\n", errMsg);
        } finally {
            delete[] errMsg;
        }
    }
}
```

### 4. 超时控制

```cpp
// 设置合理的超时
IntPtr client = DrxHttp_Create();

// 短超时用于快速检查
DrxHttp_SetTimeout(client, 5000);  // 5秒
IntPtr healthCheck = DrxHttp_Get(client, ...);

// 长超时用于文件下载
DrxHttp_SetTimeout(client, 120000);  // 2分钟
int downloadResult = DrxHttp_DownloadFile(client, ...);
```

### 5. 连接复用

```cpp
// Create 一个客户端并复用多个请求
IntPtr client = DrxHttp_CreateWithBaseUrl(
    (byte*)"https://api.example.com", 23
);

// 设置全局请求头
DrxHttp_SetDefaultHeader(client,
    (byte*)"Authorization", 13,
    (byte*)token, tokenLen
);

// 多个请求共享一个客户端，提高效率
IntPtr resp1 = DrxHttp_Get(client, (byte*)"/users", 6, NULL, 0);
IntPtr resp2 = DrxHttp_Get(client, (byte*)"/products", 9, NULL, 0);
IntPtr resp3 = DrxHttp_Get(client, (byte*)"/orders", 7, NULL, 0);

// 处理响应...

DrxHttp_Destroy(client);  // 一次销毁
```

---

## 常见问题

**Q: JSON 格式的请求头如何编写？**  
A: 使用标准 JSON 对象格式，例如 `{"Header-Name":"value","Another":"value2"}`。特殊字符需转义。

**Q: 支持 HTTPS 吗？**  
A: 是的。底层使用 .NET HttpClient，自动处理 HTTPS 和 SSL/TLS。

**Q: 能设置连接池大小吗？**  
A: 当前 API 不提供此配置。底层 .NET HttpClient 自动管理连接池。

**Q: 流式上传/下载支持吗？**  
A: `DownloadFile` 和 `UploadFile` 内部支持流式传输，避免将整个文件加载到内存。

**Q: 是否线程安全？**  
A: 单个 `IntPtr` 客户端/响应对象不是线程安全的。建议每个线程创建自己的客户端实例。

---

## 版本历史

| 版本 | 日期 | 说明 |
|------|------|------|
| 1.0 | 2026-03-02 | 初版发布 |

---

## 许可

Copyright © DRX Framework Team. All rights reserved.


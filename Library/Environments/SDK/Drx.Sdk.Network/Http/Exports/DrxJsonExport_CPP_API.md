# DrxJson C++ API 文档

## 概述

**DrxJsonExport** 是 `.NET 9.0` 导出的原生 JSON 操作库，供 C++ 开发者通过 P/Invoke (`LoadLibrary` / `GetProcAddress`) 调用。

- ✅ 基于 **System.Text.Json**，与服务端序列化完全兼容
- ✅ 句柄管理模式，安全高效的内存生命周期
- ✅ UTF-8 字符编码，支持国际化文本
- ✅ 支持嵌套对象/数组、完整的 JSON 操作

---

## 快速开始

### 1. 加载库

```cpp
typedef nint IntPtr;
typedef int (*FnPtr)(void);

// 加载 .NET 生成的 DLL
HMODULE hLib = LoadLibrary(L"Drx.Sdk.Network.dll");
if (!hLib) {
    printf("Failed to load DLL\n");
    return;
}

// 方式一：通过导出表获取所有函数指针
typedef IntPtr (*GetDrxJsonExportsPtr)(int* outCount);
auto GetDrxJsonExports = (GetDrxJsonExportsPtr)GetProcAddress(hLib, "GetDrxJsonExports");

int count = 0;
nint* exportTable = (nint*)GetDrxJsonExports(&count);
// 之后通过 exportTable[index] 获取函数指针

// 方式二：直接获取单个函数
typedef IntPtr (*CreateObjectPtr)(void);
auto CreateObject = (CreateObjectPtr)GetProcAddress(hLib, "DrxJson_CreateObject");
```

### 2. 创建并填充 JSON

```cpp
// 创建对象
IntPtr jsonObj = CreateObject();

// 设置字符串字段
DrxJson_SetString(jsonObj, (byte*)"name", 4, (byte*)"Alice", 5, NULL, 0);

// 设置整数字段
DrxJson_SetInt(jsonObj, (byte*)"age", 3, 30);

// 创建数组
IntPtr jsonArray = DrxJson_CreateArray();
DrxJson_ArrayPushString(jsonArray, (byte*)"C++", 3);
DrxJson_ArrayPushString(jsonArray, (byte*)"JSON", 4);

// 嵌套数组到对象
DrxJson_SetObject(jsonObj, (byte*)"skills", 6, jsonArray);

// 序列化
int len = DrxJson_GetLength(jsonObj);
char* buffer = new char[len];
DrxJson_Serialize(jsonObj, (byte*)buffer, len);
printf("JSON: %s\n", buffer);  // {"name":"Alice","age":30,"skills":["C++","JSON"]}

// 清理
delete[] buffer;
DrxJson_Destroy(jsonObj);
```

---

## 函数参考

### 核心概念

| 概念 | 说明 |
|------|------|
| **IntPtr** | 64 位指针，指向托管的 JsonNode 对象（对象/数组/值）|
| **UTF-8** | 所有字符串以 UTF-8 字节表示 + 长度传入 |
| **句柄** | 使用完毕必须调用 `DrxJson_Destroy()` 释放 |
| **深拷贝** | `SetObject()` 和 `ArrayPushObject()` 自动深拷贝子节点 |

---

### 对象创建与生命周期

#### `DrxJson_CreateObject`

创建空 JSON 对象 `{}`。

```cpp
typedef IntPtr (*CreateObjectFn)(void);

// 调用
IntPtr obj = CreateObject();
if (obj == NULL) {
    printf("Failed to create object\n");
    return;
}
```

**返回值**  
- `IntPtr`: 新对象句柄  
- `NULL`: 创建失败

---

#### `DrxJson_CreateArray`

创建空 JSON 数组 `[]`。

```cpp
typedef IntPtr (*CreateArrayFn)(void);

IntPtr arr = CreateArray();
```

**返回值**  
- `IntPtr`: 新数组句柄  
- `NULL`: 创建失败

---

#### `DrxJson_Parse`

解析 UTF-8 JSON 字符串，返回 JsonNode 句柄（可能是对象、数组或标量值）。

```cpp
typedef IntPtr (*ParseFn)(byte* jsonUtf8, int jsonLen);

const char* json = R"({"status":"ok","data":{"id":123}})";
int jsonLen = strlen(json);
IntPtr node = Parse((byte*)json, jsonLen);

if (node == NULL) {
    int errLen = GetLastError(NULL, 0);
    char* err = new char[errLen];
    GetLastError((byte*)err, errLen);
    printf("Parse error: %s\n", err);
    delete[] err;
    return;
}
```

**参数**  
- `jsonUtf8`: UTF-8 JSON 字符串指针  
- `jsonLen`: 字符串字节长度  

**返回值**  
- `IntPtr`: 解析结果（根节点）  
- `NULL`: 解析失败（调用 `DrxJson_GetLastError()` 获取原因）

---

#### `DrxJson_Destroy`

销毁 JSON 句柄，释放托管引用。

```cpp
typedef void (*DestroyFn)(IntPtr nodePtr);

DrxJson_Destroy(obj);
// obj 不再可用
```

⚠️ **必须调用**：每个 `CreateObject()`, `CreateArray()`, `Parse()` 返回的句柄都需要配对的 `Destroy()` 调用。

---

### 对象字段操作

#### `DrxJson_SetString`

在 JSON 对象设置字符串字段。

```cpp
typedef int (*SetStringFn)(
    IntPtr objPtr,           // 对象句柄
    byte* keyUtf8,           // 字段名 (UTF-8)
    int keyLen,              // 字段名长度
    byte* valueUtf8,         // 字段值 (UTF-8)
    int valueLen             // 字段值长度
);

// 示例
const char* key = "username";
const char* value = "bob";
DrxJson_SetString(obj, (byte*)key, strlen(key), 
                  (byte*)value, strlen(value));
```

**返回值**: 1 成功，0 失败

**示例**
```cpp
// 设置UTF-8中文字符串
const char* chinese = u8"姓名";  // UTF-8 编码的中文
DrxJson_SetString(obj, (byte*)"name", 4, 
                  (byte*)chinese, strlen(chinese));
```

---

#### `DrxJson_SetInt`

在 JSON 对象设置 64 位整数字段。

```cpp
typedef int (*SetIntFn)(
    IntPtr objPtr,
    byte* keyUtf8,
    int keyLen,
    long value                // int64_t
);

DrxJson_SetInt(obj, (byte*)"count", 5, 12345LL);
```

**返回值**: 1 成功，0 失败

---

#### `DrxJson_SetDouble`

在 JSON 对象设置双精度浮点字段。

```cpp
typedef int (*SetDoubleFn)(
    IntPtr objPtr,
    byte* keyUtf8,
    int keyLen,
    double value
);

DrxJson_SetDouble(obj, (byte*)"price", 5, 99.99);
```

**返回值**: 1 成功，0 失败

---

#### `DrxJson_SetBool`

在 JSON 对象设置布尔字段。

```cpp
typedef int (*SetBoolFn)(
    IntPtr objPtr,
    byte* keyUtf8,
    int keyLen,
    int value                // 非零为 true
);

DrxJson_SetBool(obj, (byte*)"active", 6, 1);  // true
DrxJson_SetBool(obj, (byte*)"deleted", 7, 0); // false
```

**返回值**: 1 成功，0 失败

---

#### `DrxJson_SetNull`

在 JSON 对象设置 null 字段。

```cpp
typedef int (*SetNullFn)(
    IntPtr objPtr,
    byte* keyUtf8,
    int keyLen
);

DrxJson_SetNull(obj, (byte*)"optional", 8);  // "optional": null
```

**返回值**: 1 成功，0 失败

---

#### `DrxJson_SetObject`

在 JSON 对象嵌套另一个 JSON 节点（对象或数组）。

```cpp
typedef int (*SetObjectFn)(
    IntPtr objPtr,
    byte* keyUtf8,
    int keyLen,
    IntPtr childPtr           // 子节点句柄（会被深拷贝）
);

IntPtr child = CreateObject();
DrxJson_SetInt(child, (byte*)"x", 1, 10);
DrxJson_SetInt(child, (byte*)"y", 1, 20);

DrxJson_SetObject(parent, (byte*)"point", 5, child);
// child 的所有权已转移，自动释放，不需要再调用 Destroy(child)
```

**返回值**: 1 成功，0 失败

⚠️ **注意**：函数内部进行深拷贝，`childPtr` 的句柄会被自动释放，调用者无需再次调用 `Destroy()`。

---

### 数组元素操作

#### `DrxJson_ArrayPushString`

向 JSON 数组追加字符串元素。

```cpp
typedef int (*ArrayPushStringFn)(
    IntPtr arrPtr,
    byte* valueUtf8,
    int valueLen
);

DrxJson_ArrayPushString(arr, (byte*)"hello", 5);
```

**返回值**: 1 成功，0 失败

---

#### `DrxJson_ArrayPushInt`

向 JSON 数组追加 64 位整数。

```cpp
typedef int (*ArrayPushIntFn)(IntPtr arrPtr, long value);

DrxJson_ArrayPushInt(arr, 100);
DrxJson_ArrayPushInt(arr, 200);
```

**返回值**: 1 成功，0 失败

---

#### `DrxJson_ArrayPushDouble`

向 JSON 数组追加双精度浮点数。

```cpp
typedef int (*ArrayPushDoubleFn)(IntPtr arrPtr, double value);

DrxJson_ArrayPushDouble(arr, 3.14);
```

**返回值**: 1 成功，0 失败

---

#### `DrxJson_ArrayPushBool`

向 JSON 数组追加布尔值。

```cpp
typedef int (*ArrayPushBoolFn)(IntPtr arrPtr, int value);

DrxJson_ArrayPushBool(arr, 1);  // true
DrxJson_ArrayPushBool(arr, 0);  // false
```

**返回值**: 1 成功，0 失败

---

#### `DrxJson_ArrayPushObject`

向 JSON 数组追加子 JSON 节点（对象或数组）。

```cpp
typedef int (*ArrayPushObjectFn)(IntPtr arrPtr, IntPtr childPtr);

IntPtr child = CreateObject();
DrxJson_SetString(child, (byte*)"type", 4, (byte*)"item", 4);

DrxJson_ArrayPushObject(arr, child);
// child 的所有权已转移，自动释放
```

**返回值**: 1 成功，0 失败

⚠️ **注意**：同 `SetObject()`，`childPtr` 会被深拷贝后自动释放。

---

### 对象字段读取

#### `DrxJson_GetString`

从 JSON 对象读取字符串字段。

```cpp
typedef int (*GetStringFn)(
    IntPtr nodePtr,
    byte* keyUtf8,
    int keyLen,
    IntPtr outBuffer,        // 输出缓冲区（分配给调用者）
    int outCapacity          // 缓冲区容量
);

// 步骤1：确定所需缓冲区大小（如果不知道的话，先探测）
char buffer[256] = {0};
int written = GetString(obj, (byte*)"name", 4, 
                       (IntPtr)buffer, sizeof(buffer) - 1);

if (written > 0) {
    buffer[written] = '\0';  // 手动添加 null 终止符
    printf("获取到字符串: %s\n", buffer);
} else if (written == -1) {
    printf("字段不存在或类型错误\n");
}
```

**参数**  
- `nodePtr`: JSON 对象句柄  
- `keyUtf8`: 字段名 (UTF-8)  
- `keyLen`: 字段名长度  
- `outBuffer`: 调用方分配的输出缓冲区  
- `outCapacity`: 缓冲区大小  

**返回值**  
- `> 0`: 实际写入的字节数（不含 null 终止符）  
- `== -1`: 字段不存在或类型不符  

⚠️ **手动 null 终止**：返回值不包括 null 终止符，调用者需手动添加 `buffer[written] = '\0'`。

---

#### `DrxJson_GetInt`

从 JSON 对象读取 64 位整数字段。

```cpp
typedef long (*GetIntFn)(
    IntPtr nodePtr,
    byte* keyUtf8,
    int keyLen
);

long value = GetInt(obj, (byte*)"count", 5);
if (value == LLONG_MIN) {
    printf("字段不存在或类型错误\n");
} else {
    printf("Count: %lld\n", value);
}
```

**返回值**: 字段值或 `LLONG_MIN` （表示错误）

---

#### `DrxJson_GetDouble`

从 JSON 对象读取双精度浮点字段。

```cpp
typedef double (*GetDoubleFn)(
    IntPtr nodePtr,
    byte* keyUtf8,
    int keyLen
);

double price = GetDouble(obj, (byte*)"price", 5);
if (isnan(price)) {
    printf("字段不存在或类型错误\n");
} else {
    printf("Price: %.2f\n", price);
}
```

**返回值**: 字段值或 `NaN` （表示错误）

---

#### `DrxJson_GetBool`

从 JSON 对象读取布尔字段。

```cpp
typedef int (*GetBoolFn)(
    IntPtr nodePtr,
    byte* keyUtf8,
    int keyLen
);

int result = GetBool(obj, (byte*)"active", 6);
if (result == -1) {
    printf("字段不存在\n");
} else if (result == 1) {
    printf("Active: true\n");
} else {
    printf("Active: false\n");
}
```

**返回值**  
- `1`: true  
- `0`: false  
- `-1`: 字段不存在或类型错误

---

#### `DrxJson_HasKey`

检查 JSON 对象是否包含指定键。

```cpp
typedef int (*HasKeyFn)(
    IntPtr nodePtr,
    byte* keyUtf8,
    int keyLen
);

if (HasKey(obj, (byte*)"email", 5) == 1) {
    printf("包含 email 字段\n");
} else {
    printf("不包含 email 字段\n");
}
```

**返回值**  
- `1`: 包含该键  
- `0`: 不包含或出错

---

### 序列化

#### `DrxJson_GetLength`

获取 JSON 节点序列化为 UTF-8 字符串后的字节长度（不含 null 终止符）。

```cpp
typedef int (*GetLengthFn)(IntPtr nodePtr);

int len = GetLength(obj);
printf("序列化后长度: %d 字节\n", len);

char* buffer = new char[len];
```

**返回值**: 序列化后的字节长度

---

#### `DrxJson_Serialize`

将 JSON 节点序列化为 UTF-8 字节串，写入调用方缓冲区。

```cpp
typedef int (*SerializeFn)(
    IntPtr nodePtr,
    IntPtr outBuffer,
    int outCapacity
);

// 完整示例
IntPtr obj = CreateObject();
DrxJson_SetString(obj, (byte*)"key", 3, (byte*)"value", 5);

int len = GetLength(obj);
char* jsonStr = new char[len + 1];

int written = Serialize(obj, (IntPtr)jsonStr, len);
jsonStr[written] = '\0';

printf("JSON: %s\n", jsonStr);  // {"key":"value"}

delete[] jsonStr;
DrxJson_Destroy(obj);
```

**参数**  
- `nodePtr`: JSON 节点句柄  
- `outBuffer`: 调用方分配的输出缓冲区  
- `outCapacity`: 缓冲区容量  

**返回值**: 实际写入的字节数

**推荐流程**  
1.调用 `GetLength()` 获取所需大小  
2. 分配缓冲区  
3. 调用 `Serialize()` 写入  
4. 手动添加 null 终止符

---

### 错误处理

#### `DrxJson_GetLastError`

获取最后一次错误信息（当前线程）。

```cpp
typedef int (*GetLastErrorFn)(
    IntPtr outBuffer,
    int outCapacity
);

// 获取错误信息长度
int errLen = GetLastError(NULL, 0);
if (errLen == 0) {
    printf("无错误\n");
    return;
}

// 分配缓冲区并获取错误
char* errMsg = new char[errLen + 1];
int written = GetLastError((IntPtr)errMsg, errLen);
errMsg[written] = '\0';

printf("错误: %s\n", errMsg);
delete[] errMsg;
```

**返回值**: 错误信息字节长度（无错误返回 0）

---

## 导出函数表

通过 `GetDrxJsonExports()` 一次性获取所有函数指针表。

```cpp
typedef IntPtr (*GetDrxJsonExportsFn)(int* outCount);
auto GetDrxJsonExports = (GetDrxJsonExportsFn)GetProcAddress(hLib, "GetDrxJsonExports");

int count = 0;
nint* table = (nint*)GetDrxJsonExports(&count);
// count 应为 22
```

**导出表索引**

| 索引 | 函数名 | 说明 |
|------|--------|------|
| 0 | `DrxJson_CreateObject` | 创建对象 |
| 1 | `DrxJson_CreateArray` | 创建数组 |
| 2 | `DrxJson_Parse` | 解析 JSON |
| 3 | `DrxJson_Destroy` | 销毁句柄 |
| 4 | `DrxJson_SetString` | 设置字符串 |
| 5 | `DrxJson_SetInt` | 设置整数 |
| 6 | `DrxJson_SetDouble` | 设置浮点数 |
| 7 | `DrxJson_SetBool` | 设置布尔值 |
| 8 | `DrxJson_SetNull` | 设置 null |
| 9 | `DrxJson_SetObject` | 设置子对象 |
| 10 | `DrxJson_ArrayPushString` | 数组追加字符串 |
| 11 | `DrxJson_ArrayPushInt` | 数组追加整数 |
| 12 | `DrxJson_ArrayPushDouble` | 数组追加浮点数 |
| 13 | `DrxJson_ArrayPushBool` | 数组追加布尔值 |
| 14 | `DrxJson_ArrayPushObject` | 数组追加子对象 |
| 15 | `DrxJson_GetString` | 读取字符串 |
| 16 | `DrxJson_GetInt` | 读取整数 |
| 17 | `DrxJson_GetDouble` | 读取浮点数 |
| 18 | `DrxJson_GetBool` | 读取布尔值 |
| 19 | `DrxJson_HasKey` | 检查键存在 |
| 20 | `DrxJson_GetLength` | 获取序列化长度 |
| 21 | `DrxJson_Serialize` | 序列化到缓冲区 |

---

## 完整示例

### 构建请求体

```cpp
// 创建用户注册请求
IntPtr request = CreateObject();

DrxJson_SetString(request, (byte*)"username", 8, 
                 (byte*)"alice", 5);
DrxJson_SetString(request, (byte*)"email", 5, 
                 (byte*)"alice@example.com", 17);
DrxJson_SetString(request, (byte*)"password", 8, 
                 (byte*)"secret123", 9);

// 添加元数据数组
IntPtr metadata = CreateArray();
DrxJson_ArrayPushString(metadata, (byte*)"tag1", 4);
DrxJson_ArrayPushString(metadata, (byte*)"tag2", 4);
DrxJson_SetObject(request, (byte*)"tags", 4, metadata);

// 序列化为 JSON 字符串
int len = GetLength(request);
char* jsonStr = new char[len + 1];
Serialize(request, (IntPtr)jsonStr, len);
jsonStr[len] = '\0';

printf("请求体:\n%s\n", jsonStr);
// {"username":"alice","email":"alice@example.com","password":"secret123","tags":["tag1","tag2"]}

delete[] jsonStr;
DrxJson_Destroy(request);
```

### 解析响应体

```cpp
// 假设从 HTTP 响应得到 JSON 字符串
const char* responseJson = R"({
    "status": "success",
    "data": {
        "userId": 12345,
        "verified": true,
        "balance": 99.99
    }
})";

IntPtr response = Parse((byte*)responseJson, strlen(responseJson));
if (response == NULL) {
    printf("Parse failed\n");
    return;
}

// 读取字段
char status[32] = {0};
int len = GetString(response, (byte*)"status", 6, (IntPtr)status, 31);
status[len] = '\0';
printf("Status: %s\n", status);

// 嵌套字段读取需要先提取子对象
// 注：当前 API 不支持路径查询 (如 "data.userId")
// 需要手动解析嵌套结构

DrxJson_Destroy(response);
```

---

## 最佳实践

### 1. 线程安全

```cpp
// GetLastError 是线程局部的，每个线程有独立的错误状态
// 异步操作中直接调用 GetLastError 即可获得当前线程的错误
```

### 2. 内存泄漏防规避

```cpp
// ✅ 正确：释放所有句柄
IntPtr obj = CreateObject();
DrxJson_SetString(obj, ...);
DrxJson_Destroy(obj);

// ❌ 错误：忘记释放
IntPtr obj = CreateObject();
DrxJson_SetString(obj, ...);
// obj 泄漏！

// ❌ 错误：SetObject 后重复释放
IntPtr child = CreateObject();
DrxJson_SetObject(parent, ..., child);
DrxJson_Destroy(child);  // 错误！child 已在 SetObject 中释放
```

### 3. UTF-8 字符编码

```cpp
// ✅ 使用 u8 前缀表示 UTF-8 字符串字面量
const char* name = u8"名字";
DrxJson_SetString(obj, (byte*)"name", 4, 
                 (byte*)name, strlen(name));

// 中文、日文、Emoji 等多字节字符自动支持
const char* emoji = u8"😀";
DrxJson_SetString(obj, (byte*)"emoji", 5, 
                 (byte*)emoji, strlen(emoji));
```

### 4. 大 JSON 处理

```cpp
// 序列化大 JSON 时，分两步获取长度和写入
int len = GetLength(largeJson);

// 使用动态分配或栈缓冲，避免栈溢出
char* buffer = new char[len + 1];
try {
    Serialize(largeJson, (IntPtr)buffer, len);
    buffer[len] = '\0';
    ProcessJson(buffer);
} finally {
    delete[] buffer;
}
```

---

## 常见问题

**Q: 字符串一定要以 null 终止吗？**  
A: 不需要。API 使用长度参数，直接读取指定字节数。但标准 C 字符串习惯仍然推荐添加。

**Q: 支持 Unicode 转义序列吗？**  
A: 是的。`Parse()` 会自动处理 JSON 中的 `\uXXXX` 转义，但 `SetString()` 的字符串应该是已经解码的 UTF-8。

**Q: 能修改已经嵌入的子对象吗？**  
A: 不能。由于深拷贝机制，修改原对象不会影响已嵌入的副本。需要先修改，再重新嵌入。

**Q: 如何检查数组长度？**  
A: 当前 API 暂不提供直接的数组长度查询。可通过序列化后手动解析，或在应用层跟踪。

---

## 版本历史

| 版本 | 日期 | 说明 |
|------|------|------|
| 1.0 | 2026-03-02 | 初版发布 |

---

## 许可

Copyright © DRX Framework Team. All rights reserved.


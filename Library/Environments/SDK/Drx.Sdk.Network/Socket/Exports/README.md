# DrxTcpClientExport 导出说明
...
[以上原文不变]

全函数用法示例（覆盖所有导出）
本节提供“逐一函数演示”，包含两种绑定方式：
- 方式 A：按导出名逐一 GetProcAddress（与上文一致，适合不使用函数表的情况）
- 方式 B：使用固定导出 GetDrxTcpClientExports 获取“函数指针表”，按索引调用（推荐，稳定且避免裁剪影响）

方式 B：通过函数表调用（推荐）
注意：导出表顺序由托管实现固定，见索引注释或参考 [`GetDrxTcpClientExports()`](./DrxTcpClientExport.cs) 实现与注释。

```cpp
// DrxExportsTable.hpp
#pragma once
#include <windows.h>
#include <cstdint>

typedef void(__cdecl* PacketResponseCallback)(const uint8_t* data, int len);

// 声明获取表函数
typedef void* (__cdecl* Fn_GetDrxTcpClientExports)();

// 索引常量（保持与托管侧一致）
enum DrxIndex : int {
    IDX_Create = 0,
    IDX_CreateWithAes,
    IDX_Connect,
    IDX_SetAesEncryptor,
    IDX_SendPacket,
    IDX_Disconnect,
    IDX_Destroy,

    IDX_JsonCreateFromBytes,
    IDX_JsonCreate,
    IDX_JsonPushString,
    IDX_JsonPushNumber,
    IDX_JsonPushBoolean,
    IDX_JsonPushCompound,
    IDX_JsonReadString,
    IDX_JsonReadBoolean,
    IDX_JsonReadNumber,
    IDX_JsonReadCompound,
    IDX_JsonSerialize,
    IDX_JsonDestroy,
    IDX_Count
};

// 每个槽位的函数签名
using P_Create              = void* (__cdecl*)();
using P_CreateWithAes       = void* (__cdecl*)(const uint8_t*, int, const uint8_t*, int);
using P_Connect             = bool  (__cdecl*)(void*, const uint8_t*, int, int);
using P_SetAesEncryptor     = void  (__cdecl*)(void*, const uint8_t*, int, const uint8_t*, int);
using P_SendPacket          = bool  (__cdecl*)(void*, const uint8_t*, int, PacketResponseCallback, int);
using P_Disconnect          = void  (__cdecl*)(void*);
using P_Destroy             = void  (__cdecl*)(void*);

using P_JsonCreateFromBytes = void* (__cdecl*)(const uint8_t*, int);
using P_JsonCreate          = void* (__cdecl*)();
using P_JsonPushString      = void* (__cdecl*)(void*, const uint8_t*, int, const uint8_t*, int);
using P_JsonPushNumber      = void* (__cdecl*)(void*, const uint8_t*, int, double);
using P_JsonPushBoolean     = void* (__cdecl*)(void*, const uint8_t*, int, uint8_t);
using P_JsonPushCompound    = void* (__cdecl*)(void*, const uint8_t*, int, void*);
using P_JsonReadString      = int   (__cdecl*)(void*, const uint8_t*, int, uint8_t*, int);
using P_JsonReadBoolean     = uint8_t(__cdecl*)(void*, const uint8_t*, int, uint8_t*);
using P_JsonReadNumber      = double(__cdecl*)(void*, const uint8_t*, int, uint8_t*);
using P_JsonReadCompound    = void* (__cdecl*)(void*, const uint8_t*, int);
using P_JsonSerialize       = int   (__cdecl*)(void*, uint8_t*, int);
using P_JsonDestroy         = void  (__cdecl*)(void*);

struct DrxApi {
    HMODULE lib = nullptr;
    void**  table = nullptr; // 指向 nint 数组首址
    // 已解析的强类型函数指针
    P_Create              Create = nullptr;
    P_CreateWithAes       CreateWithAes = nullptr;
    P_Connect             Connect = nullptr;
    P_SetAesEncryptor     SetAesEncryptor = nullptr;
    P_SendPacket          SendPacket = nullptr;
    P_Disconnect          Disconnect = nullptr;
    P_Destroy             Destroy = nullptr;

    P_JsonCreateFromBytes JsonCreateFromBytes = nullptr;
    P_JsonCreate          JsonCreate = nullptr;
    P_JsonPushString      JsonPushString = nullptr;
    P_JsonPushNumber      JsonPushNumber = nullptr;
    P_JsonPushBoolean     JsonPushBoolean = nullptr;
    P_JsonPushCompound    JsonPushCompound = nullptr;
    P_JsonReadString      JsonReadString = nullptr;
    P_JsonReadBoolean     JsonReadBoolean = nullptr;
    P_JsonReadNumber      JsonReadNumber = nullptr;
    P_JsonReadCompound    JsonReadCompound = nullptr;
    P_JsonSerialize       JsonSerialize = nullptr;
    P_JsonDestroy         JsonDestroy = nullptr;

    bool load(const wchar_t* dllPath) {
        lib = ::LoadLibraryW(dllPath);
        if (!lib) return false;
        auto getTbl = (Fn_GetDrxTcpClientExports)::GetProcAddress(lib, "GetDrxTcpClientExports");
        if (!getTbl) return false;
        table = (void**)getTbl();
        if (!table) return false;

        // 按索引绑定
        Create              = (P_Create)             table[IDX_Create];
        CreateWithAes       = (P_CreateWithAes)      table[IDX_CreateWithAes];
        Connect             = (P_Connect)            table[IDX_Connect];
        SetAesEncryptor     = (P_SetAesEncryptor)    table[IDX_SetAesEncryptor];
        SendPacket          = (P_SendPacket)         table[IDX_SendPacket];
        Disconnect          = (P_Disconnect)         table[IDX_Disconnect];
        Destroy             = (P_Destroy)            table[IDX_Destroy];

        JsonCreateFromBytes = (P_JsonCreateFromBytes)table[IDX_JsonCreateFromBytes];
        JsonCreate          = (P_JsonCreate)         table[IDX_JsonCreate];
        JsonPushString      = (P_JsonPushString)     table[IDX_JsonPushString];
        JsonPushNumber      = (P_JsonPushNumber)     table[IDX_JsonPushNumber];
        JsonPushBoolean     = (P_JsonPushBoolean)    table[IDX_JsonPushBoolean];
        JsonPushCompound    = (P_JsonPushCompound)   table[IDX_JsonPushCompound];
        JsonReadString      = (P_JsonReadString)     table[IDX_JsonReadString];
        JsonReadBoolean     = (P_JsonReadBoolean)    table[IDX_JsonReadBoolean];
        JsonReadNumber      = (P_JsonReadNumber)     table[IDX_JsonReadNumber];
        JsonReadCompound    = (P_JsonReadCompound)   table[IDX_JsonReadCompound];
        JsonSerialize       = (P_JsonSerialize)      table[IDX_JsonSerialize];
        JsonDestroy         = (P_JsonDestroy)        table[IDX_JsonDestroy];

        return Create && Connect && SendPacket && Destroy && JsonCreate && JsonSerialize && JsonDestroy;
    }
    void unload() { if (lib) ::FreeLibrary(lib); lib = nullptr; table = nullptr; }
};
```

```cpp
// FullUsage_Demo.cpp
#include "DrxExportsTable.hpp"
#include <vector>
#include <string>
#include <iostream>
#include <cstring>

static void __cdecl OnResp(const uint8_t* data, int len) {
    std::cout << "Callback bytes(" << len << "): "
              << std::string((const char*)data, len) << std::endl;
}

int main() {
    DrxApi api;
    if (!api.load(L"Drx.Sdk.Network.dll")) { // 按实际产物名替换
        std::cerr << "Load API failed" << std::endl;
        return 1;
    }

    // 1. Create & 可选 AES
    void* client = api.Create();
    if (!client) return 2;

    // 可选：运行时设置/切换 AES
    const uint8_t key[16] = {0}; // 示例：16 字节
    const uint8_t iv [16] = {1};
    api.SetAesEncryptor(client, key, 16, iv, 16);

    // 或在创建时指定 AES
    // void* client = api.CreateWithAes(key, 16, iv, 16);

    // 2. Connect
    const char* host = "127.0.0.1";
    if (!api.Connect(client, (const uint8_t*)host, (int)std::strlen(host), 8080)) {
        std::cerr << "Connect failed" << std::endl;
        api.Destroy(client);
        api.unload();
        return 3;
    }

    // 3. JSON 构造（JsonCreate + Push...）
    void* root = api.JsonCreate();
    api.JsonPushString(root, (const uint8_t*)"cmd", 3, (const uint8_t*)"echo", 4);
    api.JsonPushNumber(root, (const uint8_t*)"seq", 3, 42.0);
    api.JsonPushBoolean(root, (const uint8_t*)"urgent", 6, 1);

    // 子对象
    void* meta = api.JsonCreate();
    api.JsonPushString(meta, (const uint8_t*)"trace", 5, (const uint8_t*)"abc-123", 7);
    api.JsonPushCompound(root, (const uint8_t*)"meta", 4, meta);
    // meta 作为子对象被挂入后，依然需要对 meta/根的句柄在不用时显式 JsonDestroy（由 GCHandle 托管；导出层不自动回收）
    // 此处统一在尾部销毁

    // 4. 读取示例（ReadString/ReadBoolean/ReadNumber/ReadCompound）
    {
        // 从文本直接创建对象（JsonCreateFromBytes）
        const char* text = "{\"ok\":true,\"count\":7,\"user\":{\"name\":\"neo\"}}";
        void* j = api.JsonCreateFromBytes((const uint8_t*)text, (int)std::strlen(text));

        // ReadString: 读取 user 对象序列化，或先 ReadCompound 再取 name
        std::vector<uint8_t> buf(256);
        int n = api.JsonReadString(j, (const uint8_t*)"user", 4, buf.data(), (int)buf.size());
        std::cout << "user json len=" << n << " data=" << std::string((char*)buf.data(), std::max(0,n)) << std::endl;

        uint8_t succ = 0;
        uint8_t ok = api.JsonReadBoolean(j, (const uint8_t*)"ok", 2, &succ);
        std::cout << "ok=" << (succ? (ok? "true":"false") : "N/A") << std::endl;

        succ = 0;
        double cnt = api.JsonReadNumber(j, (const uint8_t*)"count", 5, &succ);
        std::cout << "count=" << (succ? std::to_string(cnt): "N/A") << std::endl;

        void* user = api.JsonReadCompound(j, (const uint8_t*)"user", 4);
        if (user) {
            buf.assign(128, 0);
            int m = api.JsonReadString(user, (const uint8_t*)"name", 4, buf.data(), (int)buf.size());
            std::cout << "user.name=" << std::string((char*)buf.data(), std::max(0,m)) << std::endl;
            api.JsonDestroy(user);
        }
        api.JsonDestroy(j);
    }

    // 5. Serialize 并 SendPacket（携带回调）
    std::vector<uint8_t> out(1024);
    int w = api.JsonSerialize(root, out.data(), (int)out.size());
    bool sent = false;
    if (w > 0) {
        sent = api.SendPacket(client, out.data(), w, &OnResp, 5000);
    }
    if (!sent) std::cerr << "SendPacket failed" << std::endl;

    // 6. 清理 JSON
    api.JsonDestroy(meta);
    api.JsonDestroy(root);

    // 7. 断开与销毁
    api.Disconnect(client);
    api.Destroy(client);

    api.unload();
    return 0;
}
```

方式 A：逐一按名绑定并覆盖所有 JSON 读写
若目标环境不便于使用函数表，也可在原有按名绑定的基础上，补全读取 API（JsonReadString/JsonReadBoolean/JsonReadNumber/JsonReadCompound），调用签名与上节一致，此处不再赘述。

要点与陷阱清单
- UTF-8 长度：所有字符串参数传递时需提供准确字节长度；包含中文/emoji 时注意非 ASCII。
- ReadString 截断：返回值为写入字节数，若等于 outCapacity，可能被截断，应扩大缓冲区重试。
- JsonReadBoolean/Number 的 successPtr：非空时才能区分“值为假/0”与“键不存在/解析失败”。
- JsonReadCompound 生命周期：返回的新句柄独立存在，必须 JsonDestroy；与父对象销毁解耦。
- 回调线程安全：回调在托管侧触发，避免在回调里做耗时阻塞；必要时复制数据并投递到自有队列。
- 超时策略：SendPacket 的 timeoutMs 将用于 Wait 上限与内部取消令牌；建议合理设置并在上层补做重试。
- AES 参数：当前实现示例用 16 字节 key/iv（AES-128）；若为 24/32 字节，对应 AES-192/256，同样可用。

索引与托管实现对应关系
- 详见托管实现中的导出表构造与固定入口 [`GetDrxTcpClientExports()`](./DrxTcpClientExport.cs) 以及内部说明（0..6 为 TCP，7..18 为 JSON 系列）。

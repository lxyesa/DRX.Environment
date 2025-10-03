#include <windows.h>
#include <iostream>
#include <string>

// 定义回调函数类型，与C#中的委托匹配
typedef void(__stdcall* ConnectedCallback)(void* clientPtr, bool success);
typedef void(__stdcall* DisconnectedCallback)(void* clientPtr);
typedef void(__stdcall* DataReceivedCallback)(void* clientPtr, void* dataPtr, int length, void* remoteEndPointPtr);
typedef void(__stdcall* ErrorCallback)(void* clientPtr, void* messagePtr);
typedef void(__stdcall* TimeoutCallback)(void* clientPtr);

// 定义CLR函数指针类型
typedef void* (__stdcall* CreateInstanceFunc)(void* remoteEndPointPtr, int protocolType);
typedef void (__stdcall* DisposeFunc)(void* clientPtr);
typedef void (__stdcall* ConnectAsyncFunc)(void* clientPtr, void* callbackPtr);
typedef void (__stdcall* SendAsyncFunc)(void* clientPtr, void* dataPtr, int length, void* remoteEndPointPtr);
typedef void (__stdcall* RegisterConnectedCallbackFunc)(void* clientPtr, void* callbackPtr);
typedef void (__stdcall* RegisterDisconnectedCallbackFunc)(void* clientPtr, void* callbackPtr);
typedef void (__stdcall* RegisterDataReceivedCallbackFunc)(void* clientPtr, void* callbackPtr);
typedef void (__stdcall* RegisterErrorCallbackFunc)(void* clientPtr, void* callbackPtr);
typedef void (__stdcall* RegisterTimeoutCallbackFunc)(void* clientPtr, void* callbackPtr);

// 全局回调函数实现
void __stdcall OnConnected(void* clientPtr, bool success) {
    std::cout << "[回调] 连接" << (success ? "成功" : "失败") << std::endl;
}

void __stdcall OnDisconnected(void* clientPtr) {
    std::cout << "[回调] 连接断开" << std::endl;
}

void __stdcall OnDataReceived(void* clientPtr, void* dataPtr, int length, void* remoteEndPointPtr) {
    if (dataPtr && length > 0) {
        std::string data((char*)dataPtr, length);
        std::cout << "[回调] 收到数据: " << data << std::endl;
    }

    // 释放非托管内存（由C#分配）
    if (dataPtr) CoTaskMemFree(dataPtr);
    if (remoteEndPointPtr) CoTaskMemFree(remoteEndPointPtr);
}

void __stdcall OnError(void* clientPtr, void* messagePtr) {
    if (messagePtr) {
        std::string message((char*)messagePtr);
        std::cout << "[回调] 错误: " << message << std::endl;
        CoTaskMemFree(messagePtr);
    }
}

void __stdcall OnTimeout(void* clientPtr) {
    std::cout << "[回调] 连接超时" << std::endl;
}

int main() {
    // 加载包含NetworkClientCLR的DLL
    // 注意：需要根据实际的DLL名称和路径进行调整
    HMODULE hModule = LoadLibraryA("Drx.Sdk.Network.dll");
    if (!hModule) {
        std::cerr << "无法加载DLL: " << GetLastError() << std::endl;
        return 1;
    }

    // 获取函数指针
    auto createInstance = (CreateInstanceFunc)GetProcAddress(hModule, "CreateInstance");
    auto dispose = (DisposeFunc)GetProcAddress(hModule, "Dispose");
    auto connectAsync = (ConnectAsyncFunc)GetProcAddress(hModule, "ConnectAsync");
    auto sendAsync = (SendAsyncFunc)GetProcAddress(hModule, "SendAsync");
    auto registerConnected = (RegisterConnectedCallbackFunc)GetProcAddress(hModule, "RegisterConnectedCallback");
    auto registerDisconnected = (RegisterDisconnectedCallbackFunc)GetProcAddress(hModule, "RegisterDisconnectedCallback");
    auto registerDataReceived = (RegisterDataReceivedCallbackFunc)GetProcAddress(hModule, "RegisterDataReceivedCallback");
    auto registerError = (RegisterErrorCallbackFunc)GetProcAddress(hModule, "RegisterErrorCallback");
    auto registerTimeout = (RegisterTimeoutCallbackFunc)GetProcAddress(hModule, "RegisterTimeoutCallback");

    if (!createInstance || !dispose || !connectAsync) {
        std::cerr << "无法获取函数指针" << std::endl;
        FreeLibrary(hModule);
        return 1;
    }

    try {
        // 创建远程端点字符串
        const char* remoteEndPoint = "127.0.0.1:1234";
        void* remoteEndPointPtr = CoTaskMemAlloc(strlen(remoteEndPoint) + 1);
        strcpy_s((char*)remoteEndPointPtr, strlen(remoteEndPoint) + 1, remoteEndPoint);

        // 创建NetworkClient实例 (TCP = 1, UDP = 2)
        void* clientPtr = createInstance(remoteEndPointPtr, 1); // TCP
        if (!clientPtr) {
            std::cerr << "创建NetworkClient失败" << std::endl;
            CoTaskMemFree(remoteEndPointPtr);
            FreeLibrary(hModule);
            return 1;
        }

        CoTaskMemFree(remoteEndPointPtr);

        // 注册回调函数
        registerConnected(clientPtr, (void*)&OnConnected);
        registerDisconnected(clientPtr, (void*)&OnDisconnected);
        registerDataReceived(clientPtr, (void*)&OnDataReceived);
        registerError(clientPtr, (void*)&OnError);
        registerTimeout(clientPtr, (void*)&OnTimeout);

        std::cout << "正在异步连接到 " << remoteEndPoint << "..." << std::endl;

        // 异步连接
        connectAsync(clientPtr, (void*)&OnConnected);

        // 等待连接完成（实际应用中应该使用事件循环或线程等待）
        Sleep(2000); // 简单等待2秒

        // 发送测试数据
        const char* testData = "Hello from C++!";
        void* dataPtr = CoTaskMemAlloc(strlen(testData) + 1);
        strcpy_s((char*)dataPtr, strlen(testData) + 1, testData);

        std::cout << "发送数据: " << testData << std::endl;
        sendAsync(clientPtr, dataPtr, strlen(testData), nullptr);

        CoTaskMemFree(dataPtr);

        // 等待更多操作...
        Sleep(5000); // 等待5秒

        // 清理资源
        dispose(clientPtr);

    } catch (const std::exception& e) {
        std::cerr << "异常: " << e.what() << std::endl;
    }

    // 卸载DLL
    FreeLibrary(hModule);

    std::cout << "程序结束" << std::endl;
    return 0;
}
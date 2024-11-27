# 公共网络 API 使用说明
- 本文档是基于C++/CLR版本构建的，首先你需要了解怎么启用C++/CLR。
- 本库文件只提供内部使用。
## 介绍
1. 命名空间:
   - NetworkCommonLibrary 这是最基本的命名空间。
   - NetworkCommonLibrary.Models 这个命名空间中用来存放数据模型，如数据包、结构体、响应包。
   - NetworkCommonLibrary.EventHandlers 这个命名空间中存在各类事件的Handler，你可以选择继承它们并实现自己的事件执行逻辑。
   - NetworkCommonLibrary.Builder 这个命名空间中存在各种构建器，请合理的使用它们，尤其是在构建网络包请求体的时候异常好用。
   - 除此之外，没有特别需要声明的命名空间，上面的命名空间是已有的。
2. 抽象类:
   - 任何在**NetworkCommonLibrary.EventHandlers**命名空间中的类都是抽象类，这些类需要你自己实现执行方法。
## API 文档
| 函数名 | 参数 | 说明 | 返回值 |
|-------|-----|-------|--------|
| NetworkUtils(类) | NetworkClientEventHandler^ event_handler, int heart_delay | 初始化网络工具，为下一步做准备，参数需要一个事件处理类，心跳包频率 | void |
| ConnectAsync | String^ ip, int port | 异步连接服务器 | Task<void>(可以是作为void) |

使用案例：
```cpp
#include <msclr/marshal_cppstd.h>
#include <iostream>
#using "xxxxx\NetworkCommonLibrary.dll"   // 根据你的库路径决定

using namespace NetworkCommonLibrary;   // 必须
using namespace NetworkCommonLibrary::EventHandlers;   // 必须

NetworkUtils^ networkUtils = gcnew NetworkUtils(eventHandler, heart_delay);   // 创建一个网络工具实例，使用gcnew托管（gc是一个前缀，你可以将其视作为托管给.NET的new，不用（也不能）手动管理内存，而是.NET为你管理）
networkUtils->ConnectAsync("0.0.0.0",114514);   // 尝试连接服务器。
// 如果你需要等待服务器连接后(无论失败与否，当服务器连接失败或成功后都会结束Wait)执行下一步，你可以使用以下写法：
networkUtils->ConnectAsync("0.0.0.0",114514)->Wait();
```
以上为初级网络工具的使用方法。
-------------------------------

### 事件监听器
1. NetworkClientEventHandler
| 继承方需要实现的函数 | 参数 | 触发条件 |
|-|-|-|
| OnPacketReceived | TcpClient^ client, NetworkPacket^ packet | 当客户端接收到数据包时触发 |
| OnConnected | TcpClient^ client | 当客户端连接时触发 |
| OnHeartbeatSent | TcpClient^ client, NetworkPacket^ heartbeatPacket | 给服务器发送心跳包时触发 |
| OnDisconnected | TcpClient^ client, string^ reason | 变成卡逼时触发（与服务器断开连接时触发） |
| OnPacketSent | TcpClient^ client, NetworkPacket^ packet | 当数据包被发送时触发 |

使用案例：
```cpp
#include <msclr/marshal_cppstd.h>

using namespace System;
using namespace System::Net::Sockets;
using namespace NetworkCommonLibrary;
using namespace NetworkCommonLibrary::Models;
using namespace NetworkCommonLibrary::EventHandlers;

ref class MyHandler : public NetworkClientEventHandler   // 继承自 NetworkClientEventHandler 抽象类
{
public:
    virtual void OnPacketReceived(TcpClient^ client, NetworkPacket^ packet) override
    {
        // 接收到数据包时触发
        Console::WriteLine("Packet received.");
    }

    virtual void OnConnected(TcpClient^ client) override
    {
        // 连接至服务器时触发
        Console::WriteLine("Connected to server.");
    }

    virtual void OnHeartbeatSent(TcpClient^ client, NetworkPacket^ heartbeatPacket) override
    {
        // 心跳包发送时触发 
        Console::WriteLine("Heartbeat sent.");
    }

    virtual void OnDisconnected(TcpClient^ client, String^ reason) override
    {
        // 与服务器断开连接时触发
        Console::WriteLine("Disconnected from server.");
    }

    virtual void OnPacketSent(TcpClient^ client, NetworkPacket^ packet) override
    {
        // 数据包被发送时触发
        Console::WriteLine("Packet sent.");
    }
};
```
------------------------

#### 当你准备好一个 NetworkClientEventHandler 的子类实现后，你需要在初始化 `NetworkUtils` 时在构造函数中传入该Handler
```cpp
// 创建网络事件Handler实例
MyHandler^ eventHandler = gcnew MyHandler();

// 创建NetworkUtils实例，并传入 eventHandler
NetworkUtils^ networkUtils = gcnew NetworkUtils(eventHandler, heart_delay);
// 连接服务器进行测试
networkUtils->ConnectAsync("0.0.0.0",114514);
```























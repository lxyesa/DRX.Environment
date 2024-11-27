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
// 如果你需要等待服务器连接后执行下一步，你可以使用以下写法：
networkUtils->ConnectAsync("0.0.0.0",114514)->Wait();

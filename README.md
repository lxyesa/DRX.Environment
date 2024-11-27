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
--------------------------------
| NetworkUtils | NetworkClientEventHandler^ event_handler, int heart_delay | 初始化网络工具，为下一步做准备，参数需要一个事件处理类，心跳包频率 | void |

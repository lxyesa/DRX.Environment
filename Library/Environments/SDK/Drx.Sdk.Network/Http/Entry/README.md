# Entry 目录 - 路由和中间件入口点

## 概述
Entry 目录定义了 HTTP 框架中各种入口点的对象模型，包括路由条目、中间件条目等，用于内部表示和缓存已注册的处理器和中间件。

## 文件说明

### RouteEntry.cs
**路由条目**
- 表示一条已注册的路由规则
- 存储路由匹配所需的元数据
- 特点：
  - 方法信息（Method、Path）
  - 处理器委托
  - 参数提取规则
  - 缓存优化

**主要属性：**
- `Method` - HTTP 方法 (GET, POST, 等)
- `Path` - 路由路径 (可包含动态参数)
- `Handler` - 处理委托
- `Parameters` - 动态参数列表
- `Priority` - 路由优先级（用于排序）
- `Compiled` - 编译的匹配函数（性能优化）

### MiddlewareEntry.cs
**中间件条目**
- 表示一个已注册的中间件
- 存储中间件执行所需的元数据
- 特点：
  - 中间件方法信息
  - 执行顺序
  - 条件检查
  - 异步支持

**主要属性：**
- `Name` - 中间件名称
- `Method` - 中间件委托
- `Order` - 执行顺序
- `Condition` - 执行条件（可选）
- `IsAsync` - 是否异步

### TickerEntry.cs
**定时器条目**
- 表示一个后台定时任务
- 用于后台处理和清理

### TickerRegistration.cs
**定时器注册**
- 定时器的注册和管理
- 支持周期性任务

### CommandQueueEntry.cs
**命令队列条目**
- 表示一个待处理的内置命令
- 用于内置命令队列管理

## 内部使用场景

1. **路由缓存** - RouteEntry 被缓存以加速路由匹配
2. **中间件链** - MiddlewareEntry 形成中间件链表
3. **性能优化** - 编译和缓存提高查询速度
4. **后台任务** - TickerEntry 管理后台清理任务
5. **内置命令** - CommandQueueEntry 处理内置命令

## 与其他模块的关系

- **与 Configs 的关系** - 从 HttpHandleAttribute 等创建 Entry
- **与 Server 的关系** - 服务器维护 Entry 的注册表
- **与 Performance 的关系** - RouteMatchCache 缓存路由条目

## 架构模式

这些 Entry 类使用了**策略模式**和**注册表模式**：

```
Attribute 标记
    ↓
自动扫描
    ↓
创建 Entry 对象
    ↓
存储在注册表
    ↓
请求处理时快速查询
    ↓
执行对应的处理器/中间件
```

## 性能考虑

1. **缓存** - Entry 被缓存避免重复创建
2. **编译** - 路由匹配函数被编译为委托
3. **索引** - 快速查询数据结构
4. **池化** - 对象池复用 Entry

## 相关文档
- 参见 [../Server/README.md](../Server/README.md) 了解如何使用 Entry
- 参见 [../Configs/README.md](../Configs/README.md) 了解属性配置
- 参见 [../Performance/README.md](../Performance/README.md) 了解缓存机制

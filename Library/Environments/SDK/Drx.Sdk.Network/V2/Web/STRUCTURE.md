# Web 目录结构说明

Web 目录已按功能划分为多个子目录，便于代码组织和维护。

## 目录结构

### 📁 Core/ - 核心服务器和客户端
核心的HTTP服务器和客户端实现
```
Core/
├── DrxHttpServer.cs      # HTTP服务器主类
├── DrxHttpClient.cs      # HTTP客户端实现
└── LLMHttpClient.cs      # LLM集成的HTTP客户端
```

### 📁 Http/ - HTTP基础设施
HTTP请求、响应、头部等基础设施类
```
Http/
├── HttpRequest.cs        # HTTP请求封装
├── HttpResponse.cs       # HTTP响应封装
├── HttpHeaders.cs        # HTTP头部管理
└── HttpActionResults.cs  # Action执行结果处理
```

### 📁 Serialization/ - 序列化相关
JSON序列化和反序列化相关实现
```
Serialization/
└── DrxJsonSerializer.cs  # 自定义JSON序列化器
```

### 📁 Auth/ - 认证和安全
认证、授权和安全相关的类
```
Auth/
├── JwtHelper.cs          # JWT令牌处理
└── TokenBucket.cs        # 令牌桶速率限制实现
```

### 📁 Performance/ - 性能优化
缓存、路由优化、消息队列等性能优化相关
```
Performance/
├── RouteMatchCache.cs    # 路由匹配缓存
├── HttpObjectPool.cs     # HTTP对象池管理
├── MessageQueue.cs       # 消息队列实现
└── ThreadPoolManager.cs  # 线程池管理
```

### 📁 Utilities/ - 工具类
通用工具和辅助类
```
Utilities/
├── DrxUrlHelper.cs           # URL处理工具
├── DrxClientHelper.cs        # 客户端辅助工具
└── DataPersistentManager.cs  # 数据持久化管理
```

### 📁 Guides/ - 文档和指南
开发指南、实现文档和使用说明
```
Guides/
├── DrxHttpServer.DEVGUIDE.md         # HTTP服务器开发指南
├── DrxHttpClient.DEVGUIDE.md         # HTTP客户端开发指南
├── LLMHttpClient.DEVGUIDE.md         # LLM客户端开发指南
├── DrxUrlHelper.DEVGUIDE.md          # URL工具开发指南
├── HttpRequest.DEVGUIDE.md           # HTTP请求开发指南
├── HttpResponse.DEVGUIDE.md          # HTTP响应开发指南
├── HttpHeaders.DEVGUIDE.md           # HTTP头部开发指南
├── JwtHelper.DEVGUIDE.md             # JWT工具开发指南
├── JSON_SERIALIZATION_GUIDE.md       # JSON序列化指南
├── RateLimitCallback.IMPLEMENTATION.md  # 速率限制回调实现指南
└── SessionSystem.md                  # 会话系统说明
```

### 📁 Configs/ - 配置类
（现有目录）配置相关的类和常量
```
Configs/
├── AuthorizationRecord.cs    # 授权记录
├── CommandParseResult.cs     # 命令解析结果
├── CookieOptions.cs          # Cookie选项
├── HttpHandleAttribute.cs    # HTTP处理特性
├── HttpMiddlewareAttribute.cs # HTTP中间件特性
├── OverrideContext.cs        # 覆盖上下文
└── Session.cs                # 会话配置
```

### 📁 Models/ - 数据模型
（现有目录）数据模型和实体类
```
Models/
├── DataModelBase.cs          # 数据模型基类
└── DataModels.cs             # 具体数据模型
```

### 📁 Results/ - 操作结果
（现有目录）各种操作结果的包装类
```
Results/
└── BytesResult.cs            # 字节内容结果
```

## 使用建议

1. **导入命名空间时**：根据类的所在目录选择合适的命名空间前缀
   ```csharp
   using Drx.Sdk.Network.V2.Web.Core;
   using Drx.Sdk.Network.V2.Web.Http;
   using Drx.Sdk.Network.V2.Web.Auth;
   ```

2. **新增类时**：根据功能分类放到相应的目录
   - 新的HTTP处理器 → Http/
   - 新的缓存机制 → Performance/
   - 新的安全功能 → Auth/
   - 新的工具方法 → Utilities/

3. **文档维护**：
   - 新增功能的使用说明放在 Guides/ 目录
   - 保持相关代码文件与其DEVGUIDE.md同步

## 分类原则

| 分类 | 原则 |
|------|------|
| Core | 核心服务和客户端主类 |
| Http | HTTP协议相关的基础类 |
| Serialization | 数据序列化相关 |
| Auth | 认证、授权和安全机制 |
| Performance | 性能优化、缓存、队列 |
| Utilities | 通用工具和辅助方法 |
| Configs | 配置对象和常量 |
| Models | 数据模型和实体类 |
| Results | 操作结果封装 |
| Guides | 文档和开发指南 |

# Protocol 目录 - HTTP 协议核心定义

## 概述
Protocol 目录定义了 DRX 框架中 HTTP 请求和响应的核心结构，是整个 HTTP 系统的基础。

## 文件说明

### HttpRequest.cs
**HTTP 请求对象**
- 表示客户端发送的 HTTP 请求
- 支持完整的 HTTP 请求语义
- 特点：
  - 方法、路径、URL 等标准字段
  - 查询参数、请求头、Cookie 支持
  - 多种请求体形式（字符串、字节、流、对象）
  - 文件上传描述符支持
  - 会话信息集成

**主要属性：**
- `Method` - HTTP 方法 (GET, POST, PUT, DELETE, 等)
- `Path` - 请求路径
- `Url` - 完整 URL（可选）
- `Query` - 查询参数集合（NameValueCollection）
- `Headers` - 请求头集合
- `Cookies` - Cookie 集合
- `Body` - 请求体（字符串）
- `BodyBytes` - 请求体（字节）
- `BodyObject` - 请求体（对象）
- `BodyStream` - 请求体（流）
- `SessionId` - 会话 ID
- `RemoteAddress` - 远程地址
- `RemotePort` - 远程端口

**嵌套类型：**
- `UploadFileDescriptor` - 文件上传描述符
- `FormData` - 表单数据容器

### HttpResponse.cs
**HTTP 响应对象**
- 表示服务器返回的 HTTP 响应
- 支持多种内容格式
- 特点：
  - 状态码和状态描述
  - 可扩展的动态内容对象
  - 自动 JSON 反序列化
  - 会话 ID 传回支持

**主要属性：**
- `StatusCode` - HTTP 状态码 (200, 404, 500, 等)
- `StatusDescription` - 状态描述（例如 "OK", "Not Found"）
- `Headers` - 响应头集合
- `Body` - 响应体（字符串）
- `BodyBytes` - 响应体（字节）
- `Content` - 动态内容对象 (ExpandoObject)
  - `Content.Text` - 文本内容
  - `Content.Json` - JSON 对象
  - `Content.Object` - 反序列化对象
  - `Content.XYZ` - 自定义扩展字段
- `SessionId` - 会话 ID（可选）

### HttpHeaders.cs
**HTTP 请求头管理**
- 常见的 HTTP 请求头常量定义
- 请求头工具方法
- 与 Microsoft.Net.Http.Headers 的集成

### HttpActionResults.cs
**HTTP 操作结果类**
- 各种特殊的 HTTP 响应类型封装
- 包括 OK、BadRequest、NotFound 等结果类型

## 核心数据流

```
请求流程：
HttpRequest (客户端创建) 
  ↓
发送到服务器
  ↓
服务器处理
  ↓
生成 HttpResponse
  ↓
返回给客户端

响应内容访问：
HttpResponse.Body (字符串)
或
HttpResponse.Content.Json (对象)
或
HttpResponse.Content.XYZ (动态字段)
```

## 使用场景

1. **HTTP 通信** - 客户端和服务器之间的通信
2. **API 定义** - RESTful API 的请求/响应定义
3. **中间件处理** - 拦截和修改请求/响应
4. **路由匹配** - 根据请求信息匹配处理规则
5. **日志记录** - 记录完整的请求/响应信息

## 与其他模块的关系

- **与 Server 的关系** - DrxHttpServer 使用这些协议类
- **与 Client 的关系** - DrxHttpClient 创建和处理这些对象
- **与 Serialization 的关系** - JSON 序列化/反序列化
- **与 Session 的关系** - SessionId 字段支持会话
- **与 Configs 的关系** - 配置相关的元数据存储
- **与 Results 的关系** - HttpActionResults 基于这些协议

## 最佳实践

1. **安全** - 验证请求数据，防止注入攻击
2. **性能** - 避免不必要的数据复制，使用流处理大文件
3. **兼容性** - 使用标准的 HTTP 状态码
4. **可扩展性** - 使用动态 Content 字段扩展响应信息
5. **文档** - 为 API 中英文注释，便于理解

## 相关文档
- 参见 [../Guides/HttpRequest.DEVGUIDE.md](../Guides/HttpRequest.DEVGUIDE.md)
- 参见 [../Guides/HttpResponse.DEVGUIDE.md](../Guides/HttpResponse.DEVGUIDE.md)

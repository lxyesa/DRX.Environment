# Asp 目录 - ASP.NET Core 集成

## 概述
Asp 目录提供了 ASP.NET Core 框架的轻量级集成封装，使开发者能够快速启动 HTTP 服务或与现有的 ASP.NET Core 应用集成。

## 文件说明

### DrxHttpAspClient.cs
**轻量的 HTTP 客户端封装**
- 基于 `System.Net.Http.HttpClient` 的简化包装
- 快速向 ASP.NET Core 服务器发送请求
- 支持 GET、POST、PUT、DELETE 等标准 HTTP 方法
- 支持 JSON 序列化的请求和响应
- 支持默认请求头设置
- 特点：
  - 简洁的 API 设计
  - 默认基地址支持
  - 异步操作支持

**主要方法：**
- `GetStringAsync(path)` - 发送 GET 请求返回字符串
- `PostStringAsync(path, json)` - 发送 JSON POST 请求
- `PutStringAsync()` - 发送 PUT 请求
- `DeleteAsync()` - 发送 DELETE 请求
- `SetDefaultHeader()` - 设置默认请求头

### DrxHttpAspServer.cs
**轻量的 ASP.NET Core 服务器启动封装**
- 封装 WebApplication 的启动/停止流程
- 支持自定义路由和中间件注册
- 支持框架层请求处理委托
- 特点：
  - 快速启动 ASP.NET Core HTTP 服务
  - 灵活的配置方式
  - 可选日志工厂集成
  - 支持自定义请求处理

**主要功能：**
- `Start()` - 启动服务器
- `Stop()` - 停止服务器
- `IsRunning` - 表示服务器是否正在运行

## 使用场景

1. **快速服务开发** - 使用 DrxHttpAspServer 快速启动 HTTP 服务
2. **客户端通信** - 使用 DrxHttpAspClient 与远程服务通信
3. **框架集成** - 将 ASP.NET Core 与 DRX 框架集成
4. **轻量级 API** - 创建简单的 RESTful API

## 与其他模块的关系

- **与 Protocol 的关系** - 使用 `HttpRequest`、`HttpResponse` 进行通信
- **与 Auth 的关系** - 后续可集成 JWT 认证
- **与 Server 的关系** - DrxHttpServer 提供更高层的功能

## 相关文档
- 参见 [Guides/ 目录](../Guides/)了解更多开发指南

# Guides 目录 - 开发指南索引

## 概述
Guides 目录包含详细的开发指南文档，为 Http 模块的各个组件提供了深入的使用说明和最佳实践。

## 现有指南

### 客户端指南

#### [DrxHttpClient.DEVGUIDE.md](DrxHttpClient.DEVGUIDE.md)
**HTTP 客户端完整指南**
- 客户端的详细功能说明
- 输入/输出契约
- 公开 API 总结
- 使用示例和模式
- 性能优化建议

**主要内容：**
- 基本用法（GET、POST、PUT、DELETE）
- 文件上传和下载
- 会话和 Cookie 管理
- 进度跟踪
- 异常处理

#### [LLMHttpClient.DEVGUIDE.md](LLMHttpClient.DEVGUIDE.md)
**LLM 客户端专用指南**
- 大语言模型 API 的客户端使用
- 流式响应处理
- Token 计数
- 速率限制处理

### 服务器指南

#### [DrxHttpServer.DEVGUIDE.md](DrxHttpServer.DEVGUIDE.md)
**HTTP 服务器完整指南**
- 服务器架构和生命周期
- 路由定义和匹配
- 中间件开发
- 认证和授权
- 处理器编写
- 异常处理

**主要内容：**
- Partial class 组织模式
- HttpHandleAttribute 使用
- HttpMiddlewareAttribute 使用
- 自定义结果类型
- Error handling 最佳实践

#### [ROUTING_GUIDE.md](ROUTING_GUIDE.md) 🆕
**路由系统完整指南**
- 三种路由模式详解
- 原始路由（Raw Route）
- 流式上传路由
- 标准路由（REST API）
- 路由匹配流程
- 性能对比和选择指南

#### [MIDDLEWARE_GUIDE.md](MIDDLEWARE_GUIDE.md) 🆕
**中间件系统完整指南**
- 三种中间件类型详解
- 原始上下文中间件
- 请求级中间件
- 属性标记中间件
- 优先级系统
- 常见中间件模板
- 故障排查指南

#### [RateLimitCallback.IMPLEMENTATION.md](RateLimitCallback.IMPLEMENTATION.md)
**速率限制实现指南**
- 速率限制的配置
- 回调机制
- 自定义限流规则
- 性能优化

### 协议指南

#### [HttpRequest.DEVGUIDE.md](HttpRequest.DEVGUIDE.md)
**HTTP 请求对象详解**
- HttpRequest 的完整属性说明
- 请求头处理
- 查询参数提取
- 文件上传处理
- 请求体解析

#### [HttpResponse.DEVGUIDE.md](HttpResponse.DEVGUIDE.md)
**HTTP 响应对象详解**
- HttpResponse 的完整属性说明
- 状态码最佳实践
- 响应头设置
- 动态内容对象
- 会话 ID 管理

#### [HttpHeaders.DEVGUIDE.md](HttpHeaders.DEVGUIDE.md)
**HTTP 请求头完整指南**
- 常见 HTTP 请求头
- 头信息最佳实践
- 安全相关的请求头
- 性能优化请求头
- 跨域 (CORS) 配置

### 功能模块指南

#### [JwtHelper.DEVGUIDE.md](JwtHelper.DEVGUIDE.md)
**JWT 认证完整指南**
- JWT 基础概念
- 令牌生成和验证
- 声明（Claims）管理
- 令牌刷新策略
- 令牌撤销机制
- 安全最佳实践

#### [SessionSystem.md](SessionSystem.md)
**会话管理系统详解**
- 会话的创建和管理
- Cookie 与会话的关系
- 会话存储选项
- 会话安全
- 分布式会话处理

#### [DrxUrlHelper.DEVGUIDE.md](DrxUrlHelper.DEVGUIDE.md)
**URL 处理工具完整指南**
- URL 解析和构建
- 路径处理
- 查询参数处理
- 特殊字符编码
- URL 验证

#### [JSON_SERIALIZATION_GUIDE.md](JSON_SERIALIZATION_GUIDE.md)
**JSON 序列化指南**
- 对象序列化
- JSON 反序列化
- 自定义序列化规则
- 性能优化
- 兼容性处理

### 更新说明

#### [UPDATE_NOTES.md](UPDATE_NOTES.md) 🆕
**模块更新说明**
- 最新更新内容总结
- 新增 API 文档化
- 性能优化详情
- 快速开始指南
- 文档导航

## 📚 使用指南

### ⚡ 快速开始路径

#### 5 分钟快速入门
1. 阅读本文档的[快速开始](#快速开始路径)部分
2. 查看对应模块的快速示例
3. 运行示例代码

#### 30 分钟基础学习
1. **服务器开发** → [DrxHttpServer.DEVGUIDE.md](DrxHttpServer.DEVGUIDE.md)
   - 路由、中间件、处理器基础
   
2. **客户端开发** → [DrxHttpClient.DEVGUIDE.md](DrxHttpClient.DEVGUIDE.md)
   - 请求发送、文件操作、会话管理

#### 2 小时深入学习
1. [HttpRequest.DEVGUIDE.md](HttpRequest.DEVGUIDE.md) - 请求对象详解
2. [HttpResponse.DEVGUIDE.md](HttpResponse.DEVGUIDE.md) - 响应对象详解
3. [JwtHelper.DEVGUIDE.md](JwtHelper.DEVGUIDE.md) - 认证系统
4. [SessionSystem.md](SessionSystem.md) - 会话管理

#### 4 小时完整掌握
- 全部指南 + 源代码分析
- 性能优化技巧
- 高级功能实现

### 按开发角色查找

#### 🛠️ 后端开发
必读：
- [DrxHttpServer.DEVGUIDE.md](DrxHttpServer.DEVGUIDE.md) - 核心服务器
- [JwtHelper.DEVGUIDE.md](JwtHelper.DEVGUIDE.md) - 安全认证
- [SessionSystem.md](SessionSystem.md) - 用户会话

推荐：
- [HttpRequest.DEVGUIDE.md](HttpRequest.DEVGUIDE.md) - 请求处理
- [JSON_SERIALIZATION_GUIDE.md](JSON_SERIALIZATION_GUIDE.md) - 数据序列化

#### 🔌 集成开发
必读：
- [DrxHttpClient.DEVGUIDE.md](DrxHttpClient.DEVGUIDE.md) - HTTP 客户端
- [DrxUrlHelper.DEVGUIDE.md](DrxUrlHelper.DEVGUIDE.md) - URL 处理

推荐：
- [HttpHeaders.DEVGUIDE.md](HttpHeaders.DEVGUIDE.md) - 头处理
- [JSON_SERIALIZATION_GUIDE.md](JSON_SERIALIZATION_GUIDE.md) - 数据解析

#### 🛡️ 安全工程师
必读：
- [JwtHelper.DEVGUIDE.md](JwtHelper.DEVGUIDE.md) - JWT 认证
- [RateLimitCallback.IMPLEMENTATION.md](RateLimitCallback.IMPLEMENTATION.md) - 速率限制
- [SessionSystem.md](SessionSystem.md) - 会话安全

#### ⚡ 性能优化
必读：
- [DrxHttpServer.DEVGUIDE.md](DrxHttpServer.DEVGUIDE.md#性能优化) - 性能章节
- [DrxHttpClient.DEVGUIDE.md](DrxHttpClient.DEVGUIDE.md#性能建议) - 客户端优化
- 性能模块相关文档

### 按任务查找
- **Client 开发** → DrxHttpClient, LLMHttpClient 指南
- **Server 开发** → DrxHttpServer, RateLimitCallback 指南
- **认证授权** → JwtHelper, SessionSystem 指南
- **数据处理** → HttpRequest, HttpResponse, JSON_SERIALIZATION 指南
- **工具方法** → DrxUrlHelper, HttpHeaders 指南

### 🆕 最新更新 (2026 年 3 月)
参见 [UPDATE_NOTES.md](UPDATE_NOTES.md) 了解：
- 三种路由模式的完整对比
- 中间件优先级系统详解
- 并发控制机制
- 性能优化建议
- 新增 API 文档化

## 文档约定

### 代码示例格式
```csharp
// 生产级别代码示例
[HttpHandle("GET", "/api/users/{id}")]
public static async Task<HttpResponse> GetUser(HttpRequest request)
{
    var id = request.Path.Parameters["id"];
    // 实现代码...
}
```

### 表格格式
| 列 1 | 列 2 | 列 3 |
|-----|-----|-----|
| 值 1 | 值 2 | 值 3 |

### 注意事项标记
- ⚠️ **警告** - 重要的注意事项
- 💡 **提示** - 最佳实践和建议
- 🔒 **安全** - 安全相关的考虑
- ⚡ **性能** - 性能相关的优化

## 相关资源

### 外部文档
- [ASP.NET Core 官方文档](https://docs.microsoft.com/aspnet/core)
- [HTTP 状态码](https://developer.mozilla.org/en-US/docs/Web/HTTP/Status)
- [JWT 介绍](https://jwt.io/introduction)
- [RFC 7231 - HTTP 语义](https://tools.ietf.org/html/rfc7231)

### 内部关联
- 参见 [../](../) 了解其他模块
- 参见各子目录的 README.md 了解模块概览

## 贡献指南

### 添加新指南
1. 创建新的 `{Component}.DEVGUIDE.md` 文件
2. 遵循现有指南的格式
3. 包含概述、API、示例和最佳实践
4. 添加外部链接和相关参考
5. 在此索引中添加条目

### 更新现有指南
- 确保与代码的最新版本同步
- 修正错误或过时信息
- 添加新功能的说明
- 改进示例代码的清晰度

## 质量标准

每份指南应包含：
- ✅ 清晰的概述
- ✅ 完整的 API 文档
- ✅ 实际的代码示例
- ✅ 常见问题解答
- ✅ 最佳实践建议
- ✅ 异常和错误处理
- ✅ 相关链接和参考

---

**最后更新**：2026 年 3 月
**维护者**：DRX 框架团队

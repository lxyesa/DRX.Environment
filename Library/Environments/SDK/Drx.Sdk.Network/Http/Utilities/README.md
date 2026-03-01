# Utilities 目录 - 工具方法

## 概述
Utilities 目录提供了 HTTP 框架中的各种通用工具方法和辅助函数。

## 主要功能分类

### URL 和路径处理
- URL 编码和解码
- 路径解析和拼接
- 查询参数提取
- URI 规范化

### 请求头处理
- 请求头解析
- 内容协商
- 压缩支持检测
- 缓存头处理

### MIME 类型
- 文件扩展名到 MIME 类型映射
- MIME 类型检测
- 常见类型常量

### 数据转换
- 字节数组转换
- 字符编码
- 十六进制转换
- Base64 编码/解码

### 验证和检查
- URL 格式验证
- IP 地址验证
- 数据有效性检查
- 安全检查

### 日志和调试
- 请求日志
- 响应日志
- 性能统计
- 错误诊断

## 常用工具函数

### URL 工具
```csharp
DrxUrlHelper.BuildUrl(baseUrl, path, query)
DrxUrlHelper.ParseUrl(url)
DrxUrlHelper.EncodeQueryString(parameters)
DrxUrlHelper.GetFileName(url)
```

### HTTP 头工具
```csharp
HttpHeaders.GetContentType(fileName)
HttpHeaders.GetCharset(contentType)
HttpHeaders.ParseCacheControl(headerValue)
HttpHeaders.IsCorsAllowed(origin, allowedOrigins)
```

### 编码工具
```csharp
HttpUtilities.EncodeBase64(data)
HttpUtilities.DecodeBase64(data)
HttpUtilities.UrlEncode(text)
HttpUtilities.UrlDecode(text)
```

### 验证工具
```csharp
HttpUtilities.IsValidUrl(url)
HttpUtilities.IsValidIpAddress(ipAddress)
HttpUtilities.IsValidEmail(email)
HttpUtilities.IsSafeFileName(fileName)
```

## 使用场景

1. **URL 处理** - 解析和构建 URL
2. **内容协商** - 确定响应格式
3. **安全** - 验证和清理输入
4. **编码** - 处理各种格式数据
5. **日志** - 记录和调试
6. **性能** - 测量和优化

## 与其他模块的关系

- **与 Protocol 的关系** - 处理 URL 和头信息
- **与 Server 的关系** - 路由和请求处理中使用
- **与 Client 的关系** - URL 构建和编码
- **与 Guides 的关系** - 参见 DrxUrlHelper.DEVGUIDE.md

## 最佳实践

1. **使用工具方法** - 而不是自己实现标准功能
2. **验证输入** - 使用验证工具检查数据
3. **错误处理** - 处理工具方法的异常
4. **文档** - 为复杂的工具函数提供示例
5. **性能** - 使用缓存减少重复处理

## 相关文档
- 参见 [../Guides/DrxUrlHelper.DEVGUIDE.md](../Guides/DrxUrlHelper.DEVGUIDE.md)
- 参见 [../Guides/HttpHeaders.DEVGUIDE.md](../Guides/HttpHeaders.DEVGUIDE.md)

# Serialization 目录 - JSON 序列化

## 概述
Serialization 目录提供 JSON 序列化和反序列化功能，用于在 HTTP 请求/响应中传递对象。

## 文件说明

### DrxJsonSerializer.cs
**JSON 序列化工具类**
- 封装 System.Text.Json 序列化功能
- 提供对象和 JSON 字符串的相互转换
- 特点：
  - 高性能序列化
  - 中文支持
  - 配置灵活
  - 类型安全

**主要方法：**
- **序列化**
  - `Serialize<T>(object)` - 对象转 JSON 字符串
  - `SerializeToBytes<T>(object)` - 对象转 JSON 字节
  
- **反序列化**
  - `Deserialize<T>(string)` - JSON 字符串转对象
  - `DeserializeFromBytes<T>(byte[])` - JSON 字节转对象
  - `Deserialize(string, Type)` - 动态类型反序列化

- **工具方法**
  - `IsValidJson(string)` - 验证 JSON 格式
  - `TryDeserialize<T>(string, out T)` - 安全反序列化

## 使用场景

1. **HTTP 请求体** - 序列化对象为 JSON 送出
2. **HTTP 响应体** - 反序列化 JSON 为对象
3. **配置管理** - 序列化/反序列化配置对象
4. **数据存储** - 存储对象为 JSON 字符串
5. **日志记录** - 序列化复杂对象用于日志

## 与其他模块的关系

- **与 Protocol 的关系** - HttpRequest/HttpResponse 使用序列化
- **与 Client 的关系** - DrxHttpClient 使用序列化
- **与 Server 的关系** - DrxHttpServer 使用序列化

## 序列化选项

通常支持的配置：
- `PropertyNamingPolicy` - 属性命名策略（CamelCase 等）
- `IgnoreNullValues` - 是否忽略空值
- `WriteIndented` - 是否格式化输出
- `DefaultIgnoreCondition` - 默认忽略条件
- `TypeInfoResolver` - 自定义类型信息解析

## 最佳实践

1. **异常处理** - 使用 TryDeserialize 等安全方法
2. **性能** - 缓存序列化结果（如适用）
3. **兼容性** - 注意版本间的序列化格式变化
4. **安全** - 验证反序列化的数据
5. **文档** - 为可序列化的类添加文档

## 相关文档
- 参见 [../Guides/JSON_SERIALIZATION_GUIDE.md](../Guides/JSON_SERIALIZATION_GUIDE.md) 详细指南

# Sse 目录 - Server-Sent Events 实现

## 概述
Sse 目录实现了 Server-Sent Events（SSE）功能，用于服务器向客户端推送实时事件。

## 功能说明

### Server-Sent Events 是什么？

SSE 是一种标准的 Web 技术，允许服务器在建立的 HTTP 连接上向客户端推送数据，而不需要客户端轮询。

**特点：**
- 基于 HTTP 标准，无需特殊协议
- 比轮询更高效
- 自动重连机制
- 事件类型支持

### 与 WebSocket 的区别

| 特性 | SSE | WebSocket |
|------|-----|----------|
| 协议 | HTTP | TCP |
| 推送方式 | 服务器推送 | 双向通信 |
| 标准化 | HTTP 标准 | WebSocket 标准 |
| 复杂性 | 简单 | 复杂 |
| 兼容性 | 较好 | 较好 |
| 用途 | 单向推送 | 双向实时通信 |

## 使用场景

1. **实时通知** - 系统通知、消息提醒
2. **数据更新** - 股票价格、天气、新闻等
3. **日志流** - 实时日志输出
4. **进度更新** - 后台任务的进度推送
5. **聊天消息** - 单向消息推送
6. **性能监控** - 实时数据监控

## 实现方式

### 服务器端

```csharp
[HttpSse("/api/events")]
public static async IAsyncEnumerable<SseEvent> StreamEvents(HttpRequest request)
{
    for (int i = 0; i < 10; i++)
    {
        yield return new SseEvent 
        { 
            Data = $"Event {i}", 
            EventType = "update" 
        };
        await Task.Delay(1000);
    }
}
```

### 客户端 (JavaScript)

```javascript
const eventSource = new EventSource('/api/events');

eventSource.addEventListener('update', (event) => {
    console.log('Received:', event.data);
});

eventSource.onerror = () => {
    console.error('Connection error');
    eventSource.close();
};
```

## SSE 事件格式

```
event: eventType
data: {"message": "Hello"}
id: 1

: Comment
data: Another message
retry: 5000

```

**字段说明：**
- `event` - 事件类型
- `data` - 事件数据（JSON 推荐）
- `id` - 事件 ID（用于恢复）
- `retry` - 断线重连等待时间（毫秒）
- `:` - 注释行

## 高级功能

### 1. 自动重连
- 连接断开后自动重连
- 支持指定重试间隔

### 2. 事件 ID
- 标记事件序列号
- 断线后恢复时发送 Last-Event-Id

### 3. 心跳保活
- 定期发送心跳保活连接
- 防止代理关闭连接

### 4. 事件过滤
- 客户端只接收特定事件类型
- 减少带宽消耗

## 与其他模块的关系

- **与 Configs 的关系** - HttpSseAttribute 标记 SSE 端点
- **与 Server 的关系** - DrxHttpServer.Sse 处理 SSE
- **与 Protocol 的关系** - HttpRequest/HttpResponse 管理连接

## 最佳实践

1. **心跳** - 每 30 秒发送心跳防止超时
2. **事件 ID** - 为关键事件添加 ID
3. **错误处理** - 客户端要处理连接错误
4. **资源清理** - 及时关闭过期连接
5. **并发限制** - 限制同时连接数
6. **内存管理** - 避免内存泄漏

## 浏览器兼容性

- Chrome/Edge: 完全支持
- Firefox: 完全支持
- Safari: 完全支持
- IE: 不支持（使用 polyfill）

## 性能考虑

- **连接数** - 每个客户端占用一个连接
- **带宽** - 相对 WebSocket 更省带宽
- **延迟** - 取决于网络和事件频率
- **扩展性** - 分布式部署需要特殊处理

## 相关文档
- 参见 [../Server/DrxHttpServer.Sse.cs](../Server/README.md)
- 参见 [../Configs/HttpSseAttribute.cs](../Configs/README.md)
- MDN SSE 文档：https://developer.mozilla.org/en-US/docs/Web/API/Server-sent_events

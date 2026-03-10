# TcpClientBridge
> TCP/UDP 客户端脚本桥接层

## Classes
| 类名 | 简介 |
|------|------|
| `TcpClientBridge` | 静态类，包装 NetworkClient 提供 TCP/UDP 连接与收发能力 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `createTcp(host, port)` | `host:string, port:int` | `NetworkClient` | 创建 TCP 客户端 |
| `createUdp(host, port)` | `host:string, port:int` | `NetworkClient` | 创建 UDP 客户端 |
| `connect(client)` | `client:NetworkClient` | `Task<bool>` | 异步连接 |
| `disconnect(client)` | `client:NetworkClient` | `void` | 断开连接 |
| `sendBytes(client, data)` | `client:NetworkClient, data:byte[]` | `void` | 发送字节 |
| `sendText(client, text)` | `client:NetworkClient, text:string` | `void` | 发送文本(UTF-8) |
| `sendBytesAsync(client, data)` | `client:NetworkClient, data:byte[]` | `Task<bool>` | 异步发送字节 |
| `sendTextAsync(client, text)` | `client:NetworkClient, text:string` | `Task<bool>` | 异步发送文本 |
| `isConnected(client)` | `client:NetworkClient` | `bool` | 查询连接状态 |
| `setTimeout(client, seconds)` | `client:NetworkClient, seconds:float` | `void` | 设置超时 |
| `dispose(client)` | `client:NetworkClient` | `void` | 释放资源 |

## Usage
```typescript
const client = TcpClient.createTcp("127.0.0.1", 9000);
await TcpClient.connect(client);
TcpClient.sendText(client, "Hello");
TcpClient.disconnect(client);
TcpClient.dispose(client);
```

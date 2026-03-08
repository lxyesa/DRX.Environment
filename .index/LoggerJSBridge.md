# LoggerJSBridge
> 将 Drx.Sdk.Shared.Logger 暴露为 JavaScript 可调用静态对象。

## Classes
| 类名 | 简介 |
|------|------|
| `LoggerJSBridge` | JS 桥接静态类，转发日志调用到 `Logger`。 |
| `LoggerJsCompatBridge` | 兼容别名桥接类，提供 `logger.*` 旧调用入口。 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `info(message)` | `message:object` | `void` | 输出信息日志。 |
| `warn(message)` | `message:object` | `void` | 输出警告日志。 |
| `error(message)` | `message:object` | `void` | 输出错误日志。 |
| `debug(message)` | `message:object` | `void` | 输出调试日志。 |

> `LoggerJsCompatBridge` 提供同名方法集合，语义与 `LoggerJSBridge` 完全一致。

## Usage
```csharp
using Drx.Sdk.Shared.JavaScript;

JavaScript.Execute("logger.info('hello from js');");
```

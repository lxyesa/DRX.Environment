# ConsoleJSBridge
> 将 .NET `Console` 能力暴露给 JavaScript：`console.log/info/debug/warn/error` 及异步版本。

## Classes
| 类名 | 简介 |
|------|------|
| `ConsoleJSBridge` | 控制台输出桥接，直接写标准输出/错误。 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `log(message)` | `object?` | `void` | 标准输出。 |
| `info(message)` | `object?` | `void` | 标准输出。 |
| `debug(message)` | `object?` | `void` | 标准输出。 |
| `warn(message)` | `object?` | `void` | 标准错误输出。 |
| `error(message)` | `object?` | `void` | 标准错误输出。 |
| `logAsync/infoAsync/debugAsync/warnAsync/errorAsync` | `object?` | `Task` | 异步输出。 |

## Usage
```javascript
console.info('hello');
console.error('oops');
```

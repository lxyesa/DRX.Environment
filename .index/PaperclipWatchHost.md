# WatchHost
> Paperclip CLI watch 运行宿主，负责重载循环、HTTP 服务器热重载与异常容错。

## Classes
| 类名 | 简介 |
|------|------|
| `WatchHost` | 在 run --watch 模式下执行初次运行与后续重载 |
| `ActiveServerTracker` | 跟踪脚本创建的 HTTP 服务器实例，重载前批量停止 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `WatchHost.Run(...)` | `options:PaperclipOptions, bootstrap:EngineBootstrap` | `int` | 进入 watch 生命周期，监听变更并触发重载 |
| `ActiveServerTracker.Register(...)` | `server:DrxHttpServer` | `void` | 注册新创建的服务器实例 |
| `ActiveServerTracker.StopAllAsync()` | — | `Task` | 停止并释放所有已注册服务器，清空列表 |
| `ActiveServerTracker.Count` | — | `int` | 当前跟踪的服务器数量 |

## Usage
```csharp
using var bootstrap = EngineBootstrap.Create(options);
return WatchHost.Run(options, bootstrap);
```

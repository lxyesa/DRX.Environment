# PaperclipReplHost
> Paperclip REPL 交互宿主，负责读取输入、执行脚本、处理内置命令与中断信号。

## Classes
| 类名 | 简介 |
|------|------|
| `ReplHost` | REPL 循环执行器，提供命令处理与多行输入能力。 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `Start(bootstrap)` | `EngineBootstrap` | `int` | 启动 REPL 循环，直到 `.exit` 或 Ctrl+D；返回退出码。 |

## Usage
```csharp
using var bootstrap = EngineBootstrap.Create(new PaperclipOptions { IsRepl = true });
var exitCode = ReplHost.Start(bootstrap);
```

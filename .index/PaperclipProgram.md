# Program
> Paperclip CLI 入口，负责参数解析、命令路由与顶层异常处理。

## Classes
| 类名 | 简介 |
|------|------|
| `Program` | 命令分发（run/repl/project/help/version）与统一错误处理。 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `Main(args)` | `string[]` | `void` | 入口：解析参数并路由到 ScriptHost/ReplHost/ProjectHost。 |

## Usage
```csharp
// paperclip run script.js
// paperclip run projectDir
// paperclip run script.js main
// paperclip project cr demo
// paperclip project de demo
```

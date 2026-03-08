# Program
> Paperclip CLI 入口，负责项目初始化、桥接脚本释放、模块预加载与 JS/TS 脚本执行（按 partial 文件拆分）。

## Classes
| 类名 | 简介 |
|------|------|
| `Program` | 命令分发（run/mrun/create/ts run/ts create）、PATH 自注册、运行时环境准备。 |

## File Split
| 文件 | 职责 |
|------|------|
| `Program.cs` | 常量（含 `CommandModuleRun`）、入口、顶层命令分发（run/mrun/create/ts）、帮助信息。 |
| `Program.Project.cs` | PATH 自注册、`create`/`ts create`、TS 脚手架。 |
| `Program.Execution.cs` | `run`/`mrun` 执行、预加载、参数解析；`ts` 入口委托处理器。 |
| `TypeScriptCommandHandler.cs` | `ts run/ts create` 子命令分发与执行细节。 |
| `Program.Resources.cs` | 嵌入资源发现与资源元数据类型。 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `Main(args)` | `string[]` | `void` | 入口；注册环境并执行命令。 |
| `ExecuteCommand(args)` | `string[]` | `void` | 分发 `run/mrun/create/ts` 顶层命令。 |
| `ExecuteTypeScriptCommand(args)` | `string[]` | `void` | 仅路由到 `TypeScriptCommandHandler.Execute`。 |
| `CreateProject(name)` | `string?` | `void` | 初始化项目、默认脚本，并在目标目录释放 `Models` 目录。 |
| `RunModuleScriptAsync(runOptions)` | `ModuleRunOptions` | `Task` | Module Runtime 执行入口：装载 ModuleRuntimeOptions（CLI+paperclip.json，含 imports map）、执行安全边界校验并运行脚本。 |
| `RunScriptAsync(scriptName, enableDebugLogs)` | `string, bool` | `Task` | Legacy Runtime 执行目标脚本并控制调试环境变量。 |
| `RunTypeScriptAsync(scriptName, enableDebugLogs)` | `string, bool` | `Task` | 委托 SDK 的 TypeScript 转译能力并执行脚本。 |
| `ParseModuleRunOptions(args)` | `string[]` | `ModuleRunOptions` | 解析 mrun 命令参数（`--config`、`--allow-import`、`--debug-events`）。 |
| `BuildModuleRuntimeOptions(runOptions, workingDirectory)` | `ModuleRunOptions, string` | `ModuleRuntimeOptions` | 合并默认值、配置文件与命令行覆盖，生成最终运行时选项。 |
| `RegisterDirectDotNetAccess()` | `-` | `void` | 向 JS 全局注册 `host` 与一组 `Net*` 宿主类型（Path/Directory/DateTime 等），支持脚本直接访问 .NET。 |
| `EnsureModelsReleased(targetDirectory)` | `string` | `void` | 从程序集嵌入资源释放完整 `Models` 目录到目标目录。 |

## Usage
```csharp
// paperclip run main.js          — legacy runtime
// paperclip mrun main.js         — module runtime (ESM)
// paperclip mrun main.ts         — module runtime (TS)
// paperclip create demo
// paperclip ts create demo-ts
// paperclip ts run main.ts
```

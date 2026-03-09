# Program
> Paperclip CLI 入口，负责项目初始化、桥接脚本释放、模块预加载与 JS/TS 脚本执行。使用 JavaScriptEngineBuilder 创建引擎实例，不依赖 JavaScript 静态门面。

## Classes
| 类名 | 简介 |
|------|------|
| `Program` | 命令分发（run/mrun/create/ts run/ts create）、PATH 自注册、引擎实例生命周期管理。 |

## File Split
| 文件 | 职责 |
|------|------|
| `Program.cs` | 常量、`CreateEngine()` 工厂方法、入口、顶层命令分发（run/mrun/create/ts）、帮助信息。 |
| `Program.Project.cs` | PATH 自注册、`create`/`ts create`、TS 脚手架、`main.js/main.ts` 初始化模板与 `paperclip_models` 友好导出模块生成。 |
| `Program.Execution.cs` | `run`/`mrun` 执行（using 引擎实例）、预加载、参数解析、TS 转译。 |
| `TypeScriptCommandHandler.cs` | `ts run/ts create` 子命令分发与执行细节。 |
| `Program.Resources.cs` | 嵌入资源发现与资源元数据类型。 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `Main(args)` | `string[]` | `void` | 入口；注册环境并执行命令。 |
| `CreateEngine()` | `-` | `IJavaScriptEngine` | 通过 JavaScriptEngineBuilder 创建引擎实例，调用方负责 Dispose。 |
| `ExecuteCommand(args)` | `string[]` | `void` | 分发 `run/mrun/create/ts` 顶层命令。 |
| `CreateProject(name)` | `string?` | `void` | 初始化项目，创建 `main.js/main.ts`，并在目标目录释放 `paperclip_models` 模块目录。 |
| `RunModuleScriptAsync(runOptions)` | `ModuleRunOptions` | `Task` | Module Runtime：创建引擎实例，装载 ModuleRuntimeOptions，执行安全边界校验并运行脚本。 |
| `RunScriptAsync(scriptName, enableDebugLogs)` | `string, bool` | `Task` | Legacy Runtime：创建引擎实例执行目标脚本。 |
| `RunTypeScriptAsync(scriptName, enableDebugLogs)` | `string, bool` | `Task` | 创建引擎实例，转译 TS 并执行。 |
| `RegisterDirectDotNetAccess(engine)` | `IJavaScriptEngine` | `void` | 向引擎实例注册 `host` 与一组 `Net*` 宿主类型。 |
| `RegisterDynamicImportGlobal(engine, loader, entryScriptPath)` | `IJavaScriptEngine, ModuleLoader, string` | `void` | 注册 __importDynamic 异步委托与 import() polyfill。 |
| `PreloadPresetModules(engine, targetDirectory)` | `IJavaScriptEngine, string` | `void` | 在引擎实例上预加载 SDK 模块。 |
| `TranspileTypeScriptFile(engine, scriptPath, workingDirectory)` | `IJavaScriptEngine, string, string` | `string` | 在引擎实例上加载 typescript.js 转译 TS。 |
| `EnsureTypeScriptScaffold(targetDir, projectName)` | `string, string` | `void` | 生成 tsconfig.json/package.json 脚手架。 |
| `EnsurePaperclipFriendlyModules(targetDir)` | `string` | `void` | 生成 `paperclip_models/index.js` 与 `paperclip_models/index.ts`，导出 JS/TS 友好 SDK API。 |
| `EnsureModelsReleased(targetDirectory)` | `string` | `void` | 从程序集嵌入资源释放完整 `paperclip_models` 模块目录到目标目录。 |

## Usage
```csharp
// paperclip run main.js          — legacy runtime
// paperclip mrun main.js         — module runtime (ESM)
// paperclip mrun main.ts         — module runtime (TS)
// paperclip create demo
// paperclip ts create demo-ts
// paperclip ts run main.ts
```

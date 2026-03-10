# PaperclipEngineBootstrap

## File
`Library/Applications/Clients/Paperclip/Hosting/Bootstrap/EngineBootstrap.cs`

## Namespace
`DrxPaperclip.Hosting`

## Purpose
引擎启动器，根据 `PaperclipOptions` 创建并配置 `IJavaScriptEngine`，管理模块运行时全部组件的生命周期。

## Class: `EngineBootstrap`
- **Sealed class**, implements `IDisposable`
- Dispose 顺序：engine → plugins → diagnosticCollector

### Properties
| Property | Type | Description |
|----------|------|-------------|
| `Engine` | `IJavaScriptEngine` | 已配置的 JavaScript 引擎实例 |
| `ModuleLoader` | `ModuleLoader?` | 模块加载器（`--no-modules` 时为 null） |
| `ModuleCache` | `ModuleCache?` | 模块缓存实例 |
| `TranspileCache` | `TranspileCache?` | TS 转译缓存实例（`--no-cache` 时为 null） |
| `DiagnosticOutput` | `DiagnosticOutput` | 诊断输出管道 |
| `Options` | `ModuleRuntimeOptions` | 运行时配置选项 |

### Methods
| Method | Signature | Purpose |
|--------|-----------|---------|
| `Create` | `static EngineBootstrap Create(PaperclipOptions options)` | 工厂方法：按 Design §2.2 流程创建完整引擎栈 |
| `Dispose` | `void Dispose()` | 释放 engine → plugins → collector 资源 |

### Create() 初始化流程
1. 确定 projectRoot（run file → 文件目录；run dir → 目录自身；repl → CWD）
2. `ModuleRuntimeOptions.CreateSecureDefaults(projectRoot)`
3. 添加 `--allow-path` 到 `AllowedImportPathPrefixes`
4. `--debug` → `EnableDebugLogs = true` + `EnableStructuredDebugEvents = true`
5. `AllowNodeModulesResolution = true`
6. `ValidateAndNormalize()`
7. 创建 `ImportSecurityPolicy` → `ModuleDiagnosticCollector` → `ModuleResolver` → `ModuleCache` → `ModuleLoader`
8. 根据 `--no-cache` 决定是否创建 `TranspileCache`
9. `PluginLoader.Load()` 加载插件
10. `JavaScriptEngineBuilder` 链式配置 + `Build()`
11. 注册 `print/pause` 全局函数（`0` 参数与 `1` 参数桥接包装）
12. 创建 `DiagnosticOutput`

## Dependencies
- `DrxPaperclip.Cli.PaperclipOptions`
- `DrxPaperclip.Diagnostics.DiagnosticOutput`
- `DrxPaperclip.Hosting.Caching.TranspileCache`
- `Drx.Sdk.Shared.JavaScript.Abstractions.IJavaScriptEngine`
- `Drx.Sdk.Shared.JavaScript.Abstractions.IJavaScriptPlugin`
- `Drx.Sdk.Shared.JavaScript.Abstractions.ModuleRuntimeOptions`
- `Drx.Sdk.Shared.JavaScript.Engine.JavaScriptEngineBuilder`
- `Drx.Sdk.Shared.JavaScript.Engine.ImportSecurityPolicy`
- `Drx.Sdk.Shared.JavaScript.Engine.ModuleResolver`
- `Drx.Sdk.Shared.JavaScript.Engine.ModuleCache`
- `Drx.Sdk.Shared.JavaScript.Engine.ModuleLoader`
- `Drx.Sdk.Shared.JavaScript.Engine.ModuleDiagnosticCollector`

## Spec Reference
- **Requirements**: FR-1, FR-2, FR-3, FR-5, FR-6, FR-7, FR-8
- **Design**: §2.2 EngineBootstrap

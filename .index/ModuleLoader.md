# ModuleLoader
> 模块加载器：递归构建依赖图，状态机驱动加载流程，集成 ModuleResolver + ModuleCache，支持循环依赖检测、错误传播与诊断事件。

## Classes
| 类名 | 简介 |
|------|------|
| `ModuleLoader` | 模块加载器入口，使用 ModuleResolver 解析 + ModuleCache 缓存，递归加载依赖图。 |
| `ModuleLoaderEvent` | 诊断事件记录：EventName / CacheKey / Data / Timestamp。 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `ModuleLoader(resolver, cache, options)` | `ModuleResolver, ModuleCache, ModuleRuntimeOptions` | `ctor` | 注入解析器、缓存与运行时选项。 |
| `LoadModuleGraph(entryPath, executeModule)` | `string, Func<string, string, object?>` | `ModuleRecord` | 从入口递归加载模块图。 |
| `DetermineModuleKind(filePath)` | `string` | `ModuleKind` | 按扩展名推断模块类型（内部静态）。 |

## Usage
```csharp
var resolver = new ModuleResolver(options, builtins);
var cache = new ModuleCache();
var loader = new ModuleLoader(resolver, cache, options);

var entryRecord = loader.LoadModuleGraph(
    entryFilePath: @"C:\project\main.js",
    executeModule: (path, source) => engine.Execute(source));

// entryRecord.State == Loaded
// entryRecord.Dependencies → 依赖 cache key 列表
// cache.GetStatistics().HitRate → 缓存命中率

// 诊断事件（debug 模式）
foreach (var evt in loader.DiagnosticEvents)
    Console.WriteLine($"{evt.EventName}: {evt.CacheKey}");
```

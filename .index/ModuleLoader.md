# ModuleLoader
> 模块加载器：递归构建依赖图，状态机驱动加载流程，集成 ModuleResolver + ModuleCache + InteropBridge，支持循环依赖检测、ESM↔CJS 互操作、错误传播与诊断事件。

## Classes
| 类名 | 简介 |
|------|------|
| `ModuleLoader` | 模块加载器入口，使用 ModuleResolver 解析 + ModuleCache 缓存 + InteropBridge 互操作，递归加载依赖图。 |
| `ModuleLoaderEvent` | 诊断事件记录：EventName / CacheKey / Data / Timestamp。 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `ModuleLoader(resolver, cache, options, securityPolicy?)` | `ModuleResolver, ModuleCache, ModuleRuntimeOptions, ImportSecurityPolicy?` | `ctor` | 注入解析器、缓存、运行时选项与可选安全策略，自动创建 InteropBridge。加载前对每个模块路径做安全复查。 |
| `LoadModuleGraph(entryPath, executeModule)` | `string, Func<string, string, object?>` | `ModuleRecord` | 从入口递归加载模块图。 |
| `DynamicImportAsync(specifier, fromFilePath, executeModule)` | `string, string?, Func<string, string, object?>` | `Task<ModuleRecord>` | 异步动态导入模块（对应 JS `import()`），复用统一 resolver/cache/interop，错误模型与静态导入一致。 |
| `GetDependencyExports(importerKind, depCacheKey, targetSource)` | `ModuleKind, string, string?` | `InteropResult` | 获取依赖模块导出并自动应用 ESM↔CJS 互操作包装。 |
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

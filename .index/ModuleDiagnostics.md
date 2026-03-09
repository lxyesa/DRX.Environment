# ModuleDiagnostics
> 统一模块诊断事件模型与收集器——全链路 resolve/load/cache/security/interop 结构化事件、错误码常量与结构化错误接口。

## Classes
| 类名 | 简介 |
|------|------|
| `ModuleDiagnosticEvent` | 统一诊断事件：EventName/Category/Severity/ModuleKey/Data/Timestamp，支持 JSONL 与可读文本双格式输出。 |
| `ModuleDiagnosticCollector` | 线程安全事件收集器，非 debug 零开销，提供分类/严重级别快捷方法、JSONL/文本批量输出与摘要统计。 |
| `DiagnosticSummary` | 摘要统计 record：TotalEvents/ByCategory/BySeverity。 |
| `ModuleErrorCodes` | 全链路错误码常量类：PC_RES_*/PC_LOAD_*/PC_DYN_*/PC_SEC_*/PC_INTEROP_* 集中管理。 |
| `IModuleStructuredError` | 统一结构化错误接口：Code/Hint/ToStructuredError()，由 ModuleResolutionException/ModuleLoadException/ImportSecurityException/InteropException 实现。 |

## Enums
| 枚举 | 简介 |
|------|------|
| `DiagnosticCategory` | 事件类别：Resolve/Load/Cache/Security/Interop/DynamicImport/Runtime。 |
| `DiagnosticSeverity` | 严重级别：Trace/Debug/Info/Warning/Error。 |

## Methods (ModuleDiagnosticCollector)
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `Emit(eventName, category, severity, moduleKey, data?)` | `string, DiagnosticCategory, DiagnosticSeverity, string, object?` | `void` | 发出事件，非 debug 零分配。 |
| `EmitResolve(eventName, moduleKey, data?)` | `string, string, object?` | `void` | Resolve 类别快捷。 |
| `EmitLoad(eventName, moduleKey, data?)` | `string, string, object?` | `void` | Load 类别快捷。 |
| `EmitCache(eventName, moduleKey, data?)` | `string, string, object?` | `void` | Cache 类别快捷。 |
| `EmitSecurity(eventName, moduleKey, data?)` | `string, string, object?` | `void` | Security 类别快捷。 |
| `EmitInterop(eventName, moduleKey, data?)` | `string, string, object?` | `void` | Interop 类别快捷。 |
| `EmitDynamic(eventName, moduleKey, data?)` | `string, string, object?` | `void` | DynamicImport 类别快捷。 |
| `EmitError(eventName, category, moduleKey, data?)` | `string, DiagnosticCategory, string, object?` | `void` | Error 级别快捷。 |
| `EmitWarning(eventName, category, moduleKey, data?)` | `string, DiagnosticCategory, string, object?` | `void` | Warning 级别快捷。 |
| `ToJsonLines()` | - | `string` | JSONL 格式全量输出。 |
| `ToReadableText()` | - | `string` | 可读文本全量输出。 |
| `GetSummary()` | - | `DiagnosticSummary` | 摘要统计。 |
| `Clear()` | - | `void` | 清空。 |

## Methods (ModuleDiagnosticEvent)
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `ToJsonLine()` | - | `string` | 单事件 JSON 行。 |
| `ToReadableString()` | - | `string` | 单事件可读文本。 |

## Error Codes (ModuleErrorCodes)
| 常量 | 值 | 类别 |
|------|-----|------|
| `ResEmptySpecifier` | PC_RES_000 | Resolver |
| `ResPathNotFound` | PC_RES_001 | Resolver |
| `ResMissingContext` | PC_RES_002 | Resolver |
| `ResBuiltinNotFound` | PC_RES_003 | Resolver |
| `ResBareNotFound` | PC_RES_004 | Resolver |
| `LoadEntryFailed` | PC_LOAD_001 | Loader |
| `LoadFileNotFound` | PC_LOAD_002 | Loader |
| `LoadDependencyFailed` | PC_LOAD_003 | Loader |
| `LoadPipelineError` | PC_LOAD_004 | Loader |
| `LoadCircularError` | PC_LOAD_005 | Loader |
| `LoadInteropMissing` | PC_LOAD_006 | Loader |
| `DynResolveFailed` | PC_DYN_001 | Dynamic |
| `DynUnresolved` | PC_DYN_002 | Dynamic |
| `DynTargetFailed` | PC_DYN_003 | Dynamic |
| `DynUnexpected` | PC_DYN_004 | Dynamic |
| `SecOutOfBoundary` | PC_SEC_001 | Security |
| `SecUnauthorized` | PC_SEC_002 | Security |
| `SecInvalidPath` | PC_SEC_003 | Security |
| `InteropNullNamespace` | PC_INTEROP_001 | Interop |
| `InteropNotLoaded` | PC_INTEROP_002 | Interop |
| `InteropUnknownDirection` | PC_INTEROP_003 | Interop |

## Usage
```csharp
// 创建收集器（仅 debug 模式启用）
var collector = new ModuleDiagnosticCollector(options.EnableDebugLogs);

// 注入到各组件
var policy = new ImportSecurityPolicy(options, collector);
var loader = new ModuleLoader(resolver, cache, options, policy, collector);

// 使用快捷方法发出事件
collector.EmitResolve("resolve.start", specifier, new { from = fromFile });

// 输出 JSONL（机器解析）
Console.WriteLine(collector.ToJsonLines());

// 输出可读文本
Console.WriteLine(collector.ToReadableText());

// 获取摘要
var summary = collector.GetSummary();
// summary.TotalEvents, summary.ByCategory, summary.BySeverity

// IModuleStructuredError 统一错误处理
catch (Exception ex) when (ex is IModuleStructuredError structured)
{
    var error = structured.ToStructuredError();
    // code, hint, 完整上下文
}
```

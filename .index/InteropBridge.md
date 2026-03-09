# InteropBridge
> ESM↔CJS 互操作桥接层：实现跨模块类型的导出映射与包装，不隐式吞错，语义文档化。

## Classes
| 类名 | 简介 |
|------|------|
| `InteropBridge` | 互操作核心：CJS→ESM namespace 包装、ESM→CJS require 包装、方向分类与诊断事件。 |
| `InteropResult` | 互操作结果记录：Direction / Exports / Applied。 |
| `InteropDirection` | 互操作方向枚举：EsmImportsCjs / CjsRequiresEsm / SameKind / JsonImport / BuiltinImport。 |
| `InteropException` | 标准化互操作异常：Code / ModuleUrl / Direction / Hint。 |
| `InteropDiagnosticEvent` | 诊断事件记录：EventName / ModuleUrl / Data / Timestamp。 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `InteropBridge(options)` | `ModuleRuntimeOptions` | `ctor` | 注入运行时选项（控制诊断事件收集）。 |
| `WrapCjsAsEsmNamespace(moduleExports, cjsSource, moduleUrl)` | `object?, string?, string` | `IReadOnlyDictionary<string, object?>` | CJS exports → ESM namespace：default = module.exports，命名导出 = 运行时提取 + 源码静态推导。 |
| `WrapEsmForCjsRequire(esmNamespace, moduleUrl)` | `IReadOnlyDictionary?, string` | `IReadOnlyDictionary<string, object?>` | ESM namespace → CJS require 返回值（原样返回 namespace 对象）。 |
| `ResolveInterop(importerKind, targetRecord, targetSource)` | `ModuleKind, ModuleRecord, string?` | `InteropResult` | 根据导入方与目标模块类型自动判定方向并应用包装。 |
| `ClassifyDirection(importerKind, targetKind)` | `ModuleKind, ModuleKind` | `InteropDirection` | 静态方法，分类互操作方向。 |

## Usage
```csharp
var interop = new InteropBridge(options);

// ESM 导入 CJS
var ns = interop.WrapCjsAsEsmNamespace(cjsModuleExports, cjsSource, "/path/to/cjs.js");
// ns["default"] == cjsModuleExports
// ns["helperFn"] == cjsModuleExports.helperFn（若可提取）

// CJS require ESM
var wrapped = interop.WrapEsmForCjsRequire(esmRecord.Exports, "/path/to/esm.mjs");
// wrapped["default"] == ESM 默认导出

// 自动判定
var result = interop.ResolveInterop(ModuleKind.Esm, cjsRecord);
// result.Direction == EsmImportsCjs, result.Applied == true

// 通过 ModuleLoader 集成
var depResult = loader.GetDependencyExports(ModuleKind.Esm, depCacheKey);
```

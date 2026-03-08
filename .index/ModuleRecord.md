# ModuleRecord
> 模块记录数据模型——描述单个已加载模块的完整状态：状态机（Loading→Loaded/Failed）、缓存键、依赖追踪、耗时与错误诊断。

## Classes
| 类名 | 简介 |
|------|------|
| `ModuleRecord` | 模块实例状态快照，含缓存键、URL、类型、状态机、命名空间、导出、依赖与错误。 |
| `ModuleRecordState` | 模块状态枚举：Loading / Loaded / Failed。 |
| `ModuleKind` | 模块类型枚举：Esm / Cjs / Json / Builtin。 |
| `ModuleLoadException` | 标准化模块加载异常，含 Code / ModuleUrl / Phase / Hint。 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `ModuleRecord(cacheKey, url, kind)` | `string, string, ModuleKind` | `ctor` | 创建 Loading 状态占位记录。 |
| `MarkLoaded(namespace, exports, deps, duration)` | `object?, IReadOnlyDictionary?, IReadOnlyList<string>, TimeSpan` | `void` | Loading → Loaded 状态转换。 |
| `MarkFailed(error, duration)` | `ModuleLoadException, TimeSpan` | `void` | Loading → Failed 状态转换。 |
| `ToDiagnostic()` | `-` | `object` | 结构化诊断输出。 |
| `ModuleLoadException(code, url, phase, reason, hint?, inner?)` | `string, string, string, string, string?, Exception?` | `ctor` | 创建加载异常。 |
| `ToStructuredError()` | `-` | `object` | 输出标准化错误结构。 |

## Usage
```csharp
var record = new ModuleRecord(cacheKey, "/path/to/module.js", ModuleKind.Esm);
// State == Loading

record.MarkLoaded(namespace, exports, depKeys, elapsed);
// State == Loaded

// 或失败：
record.MarkFailed(new ModuleLoadException(...), elapsed);
// State == Failed, Error 可检视
```

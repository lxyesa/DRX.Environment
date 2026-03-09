# PaperclipDiagnosticOutput

## File
`Library/Applications/Clients/Paperclip/Diagnostics/DiagnosticOutput.cs`

## Namespace
`DrxPaperclip.Diagnostics`

## Purpose
诊断输出管道，将 `ModuleDiagnosticCollector` 事件实时输出到 stderr，仅在 `--debug` 模式下激活。

## Class: `DiagnosticOutput`
- **Sealed class**, 非静态（持有收集器引用与增量状态）

### Constructor
| Parameter | Type | Description |
|-----------|------|-------------|
| `collector` | `ModuleDiagnosticCollector` | SDK 诊断事件收集器实例 |
| `enabled` | `bool` | 是否启用诊断输出（对应 `--debug` 标志） |

### Methods
| Method | Signature | Purpose |
|--------|-----------|---------|
| `Flush` | `void Flush()` | 增量输出自上次调用以来新产生的诊断事件到 stderr；非 debug 模式直接返回 |
| `PrintSummary` | `void PrintSummary(ModuleCache cache)` | 输出模块缓存统计（entries/hits/misses/hit rate）及诊断事件分类摘要到 stderr |

## Dependencies
- `Drx.Sdk.Shared.JavaScript.Engine.ModuleDiagnosticCollector`
- `Drx.Sdk.Shared.JavaScript.Engine.ModuleCache`
- `Drx.Sdk.Shared.JavaScript.Engine.ModuleCacheStatistics`
- `Drx.Sdk.Shared.JavaScript.Engine.DiagnosticSummary`

## Spec Reference
- **Requirements**: FR-8 (诊断输出)
- **Design**: §2.7 DiagnosticOutput

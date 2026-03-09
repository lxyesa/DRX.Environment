# PaperclipErrorFormatter

## ErrorFormatter.cs
- **Path**: `Library/Applications/Clients/Paperclip/Formatting/ErrorFormatter.cs`
- **Namespace**: `DrxPaperclip.Formatting`
- **Type**: `static class`
- **Purpose**: 将 SDK 异常格式化为用户友好的 stderr 输出，并提供退出码映射。

### Public Methods
| Method | Signature | Description |
|---|---|---|
| `Format` | `static string Format(Exception ex)` | 将异常格式化为 `Error [{code}]: {message}\n  at {location}\n  Hint: {hint}` 格式 |
| `GetExitCode` | `static int GetExitCode(Exception ex)` | 返回异常对应的进程退出码（NFR-3） |

### Exception Mapping
| SDK Exception Type | Exit Code | Error Code Source |
|---|---|---|
| `JavaScriptException` | 1 | ErrorType (e.g. "TypeError") |
| `ModuleResolutionException` | 1 | `.Code` (e.g. "PC_RES_NOT_FOUND") |
| `ModuleLoadException` | 1 | `.Code` (e.g. "PC_LOAD_xxx") |
| `ImportSecurityException` | 4 | `.Code` (e.g. "PC_SEC_001") |
| Other `Exception` | 1 | "UNKNOWN" |

### Format Template
```
Error [PC_RES_NOT_FOUND]: Cannot find module './utils' (specifier: ./utils, from: /app/main.js)
  Hint: Check the module path and ensure the file exists.
```

### Dependencies
- `Drx.Sdk.Shared.JavaScript.Exceptions.JavaScriptException`
- `Drx.Sdk.Shared.JavaScript.Engine.ModuleLoadException` (in ModuleRecord.cs)
- `Drx.Sdk.Shared.JavaScript.Engine.ModuleResolutionException` (in ModuleResolver.cs)
- `Drx.Sdk.Shared.JavaScript.Engine.ImportSecurityException` (in ImportSecurityPolicy.cs)

### Spec Traceability
- **Requirements**: NFR-3 (退出码语义), NFR-5 (错误消息质量)
- **Design**: §2.6 ErrorFormatter

# ImportSecurityPolicy
> deny-by-default 导入安全策略，校验所有模块路径在项目根或白名单内，检测路径穿越与符号链接越界，含审计日志与结构化异常。

## Classes
| 类名 | 简介 |
|------|------|
| `ImportSecurityPolicy` | 安全策略核心：规范化路径校验、符号链接物理路径验证、deny-by-default 边界判定、审计日志收集。 |
| `ImportSecurityException` | 安全策略异常：含 Code/ResolvedPath/Specifier/From/Reason/Hint，支持 `ToStructuredError()` JSON 序列化。 |
| `SecurityAuditEntry` | 审计日志 record：Timestamp/Decision/DenialReason/ResolvedPath/Specifier/From/ProjectRoot。 |
| `SecurityDecision` | 枚举：Allowed / Denied。 |
| `SecurityDenialReason` | 枚举：OutOfBoundary / SymlinkEscape / PathTraversal / InvalidPath。 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `ImportSecurityPolicy(options)` | `ModuleRuntimeOptions` | `ctor` | 从运行时选项构建策略，规范化项目根与白名单。 |
| `ValidateAccess(resolvedPath, specifier, fromFilePath)` | `string, string, string?` | `void` | 完整路径校验（含符号链接），拒绝时抛 `ImportSecurityException`。 |
| `IsPathAllowed(path)` | `string` | `bool` | 快速预检（不含符号链接检测），供 resolver 内部使用。 |
| `ToStructuredError()` | `-` | `object` | `ImportSecurityException` 方法：转为匿名对象供 JSON 序列化。 |

## Error Codes
| 错误码 | 含义 |
|--------|------|
| `PC_SEC_001` | 路径越界（含符号链接越界） |
| `PC_SEC_002` | 未授权白名单 |
| `PC_SEC_003` | 非法 specifier / 空路径 |

## Usage
```csharp
var options = ModuleRuntimeOptions.CreateSecureDefaults(projectRoot);
options.AllowedImportPathPrefixes.Add(@"D:\shared-libs");
options.ValidateAndNormalize();

var policy = new ImportSecurityPolicy(options);

// 完整校验（含符号链接）
policy.ValidateAccess(@"D:\project\src\main.js", "./main.js", null);

// 快速预检
bool allowed = policy.IsPathAllowed(@"D:\project\src\util.js");

// 审计日志
foreach (var entry in policy.AuditLog)
    Console.WriteLine($"{entry.Decision}: {entry.ResolvedPath}");
```

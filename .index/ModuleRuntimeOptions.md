# ModuleRuntimeOptions
> Module Runtime 运行时配置对象，提供安全默认值、配置校验、调试输出与 workspace imports map 配置。

## Classes
| 类名 | 简介 |
|------|------|
| `ModuleRuntimeOptions` | 聚合模块运行时的项目根目录、白名单导入路径、workspace imports map、调试事件输出与解析开关。 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `CreateSecureDefaults(projectRoot)` | `string` | `ModuleRuntimeOptions` | 以安全默认策略创建配置（默认拒绝越界）。 |
| `ValidateAndNormalize()` | `-` | `void` | 规范化路径并验证配置值，失败抛出可操作异常。 |
| `IsPathAllowed(path)` | `string` | `bool` | 判断目标路径是否在项目根或白名单内。 |

## Usage
```csharp
var options = ModuleRuntimeOptions.CreateSecureDefaults(Directory.GetCurrentDirectory());
options.EnableDebugLogs = true;
options.ValidateAndNormalize();
```

# TranspileCache
> Paperclip 的 TypeScript 转译缓存组件，提供磁盘持久化、版本校验与异常降级能力。

## Classes
| 类名 | 简介 |
|------|------|
| `TranspileCache` | 管理 `.paperclip/transpile-cache/` 下缓存文件，支持命中读取与回写。 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `TranspileCache(projectRoot)` | `string` | `-` | 初始化缓存目录。 |
| `TryGet(scriptPath, sourceContent, typeScriptVersion, out transpiledCode)` | `string, string, string, out string?` | `bool` | 命中返回缓存转译结果。 |
| `Set(scriptPath, sourceContent, typeScriptVersion, transpiledCode)` | `string, string, string, string` | `void` | 写入缓存；失败降级不抛出。 |
| `Invalidate(scriptPath)` | `string` | `void` | 删除指定脚本对应缓存。 |

## Usage
```csharp
var cache = new TranspileCache(projectRoot);
if (!cache.TryGet(tsPath, source, tsVersion, out var code))
{
    code = JavaScript.TranspileTypeScriptFile(tsPath, projectRoot);
    cache.Set(tsPath, source, tsVersion, code);
}
```

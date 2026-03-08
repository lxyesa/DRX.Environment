# ModuleCache
> 线程安全模块实例缓存：规范化 URL 缓存键、ConcurrentDictionary 存储、single-flight 并发合并加载、命中/未命中统计与循环依赖占位。

## Classes
| 类名 | 简介 |
|------|------|
| `ModuleCache` | 线程安全模块缓存，支持 TryGet / GetOrLoad（single-flight）/ TryRegisterLoading（循环依赖占位）。 |
| `ModuleCacheStatistics` | 缓存统计记录：条目数、命中/未命中次数、按状态分类计数、命中率。 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `NormalizeCacheKey(path)` | `string` | `string` | 静态方法，规范化绝对路径为缓存键。 |
| `TryGet(cacheKey, out record)` | `string, out ModuleRecord?` | `bool` | 查询缓存，更新命中/未命中计数。 |
| `GetOrLoad(cacheKey, factory)` | `string, Func<ModuleRecord>` | `ModuleRecord` | Single-flight：并发同键仅执行一次 factory。 |
| `TryRegisterLoading(cacheKey, record, out existing)` | `string, ModuleRecord, out ModuleRecord?` | `bool` | 注册 Loading 占位，循环依赖检测。 |
| `Update(cacheKey, record)` | `string, ModuleRecord` | `void` | 状态转换后更新缓存。 |
| `Contains(cacheKey)` | `string` | `bool` | 是否含指定键。 |
| `IsLoading(cacheKey)` | `string` | `bool` | 指定键是否处于 Loading 状态。 |
| `GetSnapshot()` | `-` | `IReadOnlyDictionary<string, ModuleRecord>` | 只读快照。 |
| `GetStatistics()` | `-` | `ModuleCacheStatistics` | 缓存统计。 |
| `Clear()` | `-` | `void` | 清空全部缓存与计数。 |

## Usage
```csharp
var cache = new ModuleCache();
var key = ModuleCache.NormalizeCacheKey(@"C:\project\main.js");

// single-flight 加载
var record = cache.GetOrLoad(key, () => {
    var r = new ModuleRecord(key, url, ModuleKind.Esm);
    // ... 执行加载 ...
    r.MarkLoaded(ns, exports, deps, elapsed);
    return r;
});

var stats = cache.GetStatistics();
// stats.HitRate, stats.LoadedCount
```

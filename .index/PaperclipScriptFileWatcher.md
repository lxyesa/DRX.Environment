# ScriptFileWatcher
> Paperclip 脚本监听适配层，复用 DevFileChangeService 做去抖聚合。

## Classes
| 类名 | 简介 |
|------|------|
| `ScriptFileWatcher` | 监听目录中的脚本文件变化并触发回调 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `ScriptFileWatcher(...)` | `options:PaperclipOptions, projectRoot:string, debounceMs:int?` | `ctor` | 构造监听服务并绑定目录与路径归一化 |
| `Start()` | 无 | `void` | 启动监听 |
| `Stop()` | 无 | `void` | 停止监听 |
| `Dispose()` | 无 | `void` | 释放监听资源 |

## Usage
```csharp
using var watcher = new ScriptFileWatcher(options, projectRoot, 200);
watcher.ScriptFilesChanged += (_, e) => Console.WriteLine(string.Join(",", e.Paths));
watcher.Start();
```

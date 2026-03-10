# PaperclipPluginLoader

## PluginLoader.cs
- **Path**: `Library/Applications/Clients/Paperclip/Hosting/Plugins/PluginLoader.cs`
- **Namespace**: `DrxPaperclip.Hosting`
- **Type**: `public static class PluginLoader`
- **Description**: 插件动态加载器，将 `--plugin <path>` 指定的 DLL 加载为 `IJavaScriptPlugin` 实例。

### Public API
| Member | Signature | Description |
|--------|-----------|-------------|
| `Load` | `static List<IJavaScriptPlugin> Load(IReadOnlyList<string> dllPaths)` | 加载 DLL 列表，扫描 IJavaScriptPlugin 实现，实例化返回 |

### Dependencies
- `Drx.Sdk.Shared.JavaScript.Abstractions.IJavaScriptPlugin`
- `System.Reflection.Assembly.LoadFrom`
- `System.Activator.CreateInstance`

### Error Handling
- `FileNotFoundException` — DLL 路径不存在
- `InvalidOperationException` — 程序集加载失败或插件实例化失败

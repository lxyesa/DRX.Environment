# JavaScript
> 静态门面入口，使用 Lazy 引擎并按 partial 拆分执行、注册、函数调用与安全执行。

## Classes
| 类名 | 简介 |
|------|------|
| `JavaScript` | 提供零配置静态 API，将调用路由到 `IJavaScriptEngine` 实例。 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `Execute(scriptString)` | `string` | `object?` | 执行脚本字符串并返回结果。 |
| `ExecuteAsync(scriptString)` | `string` | `Task<bool>` | 异步执行脚本字符串。 |
| `ExecuteAsync<T>(scriptString)` | `string` | `Task<T>` | 异步执行并转换结果。 |
| `ExecuteFile(filePath)` | `string` | `object?` | 执行脚本文件并返回结果。 |
| `ExecuteFile<T>(filePath)` | `string` | `T` | 执行脚本文件并转换结果。 |
| `TryExecute(scriptString)` | `string` | `(bool success, object result)` | 安全执行脚本字符串。 |
| `TryExecuteAsync(scriptString)` | `string` | `Task<(bool success, object result)>` | 安全异步执行脚本字符串。 |
| `TryExecuteFile(filePath)` | `string` | `(bool success, object result)` | 安全执行脚本文件。 |
| `RegisterGlobal(name, value)` | `string, object` | `void` | 注册全局对象。 |
| `RegisterGlobal(name, method)` | `string, Delegate` | `void` | 注册全局委托。 |
| `RegisterHostType(name, type)` | `string, Type` | `void` | 直接注册 .NET 类型到 JS 全局。 |
| `RegisterHostType<T>(name)` | `string` | `void` | 泛型方式注册 .NET 类型。 |
| `GetRegisteredGlobals()` | `-` | `Dictionary<string, Type>` | 获取引擎已注册全局对象快照。 |
| `GetRegisteredClasses()` | `-` | `List<string>` | 获取导出类名列表。 |
| `CallFunction(functionName, filePath, args)` | `string, string, params object[]` | `object?` | 调用 JS 文件中的函数。 |
| `CallFunction<T>(functionName, filePath, args)` | `string, string, params object[]` | `T` | 调用函数并转换结果。 |
| `TranspileTypeScriptFile(scriptPath, workingDirectory)` | `string, string?` | `string` | 运行时加载 TypeScript 编译器并将 TS 文件转译为 JS 文本。 |
| `EnsureTypeScriptScaffold(targetDir, projectName)` | `string, string` | `void` | 生成 TypeScript 项目基础脚手架（tsconfig.json、package.json）。 |

## Usage
```csharp
var result = JavaScript.Execute("1 + 1");
var ok = await JavaScript.TryExecuteAsync("globalThis.x = 42");
```

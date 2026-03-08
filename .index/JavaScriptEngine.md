# JavaScriptEngine
> DI 驱动的脚本引擎实现，按 partial 拆分初始化、执行与注册职责。

## Classes
| 类名 | 简介 |
|------|------|
| `JavaScriptEngine` | `IJavaScriptEngine` 实现，聚合运行时、绑定器、注册表和类型转换器，并管理脚本缓存与全局注册目录。 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `JavaScriptEngine(runtime,binder,registry,converter,options)` | `IScriptEngineRuntime, IScriptBinder, IScriptRegistry, ITypeConverter, IOptions<JavaScriptEngineOptions>` | `ctor` | 构造器注入依赖，启动导出类型绑定与插件初始化。 |
| `Execute(script)` | `string` | `object?` | 执行脚本并返回结果（`runtime.Evaluate`）。 |
| `Execute<T>(script)` | `string` | `T` | 执行并转换为目标类型。 |
| `ExecuteAsync(script)` | `string` | `ValueTask<object?>` | 异步求值脚本。 |
| `ExecuteAsync<T>(script)` | `string` | `ValueTask<T>` | 异步执行并转换结果。 |
| `ExecuteFile(filePath)` | `string` | `object?` | 读取脚本文件并执行，支持缓存。 |
| `ExecuteFile<T>(filePath)` | `string` | `T` | 执行脚本文件并转换结果。 |
| `CallFunction(functionName,filePath,args)` | `string, string, params object[]` | `object?` | 执行脚本文件后调用指定函数。 |
| `CallFunction<T>(functionName,filePath,args)` | `string, string, params object[]` | `T` | 调用函数并转换结果。 |
| `RegisterGlobal(name,value)` | `string, object` | `void` | 注册全局对象并记录类型目录。 |
| `RegisterGlobal(name,method)` | `string, Delegate` | `void` | 注册全局委托并记录类型目录。 |
| `RegisterHostType(name,type)` | `string, Type` | `void` | 直接注册 .NET 类型到脚本全局（静态成员可直接调用）。 |
| `GetRegisteredGlobals()` | `-` | `IReadOnlyDictionary<string, Type>` | 获取已注册全局目录。 |
| `GetRegisteredClasses()` | `-` | `IReadOnlyList<string>` | 获取已绑定导出类名。 |
| `Dispose()` | `-` | `void` | 释放运行时与插件资源。 |

## Usage
```csharp
// minimal example
var engine = provider.GetRequiredService<IJavaScriptEngine>();
var value = engine.Execute<int>("1 + 1");
```

# IJavaScriptEngine
> JavaScript 引擎公共抽象接口，定义执行、函数调用与宿主注册能力。

## Classes
| 类名 | 简介 |
|------|------|
| `IJavaScriptEngine` | 引擎统一契约，供门面与实现类解耦。 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `Execute(script)` | `string` | `object?` | 执行脚本字符串并返回结果。 |
| `ExecuteFile(filePath)` | `string` | `object?` | 执行脚本文件。 |
| `CallFunction(functionName,filePath,args)` | `string, string, params object[]` | `object?` | 调用脚本文件中的函数。 |
| `RegisterGlobal(name,value)` | `string, object` | `void` | 注册宿主对象。 |
| `RegisterGlobal(name,method)` | `string, Delegate` | `void` | 注册宿主委托。 |
| `RegisterHostType(name,type)` | `string, Type` | `void` | 直接注册 .NET 类型（静态成员/构造器可直接在 JS 调用）。 |
| `GetRegisteredGlobals()` | `-` | `IReadOnlyDictionary<string, Type>` | 获取全局注册目录。 |
| `GetRegisteredClasses()` | `-` | `IReadOnlyList<string>` | 获取导出类名列表。 |

## Usage
```csharp
var engine = provider.GetRequiredService<IJavaScriptEngine>();
engine.RegisterHostType("NetPath", typeof(System.IO.Path));
```

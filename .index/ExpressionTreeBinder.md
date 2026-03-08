# ExpressionTreeBinder

> 高性能方法绑定器：使用 Expression Tree 编译方法调用委托，替代反射 Invoke，并通过 ConcurrentDictionary 缓存已编译结果。

## Classes

| 类名 | 简介 |
|------|------|
| `ExpressionTreeBinder` | 实现 `IScriptBinder`，将 .NET 类型/方法以编译委托形式注册到 `IScriptEngineRuntime`。 |

## Methods

| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `BindType(runtime, metadata)` | `IScriptEngineRuntime`, `ScriptTypeMetadata` | `void` | 注册类型 + 所有导出方法/属性/字段；属性和字段以延迟 `Func<object?>` 注册。 |
| `BindMethod(runtime, name, method)` | `IScriptEngineRuntime`, `string`, `MethodInfo` | `void` | 获取或编译委托后，以 name 注册到运行时。 |
| `GetOrCompileDelegate(method)` | `MethodInfo` | `Func<object?[], object?>` | 缓存命中直接返回；未命中调用 `CompileDelegate`。 |
| `CompileDelegate(method)` | `MethodInfo` | `Func<object?[], object?>` | Expression Tree 编译：参数经 `ITypeConverter.FromJsValue` 转换；void 方法补 null 常量统一返回类型。 |

## Usage

```csharp
var binder = new ExpressionTreeBinder(typeConverter);
binder.BindType(runtime, ScriptTypeMetadata.FromType(typeof(MyClass), "MyClass", ScriptExportType.Class));
```

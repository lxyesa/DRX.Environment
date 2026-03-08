# TypeConverter

> .NET 与 JavaScript 值之间的类型转换器，实现 `ITypeConverter`，采用策略管道模式。

## Classes

| 类名 | 简介 |
|------|------|
| `TypeConverter` | partial class，拆分为三个文件; 实现 ITypeConverter |

## Methods

| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `RegisterStrategy(strategy)` | `strategy: ITypeConversionStrategy` | `void` | 注册自定义策略并按 Priority 降序排列 |
| `ToJsValue(value)` | `value: object?` | `object?` | 委托到 ConvertToJs（TypeConverter.ToJs.cs） |
| `FromJsValue(jsValue, targetType)` | `jsValue: object?, targetType: Type` | `object?` | 委托到 ConvertFromJs（TypeConverter.FromJs.cs） |
| `FromJsValue<T>(jsValue)` | `jsValue: object?` | `T?` | 泛型重载，调用 FromJsValue |
| `TryCustomStrategy(value, targetType)` | `value: object?, targetType: Type` | `(bool, object?)` | 按优先级遍历已注册策略 |
| `ConvertToJs(value)` | `value: object?` | `object?` | .NET → JS（ToJs.cs） |
| `ConvertFromJs(jsValue, targetType)` | `jsValue: object?, targetType: Type` | `object?` | JS → .NET，修复 null 处理（FromJs.cs） |

## Usage

```csharp
var converter = new TypeConverter();
// 注册自定义策略
converter.RegisterStrategy(new MyDateTimeStrategy());

// .NET → JS
var jsVal = converter.ToJsValue(myObject);

// JS → .NET（null 安全）
var dotnetVal = converter.FromJsValue<MyModel>(jsResult);
```

# JavaScriptEngineOptions
> JavaScript 引擎配置对象，用于控制缓存、重试、程序集扫描与插件初始化。

## Classes
| 类名 | 简介 |
|------|------|
| `JavaScriptEngineOptions` | JavaScript 引擎初始化与执行相关选项集合。 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `ctor` | `-` | `JavaScriptEngineOptions` | 创建默认引擎选项实例。 |

## Usage
```csharp
services.AddDrxJavaScript(options =>
{
    options.EnableScriptCaching = true;
    options.MaxRetry = 1;
});
```

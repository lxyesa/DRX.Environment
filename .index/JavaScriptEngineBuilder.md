# JavaScriptEngineBuilder
> Fluent API 构建器，无需 DI 容器即可链式配置并创建 IJavaScriptEngine 实例。

## Classes
| 类名 | 简介 |
|------|------|
| `JavaScriptEngineBuilder` | 可链式调用的引擎构建器，内部维护 JavaScriptEngineOptions 并在 Build() 时用 ServiceCollection 完成 DI 组装。 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `WithOption(Action<JavaScriptEngineOptions>)` | `configure: Action<JavaScriptEngineOptions>` | `JavaScriptEngineBuilder` | 配置引擎选项，支持链式调用。 |
| `WithConverter(Action<ITypeConverter>)` | `configure: Action<ITypeConverter>` | `JavaScriptEngineBuilder` | 追加类型转换器配置回调，叠加到 Options.ConfigureConverter 上。 |
| `WithPlugin(IJavaScriptPlugin)` | `plugin: IJavaScriptPlugin` | `JavaScriptEngineBuilder` | 添加插件到插件列表。 |
| `WithAssemblies(params Assembly[])` | `assemblies: Assembly[]` | `JavaScriptEngineBuilder` | 设置 Attribute 扫描范围程序集。 |
| `Build()` | — | `IJavaScriptEngine` | 完成构建：创建 ServiceCollection、注册服务、构建 ServiceProvider，解析并返回 IJavaScriptEngine。 |

## Usage
```csharp
var engine = new JavaScriptEngineBuilder()
    .WithOption(o => o.EnableScriptCaching = false)
    .WithAssemblies(typeof(MyExport).Assembly)
    .WithPlugin(new MyPlugin())
    .WithConverter(c => c.RegisterStrategy(new MyStrategy()))
    .Build();

engine.Execute("1 + 1");
```

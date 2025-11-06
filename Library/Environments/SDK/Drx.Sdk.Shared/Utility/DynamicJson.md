# DynamicJson — 动态 JsonNode 包装器

> 源码： [`Library/Environments/SDK/Drx.Sdk.Shared/Utility/DynamicJson.cs:1`](Library/Environments/SDK/Drx.Sdk.Shared/Utility/DynamicJson.cs:1)

概要

DynamicJson 提供了对 System.Text.Json.Nodes.JsonNode 的动态包装，使得可以使用 dynamic 语法通过点号或索引访问 JSON 数据。

设计要点

- 基于 System.Dynamic.DynamicObject 实现，对 JsonObject/JsonArray/JsonValue 做统一包装。
- 对象字段访问返回 DynamicJson（包装对应 JsonNode），值类型访问返回 CLR 原始值（如 string/int/bool）。
- 支持通过属性名（d.Name）和索引（d["name"] 或 d[0]）访问。
- 支持将属性赋值（仅当底层为 JsonObject 时），可直接赋入 CLR 值、JsonNode 或 IEnumerable 来构建 JsonArray。
- 提供 ToJsonString/ToJsonStringAsync 方便序列化，提供 TryConvert 将动态对象转换为指定类型。

公开 API（要点）

- DynamicJson()：创建一个包装空 JsonObject 的实例。
- DynamicJson(JsonNode? node)：使用已有 JsonNode 创建包装器（node 可为 null）。
- static DynamicJson? From(JsonNode? node)：如果 node 为 null 返回 null，否则返回包装实例。
- JsonNode? ToJsonNode()：返回底层 JsonNode（可能为 null）。
- string? ToJsonString()：同步序列化为 JSON 字符串（不可缩进）。
- Task<string?> ToJsonStringAsync(CancellationToken = default)：异步序列化（使用 JsonSerializer.SerializeAsync）。

动态行为（重写的方法）

- TryGetMember(GetMemberBinder binder, out object? result)
  - 在底层为 JsonObject 时，尝试按名称查找属性（优先精确匹配，再进行大小写不敏感查找）。
  - 找到对象或数组时返回 DynamicJson，找到值时返回 CLR 值，未找到返回 null（作为成功）。
- TrySetMember(SetMemberBinder binder, object? value)
  - 仅当底层为 JsonObject 时生效，否则返回 false。
  - 支持直接设置 JsonNode、常见原始类型、IEnumerable（构建 JsonArray）或任意 POCO（使用 JsonSerializer.SerializeToNode）。
- TryGetIndex(GetIndexBinder binder, object[] indexes, out object? result)
  - 支持单一索引：字符串索引用于 JsonObject，整数索引用于 JsonArray。越界或找不到时返回 null（成功）。
- TryConvert(ConvertBinder binder, out object? result)
  - 支持将动态包装转换为指定目标类型。
  - 对 JsonValue 优先使用 JsonValue.GetValue<T>（反射调用）提取常见类型，失败后使用 JsonSerializer 反序列化节点字符串。

内部实现细节与注意点

- WrapNode(JsonNode? node)：根据节点类型返回 DynamicJson（对象/数组）或 CLR 值（JsonValue）。
- 对 JsonValue 的 GetValue<object>() 有 try/catch，若失败降级为 ToJsonString()。
- 底层 JsonNode 是可变的，DynamicJson 只是一层包装。若在多线程中共享，请先复制（例如序列化再解析）。
- TrySetMember 对 IEnumerable 的处理会把 string 排除，以免将 string 当作集合拆分。
- TryConvert 在 node 为 null 时：若目标为非 nullable 值类型则返回失败（false）；否则视为成功并返回 null。
- 序列化选项 _jsonOptions: PropertyNameCaseInsensitive = true，WriteIndented = false，用于 SerializeToNode 调用。

使用示例

示例 1：解析字符串并读取字段

```csharp
using System.Text.Json.Nodes;

var json = "{\"name\":\"alice\",\"age\":30,\"tags\":[\"dev\",\"dotnet\"]}";
JsonNode? node = JsonNode.Parse(json);
dynamic d = node.AsDynamic(); // 扩展方法，等价于 DynamicJson.From(node)
string? name = d.name; // "alice"
int? age = d.age; // 30（会尝试转换到 int）
string? firstTag = d.tags[0]; // "dev"
```

示例 2：设置/新增字段

```csharp
var obj = new JsonObject();
dynamic d2 = obj.AsDynamic();
d2.title = "Hello";
d2.count = 5;
d2.items = new[] { 1, 2, 3 }; // 会成为 JsonArray
string jsonOut = d2.ToJsonString() ?? "{}";
```

示例 3：将动态对象转换为 POCO

```csharp
public class Person { public string? Name { get; set; } public int Age { get; set; } }
JsonNode node = JsonNode.Parse("{\"name\":\"bob\",\"age\":40}");
dynamic d3 = node.AsDynamic();
Person p = (Person)d3; // TryConvert 会将底层节点反序列化为 Person
```

高级用法与陷阱

- 读操作对大小写友好：TryGetMember 包含大小写不敏感查找，但写入时使用传入的 binder.Name 作为键名（不做大小写转换）。
- 当底层节点不是 JsonObject 时（例如 JsonArray 或 JsonValue），对属性赋值将失败（TrySetMember 返回 false）。
- TryGetIndex 对非法索引（如超出范围或错误类型）返回 null 而不是抛出异常，便于链式访问，但需留意空值检查。
- 性能：WrapNode 会为对象/数组构造新的 DynamicJson 包装器；大量频繁访问会产生对象分配。若追求极致性能，建议直接使用 JsonNode API。

何时使用 DynamicJson

- 快速处理未知结构的 JSON，或者在脚本/模板环境中需要简洁的点号访问时非常方便。
- 做一次性小规模读取/修改并序列化回字符串的场景。

不适合的场景

- 对性能或 GC 敏感且频繁访问的内热路径：建议使用强类型 POCO 或直接操作 JsonNode。
- 需要严格线程安全的数据结构：DynamicJson 底层 JsonNode 非线程安全。

相关扩展方法

- JsonNode? AsDynamic(this JsonNode? node)：把 JsonNode 包装为 dynamic（返回 DynamicJson 或 null）。
- dynamic? ToDynamicObject(this string? json)：解析 JSON 字符串并包装为 dynamic（输入为空或解析失败时返回 null）。

参考源码

源文件：[`Library/Environments/SDK/Drx.Sdk.Shared/Utility/DynamicJson.cs:1`](Library/Environments/SDK/Drx.Sdk.Shared/Utility/DynamicJson.cs:1)

许可证与备注

- 本文档基于源码注释与实现反向整理，旨在帮助维护者与调用方快速理解 API 与行为。
- 保持示例和生产代码中的空值与类型检查，以避免在运行时出现 null/类型转换问题。
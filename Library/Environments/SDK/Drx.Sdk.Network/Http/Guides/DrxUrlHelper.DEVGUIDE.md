# DrxUrlHelper.DEVGUIDE.md

## 概要
DrxUrlHelper 是一个小型的 URL 与 Query 辅助工具类，位于 `Drx.Sdk.Network.V2.Web` 命名空间中。
本 DEVGUIDE 描述该类的目的、输入/输出契约、公共 API、使用示例、边界情况与故障排查建议。

目的：
- 统一客户端构造 URL 时的编码行为（避免将 `?`、`&`、`#` 等保留字符误用为分隔符）。
- 统一服务端解析 query 的行为，提供容错、易用的解析接口。
- 提供简单的构造/编码/解码方法，便于前后端一致使用。

语言：中文。文件位置：`Library/Environments/SDK/Drx.Sdk.Network/V2/Web/DrxUrlHelper.cs`。

## 输入/输出契约（I/O Contract）
- ParseQuery
  - 输入：string? rawQuery（可含或不含前导 `?`，可为 null/空）
  - 输出：Dictionary<string, string> —— 返回键名（不区分大小写）到第一个值的映射；如果键存在但无值，返回空字符串；解析异常时尽量返回已解析项（不会抛出）。
  - 成功准则：传入标准 query（如 `?a=1&b=2`）应返回对应键值。
  - 错误模式：非法或极端输入（例如不符合 query 语法）不会抛出异常，但可能导致部分项解析失败或最终返回空字典。

- BuildQueryString
  - 输入：IDictionary<string,string>? parameters
  - 输出：string —— 以 `?` 开头的编码后的 query，若 parameters 为 null/空则返回空字符串。
  - 成功准则：对每个键与值使用 `Uri.EscapeDataString` 编码并正确用 `&` 连接。

- Encode / Decode
  - 输入/输出：单字符串编码/解码，Encode 使用 `Uri.EscapeDataString`，Decode 使用 `Uri.UnescapeDataString`（异常回退原值）。

- BuildUrlWithQuery
  - 输入：baseUrl（可能已包含 query）、parameters
  - 输出：完整 URL 字符串，若 baseUrl 已包含 query，则追加参数（用 `&`）。

Assumption（假设）:
- ParseQuery 依赖 `Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery` 的解析规则（已处理 URL 解码与重复键）。
- `HttpRequest` 的字段填充由调用方负责；本工具只负责字符串层面的解析/构造。

## 公共 API 概览
| 名称 | 签名 | 描述 | 返回 | 错误/异常模式 |
|---|---:|---|---|---|
| ParseQuery | Dictionary<string,string> ParseQuery(string? rawQuery) | 解析原始 query 字符串并返回键->值映射（取第一个值） | Dictionary | 不抛异常（容错返回） |
| BuildQueryString | string BuildQueryString(IDictionary<string,string>? parameters) | 将参数字典编码并返回以 `?` 开头的 query 字符串 | string（可能为空） | 无异常（对 null/空友好） |
| Encode | string Encode(string? value) | 对单个值进行 URL 编码 | string | 无异常（null->空字符串） |
| Decode | string Decode(string? value) | 对编码值进行解码 | string | 捕获异常并回退原值 |
| BuildUrlWithQuery | string BuildUrlWithQuery(string baseUrl, IDictionary<string,string>? parameters) | 将 baseUrl 与参数拼接成完整 URL，处理 baseUrl 已含 query 的情况 | string | 无异常（容错） |

## 方法详解

### ParseQuery(rawQuery)
- 参数
  - rawQuery (string?) — 原始 query，如 `?a=1&b=2` 或 `a=1&b=2`。
- 返回
  - Dictionary<string,string> — 键名不区分大小写，值为第一个解析到的键值（已自动 URL 解码）。
- 行为
  - 使用 `QueryHelpers.ParseQuery` 解析；对重复键只保留第一个值。
  - 捕获解析期间的异常并尽可能返回已解析项。
- 示例（成功）
```csharp
var q = DrxUrlHelper.ParseQuery("?un=user%3Fname&psw=123");
// q["un"] == "user?name"; q["psw"] == "123"
```
- 边界/错误示例
```csharp
var q = DrxUrlHelper.ParseQuery(null); // 返回空字典
var q2 = DrxUrlHelper.ParseQuery("weirdstring"); // ParseQuery 可能解析为 {"weirdstring":""} 或 空，取决于 ParseQuery 行为
```
- 注：若需要保留重复键的所有值，请直接调用 `QueryHelpers.ParseQuery` 并遍历 StringValues。

### BuildQueryString(parameters)
- 参数
  - parameters (IDictionary<string,string>?) — 待构造的键值对。
- 返回
  - string — 以 `?` 开头的编码 query，例如 `?a=1&b=2`；若输入为空，返回 `""`。
- 示例
```csharp
var url = DrxUrlHelper.BuildQueryString(new Dictionary<string,string>{{"un","a?b"},{"psw","p&@"}});
// url == "?un=a%3Fb&psw=p%26%40"
```
- 注意
  - 使用 `Uri.EscapeDataString` 对键和值均编码；该方法适合编码 query 参数值。

### Encode / Decode
- Encode: 对单值进行编码；Decode: 尝试解码并在失败时回退原值。适用于需要逐个编码/解码时使用。

### BuildUrlWithQuery(baseUrl, parameters)
- 行为
  - 当 baseUrl 不包含 `?` 时，直接 append `BuildQueryString(parameters)`。
  - 当 baseUrl 已包含 `?` 时，以 `&` 追加新的参数（确保不会插入多余 `?`）。
- 示例
```csharp
DrxUrlHelper.BuildUrlWithQuery("/api/login", new Dict{{"un","u"}});
// "/api/login?un=u"
DrxUrlHelper.BuildUrlWithQuery("/api/login?x=1", new Dict{{"un","u"}});
// "/api/login?x=1&un=u"
```

## 详细使用示例（服务端与客户端）

场景 1：前端（或客户端）构造请求并发送
```csharp
// 假设使用 HttpClient
var parameters = new Dictionary<string,string>
{
    {"un", "user?name"}, // 用户名包含 '?' 字符
    {"psw", "p&sw"}
};
var url = DrxUrlHelper.BuildUrlWithQuery("https://api.example.com/login", parameters);
// 发送请求
var resp = await httpClient.GetAsync(url);
```
说明：BuildUrlWithQuery 会把 `?` 和 `&` 等字符编码成 `%3F`、`%26`，从而保证服务器端接收的 query 能被正确解析为两个独立参数。

场景 2：服务端（DrxHttpServer 的处理器）解析 query
```csharp
// 假设 handler 能获得框架的 HttpRequest 对象，并且其中有字段保存原始 query
var parsed = DrxUrlHelper.ParseQuery(request.RawQuery); // request.RawQuery 例如 "?un=user%3Fname&psw=p%26sw"
if (parsed.TryGetValue("un", out var un)) {
    // un == "user?name"
}
```

验证（Quick verification）：
- 创建一个小单元测试或控制台程序：构造参数字典，调用 `BuildUrlWithQuery`，再把得到的 query 片段传回 `ParseQuery`，验证得到的字典与原字典相同（对 null/空做断言）。

## 高级主题与实现说明
- 为什么使用 `Uri.EscapeDataString`：它对大多数 query 值编码正确；注意在极端场景下对某些 Unicode 字符的编码长度问题，但通常可接受。
- 重复键（例如 `a=1&a=2`）的处理：`ParseQuery` 只保留第一个值；若需要全部值，用户应直接使用 `QueryHelpers.ParseQuery` 并读取 `StringValues`。
- Fragment (`#...`)：浏览器不会把 `#fragment` 发送到服务器，不能作为服务器参数；在客户端构造 URL 时要注意不要把参数放到 fragment 中以期望服务器接收。

## 并发与资源管理
- DrxUrlHelper 为静态无状态工具类，线程安全（仅使用无状态方法与 `QueryHelpers`），无需额外的并发控制或释放资源。

## 边界情况、性能、安全建议
- 对于超长 query 或大量参数，URL 长度可能超出浏览器或服务器限制（通常在几 KB 到几十 KB 之间，视平台而定），建议对于大数据使用 POST + body。
- 安全：对于敏感数据（密码、令牌），优先使用 HTTPS，且尽量放在请求体（POST）中而非 URL，以避免日志或 Referer 泄露。
- 编码盲点：某些旧客户端可能错误地使用 `HttpUtility.UrlEncode` 或自定义编码，导致服务器端解析失败。推荐统一使用本工具或 `Uri.EscapeDataString`。

## 故障排查 / FAQ
- 问：为什么我的 `psw` 参数被解析成 `un` 的一部分？
  - 答：因为 URL 中使用了多个 `?`。第一个 `?` 分隔 path 和 query，后续 `?` 会作为 query 的内容字符，正确的分隔符应为 `&`。

- 问：我需要支持重复键怎么办（例如 `tags=1&tags=2`）？
  - 答：直接使用 `Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery` 并读取 `StringValues`，或者在 `ParseQuery` 基础上改造为返回 `Dictionary<string, StringValues>`。

- 问：如何在服务器端统一为所有 handler 填充解析好的参数？
  - 答：两种做法：
    1. 在 `DrxHttpServer.ParseRequestAsync` 中调用 `DrxUrlHelper.ParseQuery(request.Url.Query)`，并把返回结果写入 `HttpRequest` 的 `Query` 字段（或框架约定的属性）。
    2. 添加一个中间件，统一解析并把结果注入到上下文中（推荐，清晰且可复用）。

## 文件位置
- 源码：`Library/Environments/SDK/Drx.Sdk.Network/V2/Web/DrxUrlHelper.cs`

## 建议的下一步
- 在 `DrxHttpServer.ParseRequestAsync` 中：
  - 将解析逻辑替换为 `var parsed = DrxUrlHelper.ParseQuery(request.Url?.Query);` 并把 `parsed` 写入 `HttpRequest` 的 `QueryParameters`（或类似字段）。
  - 或者实现一个全局中间件 `QueryParsingMiddleware`，在进入路由处理前执行该解析并注入上下文。
- 添加单元测试：
  - Happy path：普通键值对、键含特殊字符、键/值为空。
  - Edge case：重复键、超长 query、非法编码字符串。

## 质量门（Quality Gates）
- Build: 需要 `Microsoft.AspNetCore.WebUtilities` 的引用，仓库中已有相依（附件显示命名空间已被引用）；新文件仅使用 BCL 与该包，应能通过编译。
- Lint/Typecheck: C# 编译器将进行类型检查。
- Tests: 建议新增一个 xUnit/NUnit 测试文件 `Tests/DrxUrlHelperTests.cs`，验证编码/解码/解析/构造的行为。

---

如果你希望我：
- 自动把 `ParseRequestAsync` 修改为使用 `DrxUrlHelper.ParseQuery` 并提交补丁（我会先读取 `HttpRequest` 的定义以找到合适的字段），请选择“替换 ParseRequest”。
- 或者我为仓库添加一个小的单元测试文件并运行测试，请选择“添加测试并运行”。
- 若只需要文档目前这样已够，请回复“完成”。

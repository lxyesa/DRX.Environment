# SqlitePersistence.DEVGUIDE.md

## 概述

`SqlitePersistence` 是基于 `Microsoft.Data.Sqlite` 的轻量级键值持久化实现，采用单表（KeyValue）设计来支持多种基本类型与复合数据的存储。该组件提供简单的内存缓存、事务写入、WAL 模式与若干诊断方法，适用于嵌入式/桌面服务器场景的小型持久化需求。

本文档为该组件的开发者指南（中文），包含输入/输出契约、公共 API 总览、逐方法说明、使用示例、并发/资源与性能注意事项、故障排查与推荐的后续工作。

---

## 假设（Assumptions）

- 从提供的代码摘要中部分实现细节被省略（例如部分删除缓存逻辑、CompareCacheWithDisk 与 Dump 中的细节）。文档中对这些省略部分做出合理假设并以 `Assumption:` 标注，需要人工确认。
- 该类型在默认构造时会打开并保持一个 `SqliteConnection` 实例（见 InitializeDatabase），并在 `Dispose()` 中关闭。
- 键命名、表名等文本是区分大小写/不区分大小写由 SQLite 默认行为决定（取决于创建和查询方式），此处假定按字面字符串匹配。

---

## I/O 契约（Inputs / Outputs / 成功与错误模式）

- 构造器：`SqlitePersistence(string databasePath, bool readOnly = false, bool createIfNotExists = true, int cacheSize = 1024)`
  - 输入：
    - `databasePath`：SQLite 数据库文件路径（必需）。
    - `readOnly`：是否以只读模式打开（true：只允许读取操作，写操作将抛出 InvalidOperationException）。
    - `createIfNotExists`：当 `readOnly == false` 时，若数据库文件不存在是否创建。
    - `cacheSize`：SQLite `PRAGMA cache_size`（以 KB 为单位，函数内部使用负值表示 KB）。
  - 输出：成功构造并打开数据库连接，否则抛出 ArgumentNullException（`databasePath` 为 null）或 Sqlite 连接异常。

- 读写方法：大多数方法返回基本类型或 bool 表示成功/失败。
  - 成功：返回具体类型或 `true`。
  - 失败：方法多数以 `catch { return false; }` 或返回 null 的方式吞掉异常（设计选择），但可能在连接未打开或已 Dispose 时抛出 `ObjectDisposedException`。因此调用方应注意返回值并在关键路径记录日志或抛出。

- 异常模式：
  - `ObjectDisposedException`：在已 Dispose 的实例上调用任意方法时抛出（由内部 ThrowIfDisposed 实现）。
  - `InvalidOperationException`：在只读模式下尝试写操作会通过 `ThrowIfReadOnly()` 抛出。
  - 其他 SQL/IO 异常：构造或 Execute 期间可能抛出（写操作大多被 catch 并转换为返回 false）。

---

## 公共 API 总览（摘要表）

| 名称 | 签名 | 描述 | 返回 | 错误/异常 |
|---|---:|---|---:|---|
| 构造器 | `SqlitePersistence(string databasePath, bool readOnly=false, bool createIfNotExists=true, int cacheSize=1024)` | 打开/初始化数据库 | SqlitePersistence | ArgumentNullException, SqliteException |
| CreateTable | `bool CreateTable(string tableName)` | 创建逻辑表（在 KeyValue 表中写入 __TABLE_META__） | bool（是否新创建） | ObjectDisposedException, InvalidOperationException |
| DeleteTable | `bool DeleteTable(string tableName)` | 删除逻辑表与其键值数据（并清理缓存） | bool | ObjectDisposedException, InvalidOperationException |
| ReadString / WriteString / UpdateString | `string? ReadString(string tableName, string key)` / `bool WriteString(string tableName,string key,string value)` / `bool UpdateString(...)` | 读/写/更新字符串 | string? / bool | ObjectDisposedException, InvalidOperationException |
| ReadInt32/WriteInt32/UpdateInt32 | --- | 读/写/更新 Int32 | int? / bool | --- |
| ReadInt64/WriteInt64/UpdateInt64 | --- | 读/写/更新 Int64 | long? / bool | --- |
| ReadDouble/WriteDouble/UpdateDouble | --- | 读/写/更新 Double | double? / bool | --- |
| ReadFloat/WriteFloat/UpdateFloat | --- | 读/写/更新 Float | float? / bool | --- |
| ReadBool/WriteBool/UpdateBool | --- | 读/写/更新 Bool | bool? / bool | --- |
| ReadBytes/WriteBytes/UpdateBytes | --- | 读/写/更新 二进制数组 | byte[]? / bool | --- |
| KeyExists | `bool KeyExists(string tableName, string key)` | 检查键是否存在（先查缓存） | bool | ObjectDisposedException |
| RemoveKey | `bool RemoveKey(string tableName, string key)` | 删除键（并移除缓存） | bool | ObjectDisposedException, InvalidOperationException |
| WriteComposite / ReadComposite / RemoveComposite / CompositeExists | `bool WriteComposite(string tableName,string key,Func<CompositeBuilder,CompositeBuilder> buildAction)` 等 | 复合数据写入/读取/删除/存在检查（使用 `CompositeBuilder`） | bool / CompositeBuilder? / bool | ObjectDisposedException, InvalidOperationException |
| ListKeys | `List<string> ListKeys(string tableName)` | 列出表中所有键（除 __TABLE_META__） | List<string> | ObjectDisposedException |
| UpdateCacheSize | `bool UpdateCacheSize(int newCacheSize)` | 更新 PRAGMA cache_size | bool | ObjectDisposedException |
| SyncCache | `void SyncCache()` | 强制 WAL 检查点，尽力刷新到磁盘 | void | ObjectDisposedException |
| ReloadCache | `void ReloadCache()` | 清空内存缓存 | void | ObjectDisposedException |
| CompareCacheWithDisk | `bool CompareCacheWithDisk()` | 比较内存缓存与磁盘的一致性（实现细节被省略，需要人工确认） | bool | ObjectDisposedException |
| Dump | `void Dump(bool writeBinary = false)` | 将数据库信息导出用于诊断，可写入 `laststart.dump` 与原始 db 二进制（实现细节部分省略） | void | ObjectDisposedException |
| Close / Dispose | `void Close()` / `void Dispose()` | 关闭并释放资源（Dispose 同步调用 SyncCache） | void | 无（幂等） |

---

## 方法详述（逐方法）

### 构造器
`SqlitePersistence(string databasePath, bool readOnly = false, bool createIfNotExists = true, int cacheSize = 1024)`
- 参数
  - `databasePath` (string) — 数据库文件路径，不能为空。
  - `readOnly` (bool) — 是否以只读模式打开。
  - `createIfNotExists` (bool) — 若数据库不存在是否创建（仅当 readOnly == false 时生效）。
  - `cacheSize` (int) — SQLite PRAGMA cache_size（KB），方法内部会通过 `PRAGMA cache_size=-{cacheSize}` 来设置。
- 返回
  - 成功构造对象，内部打开并保持一个 `SqliteConnection`。
- 错误/异常
  - `ArgumentNullException`：`databasePath` 为 null。
  - Sqlite 连接异常（打开失败）。
- 备注
  - 非只读模式下，构造器会设置一组 PRAGMA（WAL, synchronous, foreign_keys, temp_store 等）。

---

### CreateTable(string tableName)
- 描述：在 `KeyValue` 表中插入一条键为 `__TABLE_META__` 的元记录以标记逻辑表存在。
- 返回：true 表示新建成功；若表已存在返回 false；遇异常也返回 false。
- 示例
```csharp
var db = new SqlitePersistence("data.db");
var ok = db.CreateTable("users");
if (ok) Console.WriteLine("表创建成功");
```
- 注意：如果数据库处于只读模式，将通过 `ThrowIfReadOnly()` 抛出异常。

---

### DeleteTable(string tableName)
- 描述：删除逻辑表（删除 `KeyValue` 中该 TableName 的所有行）并清理缓存中以该 TableName 前缀的条目。
- 返回：成功则 true，否则 false（异常被捕获并返回 false）。
- Assumption: 源代码省略了缓存清理的具体行，此处假定会遍历 `_cache.Keys` 并移除所有以 `${tableName}:` 开头的条目。
- 示例
```csharp
var ok = db.DeleteTable("users");
```

---

### 读写/更新基本类型（示例行为相同）
- ReadString/WriteString/UpdateString
- ReadInt32/WriteInt32/UpdateInt32
- ReadInt64/WriteInt64/UpdateInt64
- ReadDouble/WriteDouble/UpdateDouble
- ReadFloat/WriteFloat/UpdateFloat
- ReadBool/WriteBool/UpdateBool
- ReadBytes/WriteBytes/UpdateBytes

行为说明：
- 写操作内部使用事务并执行 `INSERT ... ON CONFLICT(TableName, Key) DO UPDATE SET ...` 以实现幂等写入。
- 写入后会更新 `_cache`（通过 `AddOrUpdate`）。
- 读操作优先检查 `_cache`，若命中直接返回，否则从数据库读取并缓存结果。

示例（字符串）
```csharp
db.WriteString("users", "alice.name", "Alice");
var name = db.ReadString("users", "alice.name");
```

边界/注意：
- UpdateX 系列方法会先调用 `KeyExists`，若键不存在则返回 false（不会创建新键）。
- 读取时若数据库中 `Type` 列与期望类型不一致，`ReadValue<T>` 会返回默认值（例如 null 或默认类型）。

---

### KeyExists(string tableName, string key)
- 描述：检查键是否存在。实现先查缓存（_cache），若缓存命中返回 true，否则查询数据库计数。
- 返回：bool
- 示例
```csharp
if (db.KeyExists("users","alice.name")) { /* ... */ }
```

---

### RemoveKey(string tableName, string key)
- 描述：从数据库删除指定键并从缓存中移除对应条目。
- 返回：bool
- 示例
```csharp
db.RemoveKey("users","alice.name");
```

---

### 复合数据操作（Composite）
- WriteComposite(string tableName, string key, Func<CompositeBuilder, CompositeBuilder> buildAction)
  - 描述：用 `CompositeBuilder` 构建一个复合二进制表示并以 Type='composite' 存入数据库。
  - 示例
  ```csharp
  var ok = db.WriteComposite("cfg","ui", b => b.Add("width", 1024).Add("height", 768));
  ```
- ReadComposite(string tableName, string key)
  - 描述：读取 Type='composite' 的键并解析为 `CompositeBuilder`。
  - 示例
  ```csharp
  var comp = db.ReadComposite("cfg","ui");
  var width = comp?.Get<int>("width");
  ```
- CompositeExists / RemoveComposite
  - 简单包装现有查询/删除方法。

备注：`CompositeBuilder` 的序列化格式为自定义二进制（详见下文）。

---

### ListKeys(string tableName)
- 描述：列出 `KeyValue` 表中该表所有键（排除 `__TABLE_META__`）。
- 返回：List<string>
- 示例
```csharp
var keys = db.ListKeys("users");
```

---

### 缓存与同步方法
- UpdateCacheSize(int newCacheSize)：修改 PRAGMA cache_size
- SyncCache()：执行 `PRAGMA wal_checkpoint(FULL);` 强制 WAL 检查点
- ReloadCache()：清空内存缓存
- CompareCacheWithDisk()：比较缓存与磁盘一致性（实现被省略，Assumption: 可能会遍历缓存并对比数据库中的 Value 字段）

使用建议：在进行大量更新后调用 `SyncCache()` 确保数据刷入磁盘；`ReloadCache()` 可用于测试或恢复时清空内存视图。

---

### Dump(bool writeBinary = false)
- 描述：输出数据库诊断信息（文件路径、page_count/page_size、每个逻辑表行数和近似大小），可选将文本和数据库二进制写入工作目录（`laststart.dump` / `laststart.db.bin`）。
- Assumption: 源码有较多实现被省略，请在使用前确认目标目录可写及写入行为。

---

### Dispose() / Close()
- Dispose 会调用 `SyncCache()`、关闭并释放 `_connection`、清空 `_cache` 并将 `_disposed = true`。调用后对象不可再使用。
- Close 只是调用 Dispose 的便捷方法。

---

## 辅助组件：CompositeBuilder（概要）

`CompositeBuilder` 用于在内存中构建键值对集合并序列化为自定义二进制格式，支持基本类型与 byte[]。

### 主要特性
- Add<T>(string key, T value)：添加键值对，构建后不可变（抛出异常）。
- Build(): 返回自定义二进制格式。
- Parse(byte[] data, string tableName = "", string key = ""): 从二进制解析回 CompositeBuilder。
- 支持类型：string, int, long, float, double, bool, byte[]，其它类型被 ToString() 为 string。

### 数据格式（实现说明）
- 起始分隔符 `@`（1 byte）
- 条目数量（4 bytes, Int32）
- 每个条目：
  - keyLength (2 bytes, UInt16) + keyBytes (UTF-8)
  - valueType (1 byte)
  - valueLength (4 bytes, Int32)
  - valueBytes
- 结束分隔符 `@`

示例（创建）：
```csharp
var b = new CompositeBuilder("cfg","ui");
b.Add("width", 800).Add("height",600);
var bytes = b.Build();
```

解析示例：
```csharp
var parsed = CompositeBuilder.Parse(bytes, "cfg","ui");
var h = parsed?.Get<int>("height");
```

Verified: `CompositeBuilder` 文件已附带实现，示例应能在项目中直接编译（需引用相应命名空间）。

---

## 使用示例（综合）

1) 简单读写
```csharp
using var db = new SqlitePersistence("appdata.db");
if (!db.CreateTable("users")) Console.WriteLine("表可能已存在");
db.WriteString("users","alice.name","Alice");
var name = db.ReadString("users","alice.name");
```

2) 复合数据
```csharp
using var db = new SqlitePersistence("appdata.db");
db.CreateTable("cfg");
db.WriteComposite("cfg","ui", b => b.Add("width",1024).Add("height",768));
var comp = db.ReadComposite("cfg","ui");
var width = comp?.Get<int>("width");
```

3) 强制落盘并关闭
```csharp
db.SyncCache();
db.Dispose();
```

---

## 并发、资源管理与线程安全

- `SqlitePersistence` 在类内部使用 `_connection` 与方法局部 `SqliteCommand`，并有 `_lock` 用于某些同步（例如 SyncCache）。
- SQLite 的 `SqliteConnection` 不是跨线程完全无锁的；建议每个线程使用独立实例或通过外层同步（例如锁）序列化访问。该类设计上保持一个共享连接，因此并发高时可能出现竞争或性能瓶颈。
- 建议：
  - 在高并发场景下，改为使用连接池（每次操作新建并释放连接）或外层用 `lock` 限制并发写入。
  - 长时间运行的实例应周期性调用 `SyncCache()` 以降低 WAL 文件增长。
- Dispose：必须在程序退出或不再使用时调用，Dispose 会关闭连接并清理缓存。

---

## 性能与安全注意事项

- 性能：大量写入应使用事务（当前实现每次写入都使用事务并 commit，这对单条写入是安全的，但批量写入建议外部合并为单次事务以提高吞吐）。
- 索引：当前实现在初始化创建了 `idx_table_key` 索引，但对于某些查询场景可能需要额外索引或分表设计。
- 类型安全：数据库中存储 `Type` 字段用于类型检查，但 `ReadValue<T>` 会对类型不匹配返回默认值，调用方应谨慎处理。
- SQL 注入：表名与列名在某些场景需要作为标识符插入 SQL 中，代码中提供了 `EscapeIdentifier(string)` 帮助转义内部双引号。仍需确保外部输入不能直接构成危险表达式（建议对表名进行白名单校验）。

---

## 故障排查 / 常见问题

- 问：写操作返回 false，且未抛异常？
  - 因为写方法内部捕获了异常并返回 false。检查应用日志或将方法调用包裹在 try/catch 中并记录异常。确认数据库文件权限、磁盘空间与只读模式设置。

- 问：ReadValue 返回 null 或默认值？
  - 可能是 `Type` 字段与期望类型不一致，或值为 NULL。请检查 `ListKeys()` 与 `Dump()` 输出以确认数据实际类型。

- 问：并发写入导致错误或性能下降？
  - 请降低并发写入，或改用连接池/批量事务。

- 问：Dump 写入失败？
  - 检查应用的当前工作目录与文件写入权限。若 `writeBinary=true` 会尝试写入数据库二进制，确保磁盘空间足够。

---

## 文件位置

- 源代码：`Library/Environments/SDK/Drx.Sdk.Network/V2/Persistence/Sqlite/SqlitePersistence.cs`
- 辅助组件：`CompositeBuilder.cs` 同目录
- 生成的 DEVGUIDE：`Library/Environments/SDK/Drx.Sdk.Network/V2/Persistence/Sqlite/SqlitePersistence.DEVGUIDE.md`

---

## 建议的后续步骤（Next Steps）

1. 在 `SqlitePersistence` 中补全省略的实现并移除吞掉异常的做法，改为记录（Logger）并在必要处抛出，增加可观测性。
2. 为关键方法添加 XML 注释，便于生成 API 文档。
3. 增加单元测试：
   - Happy path：Write/Read 各类型、CompositeBuilder Build/Parse。 (建议使用 xUnit 或 NUnit)
   - 边界：UpdateX 对不存在键的行为、只读模式下写操作抛出/返回 false。
   - 并发测试：并发写入/读取保证不会崩溃（并发场景可作为集成测试）。
4. 性能改进：考虑批量写入 API 或外部事务支持以提升吞吐量。
5. 安全改进：为外部提供的表名做白名单或验证，避免潜在标识符注入问题。

---

## 快速验证（如何运行 / 测试）

- 如何构建（在仓库根目录 `d:\Code`）：

```powershell
# 在 PowerShell 中运行（基础构建）
cd d:\Code
dotnet build DRX.Environment.sln
```

- 建议单元测试（示例）：
  - 新建 `Examples/SqlitePersistence.Tests` 使用 xUnit，编写测试覆盖读写/复合数据/只读模式行为。

---

## 质量门建议（Build / Lint / Tests）

- Build: 运行 `dotnet build`，确保项目能通过编译（PASS 条件：没有语法或引用错误）。
- Lint/Typecheck: 若有 Roslyn 分析器或 StyleCop，请在 CI 中开启。
- Tests: 添加至少 3 个单元测试（happy path + 两个边界/异常场景）。

---

## 最后说明

- 文档基于提供的源码片段与摘要生成；被省略的代码段已在 `Assumption:` 中标注，请在代码完整后复核文档中有标注的假设点。
- 若你希望我把文档拆分为两个文件（`SqlitePersistence.DEVGUIDE.md` 与 `CompositeBuilder.DEVGUIDE.md`），或自动生成对应的单元测试骨架，我可以继续执行这些改动。


<!-- End of DEVGUIDE -->

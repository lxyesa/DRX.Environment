# SqliteUnified.DEVGUIDE.md

## 概要
本文件为 `Drx.Sdk.Network.DataBase.Sqlite.SqliteUnified<T>` 的开发者指南（DEVGUIDE），用于说明该组件的目的、输入/输出契约、公开 API、使用示例、并发与性能注意点、常见问题与排障建议，以及推荐的测试与下一步工作。

组件位于：`Library/Environments/SDK/Drx.Sdk.Network/DataBase/Sqlite/SqliteUnified.cs`
主要功能：基于 `Microsoft.Data.Sqlite` 提供通用的对象关系持久化支持，自动创建/修复表结构，支持一对一/一对多/数组/字典/链表等字段的持久化，提供同步与异步操作。

> 说明：文档中保留了源代码中的类型/方法名不翻译。示例代码以 C# 为主，注释与说明为中文。

## 输入 / 输出 合同
- 输入（主要构造与方法参数）：
  - 构造器: `new SqliteUnified<T>(string databasePath, string? basePath = null)`
    - `databasePath`：相对或绝对的 SQLite 文件路径（相对于 `basePath` 或 AppDomain 基目录）。
    - `basePath`：可选，基础路径。
  - 公共方法常见参数：
    - `T entity`：实现 `IDataBase`（包含 `Id` 等） 的实体对象。
    - `int id`：实体主键 Id。
    - `string propertyName, object propertyValue`：基于属性名和值的查询/删除/更新条件。
    - `string condition`：任意 SQL WHERE 子句（调用者负责防注入）。
    - `CancellationToken cancellationToken`：用于异步方法取消。

- 输出：
  - 查询方法返回 `List<T>`、`T?`、或 `Task<>` 等，插入/更新/删除返回 `void`、`bool` 或受影响记录数（int）。

- 成功判据：
  - 数据能被写入到指定 SQLite 数据库文件，关系/子表自动创建，可通过 Query / QueryById / GetAll 等获取正确反序列化的对象。

- 错误模式：
  - 参数校验异常（如 null 实体或无效 propertyName）。
  - SQLite 操作异常（磁盘权限、锁、I/O 错误）。
  - SQL 注入风险（当调用者传入任意 condition 字符串）。

## 公开 API 概览
| 名称 | 签名 (摘要) | 描述 | 返回 | 常见错误/异常 |
|---|---|---:|---:|---|
| PublishAttribute | [Attribute] | 标记属性可被“发布/导出”的空 attribute | - | - |
| SqliteUnified<T> 构造器 | SqliteUnified(string databasePath, string? basePath = null) | 初始化并确保数据库/表结构 | - | IO/路径异常 |
| Push | void Push(T entity) | 将实体及其关联子表写入数据库（同步） | void | ArgumentNullException, SqliteException |
| PushAsync | Task PushAsync(T entity, CancellationToken) | 异步版本 | Task | 取消、SqliteException |
| Query | List<T> Query(string propertyName, object propertyValue) | 按属性查询 | List<T> | ArgumentException, SqliteException |
| QueryAsync | Task<List<T>> QueryAsync(string propertyName, object propertyValue, CancellationToken) | 异步查询 | Task<List<T>> | 同上 |
| QueryById / QueryByIdAsync | T? / Task<T?> | 按 Id 查单条 | T? / Task<T?> | SqliteException |
| Update / UpdateAsync | void / Task | 等同 Push | - | 同 Push |
| EditById | void EditById(int id, T entity) | 按 id 编辑（不修改 Id） | - | ArgumentException |
| EditWhere (by prop) | int EditWhere(string propertyName, object propertyValue, T entity) | 按属性更新匹配实体（返回更新数） | int | ArgumentException |
| EditWhere (condition) | int EditWhere(string condition, T entity) | 按任意 SQL 条件更新（调用者负责安全） | int | SQL 注入风险 |
| Delete / DeleteAsync | bool / Task<bool> | 删除实体（含子表） | bool/Task<bool> | SqliteException |
| DeleteById | bool DeleteById(int id) | 按 Id 删除 | bool | 同上 |
| DeleteWhere | int DeleteWhere(string propertyName, object propertyValue) | 按属性删除匹配实体 | int | ArgumentException |
| DeleteWhere(condition) | int DeleteWhere(string condition) | 任意 SQL 条件删除 | int | SQL 注入风险 |
| GetAll / GetAllAsync / QueryAllAsync | List<T>/Task<List<T>>/Task<IEnumerable<T>> | 获取全部 | 列表 | SqliteException |
| ClearAsync | Task ClearAsync(CancellationToken) | 异步清空表（含事务） | Task | InvalidOperationException, SqliteException |

> 注：表内还有若干私有/辅助方法（CreateTable、RepairTable、InsertChildEntity 等）用于具体实现，调用者无需直接使用。

## 方法逐个详解（选取关键方法）

### SqliteUnified(string databasePath, string? basePath = null)
- 参数
  - `databasePath` (string) — 相对于 `basePath` 的 sqlite 文件路径，或绝对路径。
  - `basePath` (string?) — 可选基础路径；若为 null，则使用 `AppDomain.CurrentDomain.BaseDirectory`。
- 行为
  - 确保目标目录存在。
  - 构建连接字符串并缓存 T 的属性信息（包括子表/集合类型）。
  - 调用 `InitializeDatabase()` 创建需要的表并调用 `RepairTable()` 自动修复缺失字段与索引。
- 异常
  - 可能抛出 IO/路径异常，或在数据库无法打开时抛出 `SqliteException`。
- 示例
```csharp
// 示例：创建 SqliteUnified 实例
var db = new SqliteUnified<MyData>("data/mydata.db");
```
- 备注
  - 构造器会立即访问文件系统并尝试打开/创建数据库文件，建议在有权限的目录中使用。

## 已核对的接口定义（来自仓库）
下面是当前仓库中 `IDataBase` 和 `IDataTable` 的真实定义（已从源码读取并核对）：

```csharp
// IDataBase.cs
namespace Drx.Sdk.Network.DataBase.Sqlite
{
  /// <summary>
  /// 表示数据库主表实体的基础接口
  /// </summary>
  public interface IDataBase
  {
    /// <summary>
    /// 主键ID
    /// </summary>
    int Id { get; set; }

    /// <summary>
    /// 获取表名。如果返回空或null，将使用类名作为表名。
    /// </summary>
    string TableName => null;
  }
}
```

```csharp
// IDataTable.cs
namespace Drx.Sdk.Network.DataBase.Sqlite
{
  /// <summary>
  /// 表示数据库关联子表的基础接口
  /// </summary>
  public interface IDataTable
  {
    /// <summary>
    /// 父表主键ID
    /// </summary>
    int ParentId { get; set; }

    /// <summary>
    /// 子表表名。如果返回空或null，将使用类名作为表名。
    /// </summary>
    string TableName { get; }
  }
}
```

说明：从源码可见，`IDataBase` 明确要求主表实体包含 `Id` 属性；而 `IDataTable` 接口并不在接口层声明 `Id`（仅声明 `ParentId` 与 `TableName`）。因此文档中关于子表是否包含 `Id` 的陈述已调整为“推荐在子表实体中实现 `Id` 以便建立主键/索引和便于更新”，但并非接口强制要求。

### void Push(T entity) / Task PushAsync(T entity, CancellationToken)
- 参数
  - `entity` (T) — 实体，必须实现 `IDataBase`（含 `Id`）。
- 行为
  - 为实体生成 Id（若为 0）。
  - 在事务中插入或更新主实体字段，并递归插入子表（实现 `IDataTable` 的属性、List/IDataTable、数组、字典、LinkedList 等）。
  - 提交事务或在异常时回滚。
- 错误模式
  - 参数为 null 抛 `ArgumentNullException`。
  - SQLite 操作失败会抛 `SqliteException`。
- 示例（同步）
```csharp
var item = new MyData { /* 填充字段 */ };
db.Push(item);
// Push 会将 item.Id 设置为非 0 的值（如果之前为 0）
```
- 示例（异步）
```csharp
await db.PushAsync(item, CancellationToken.None);
```
- 注意
  - 子对象的 Id 分配策略由实现细节决定（源代码中会在插入时为子实体赋 Id）。

### List<T> Query(string propertyName, object propertyValue) / Task<List<T>> QueryAsync(...)
- 参数
  - `propertyName` — 要匹配的属性名（T 的公有可读写属性之一）。
  - `propertyValue` — 要匹配的值（按等值匹配）。
- 返回
  - 匹配的实体列表（包含反序列化后的子表数据）。
- 异常/校验
  - `propertyName` 为空或不在 T 的属性字典中会抛 `ArgumentException`。
- 示例
```csharp
var results = db.Query("UserName", "alice");
```
- 性能注意
  - 查询会首先在主表上根据 ParentId/索引检索，然后查询子表；对频繁查询的字段推荐在 DB 层或外部建立合适索引（源代码会为 Id 和 ParentId 自动建立索引）。

### bool Delete(int id) / Task<bool> DeleteAsync(int id, CancellationToken)
- 行为
  - 删除主表中指定 Id 的记录，并清理所有关联的子表记录（事务性）。
- 返回
  - 是否成功删除（含事务提交）。
- 注意
  - 删除会级联清理一对多关系的子表代理数据（数组、字典代理表等）。

### int EditWhere(string condition, T entity)
- 风险提示
  - `condition` 是原生 SQL WHERE 子句（不含 WHERE），务必确保参数化或在调用端做好防注入处理。

## 高级话题与实现说明
- 表自动修复/创建：构造器会调用 `CreateTable` 与 `RepairTableForType`；会为 `Id` 建立唯一索引，为 `ParentId` 建立普通索引。
- 复杂集合持久化：
  - 简单类型数组/集合会被保存为代理表，字段为 `ParentId` 与 `Value`。
  - 复杂类型数组/集合（element 为复杂对象）会作为独立子表存储，带 `ParentId` 字段，子表表名根据元素类型或代理策略生成。
  - 字典会使用 `DictionaryEntrySurrogate` 代理（包含 `DictKey`/`DictValue` 字符串列），复杂字典值会作为另一个子表存储。
- 反射性能优化：实现中使用了静态缓存的 GetterCache/SetterCache、ChildTypePropertiesCache、ChildTypeFactoryCache 来降低运行时反射开销。

## 并发、事务与资源管理
- 事务：写操作（Push/Update/Delete/ClearAsync 等）在单个 SQLite 事务内执行，以保证一致性。
- 并发访问：SQLite 的并发能力有限（尤其是写操作）。建议：
  - 在高并发写场景下，引入外部并发控制（队列/锁/序列化写线程）。
  - 对于只读并发（Query/GetAll），SQLite 支持并行读，但仍依赖底层连接设置与 journaling 模式。
- 连接管理：实现中每次方法会创建 `SqliteConnection` 并在方法结束释放，确保不会长期占用连接句柄。

## 边界情况与注意事项（Edge cases）
- 空实体或必需字段缺失：Push / Update 会对 null 实体抛 `ArgumentNullException`。
- DateTime 存储/解析：实现中把 DateTime 转为字符串存储并提供解析方法，注意序列化一致性（时区/格式）。
- 带有循环引用或自引用的子对象：当前实现基于类型属性递归插入，未特别处理循环引用，可能导致无限递归或重复插入——调用方应避免或在模型中剔除循环引用。
- 枚举与布尔类型：实现包含枚举与 bool 到 SQLite 对应映射逻辑（bool 可能以 INTEGER 存储）。

## 使用示例（完整）
以下示例中我们假定存在简单的接口定义：
```csharp
// 说明：实际接口见上文“已核对的接口定义”。为便于示例，子表类型建议实现 Id（主键）以支持更新/索引，
// 但接口层中的 IDataTable 在仓库实现只要求 ParentId 和 TableName。
public interface IDataBase { int Id { get; set; } }
public interface IDataTable { int ParentId { get; set;} string TableName { get; } }

public class UserData : IDataBase
{
    public int Id { get; set; }
    public string UserName { get; set; }
    public int Age { get; set; }
    // 假设有一对多子表
    public List<Address> Addresses { get; set; } = new();
}
public class Address : IDataTable
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string City { get; set; }
    public string TableName => nameof(Address);
}
```
同步写入与查询：
```csharp
var db = new SqliteUnified<UserData>("data/myusers.db");
var u = new UserData { UserName = "alice", Age = 30 };
db.Push(u);
var users = db.Query("UserName", "alice");
```
异步示例：
```csharp
var db = new SqliteUnified<UserData>("data/myusers.db");
await db.PushAsync(u, CancellationToken.None);
var users = await db.QueryAsync("UserName", "alice", CancellationToken.None);
```
示例备注：上述示例在当前仓库未做编译验证（Not verified）。

## 排错 / 常见问题
- 无法打开数据库文件（SqliteException: unable to open database file）
  - 检查 `databasePath` 指向的目录是否存在且进程有写权限。
- 写入报错（database is locked）
  - 说明存在并发写或事务未正确提交。建议：减少并发写、使用单写队列或延长重试/回退策略。
- 查询返回空但表有数据
  - 检查 `propertyName` 是否为正确的列名，注意属性与列名大小写（实现以属性名为列名）。
- 字段新增后旧数据无法查询或异常
  - `RepairTable` 会在构造器运行时尝试添加缺失字段。如遇失败，检查数据库权限或 PRAGMA 兼容性。

## 文件位置
- 源文件：`Library/Environments/SDK/Drx.Sdk.Network/DataBase/Sqlite/SqliteUnified.cs`
- 本文档：`Library/Environments/SDK/Drx.Sdk.Network/DataBase/Sqlite/SqliteUnified.DEVGUIDE.md`

## 建议的单元测试（最小集合）
1. Happy path - Push & Query
   - 创建临时数据库文件，Push 一个实体（含子表），QueryById 验证字段与子表数量。
2. 异步 API 覆盖
   - PushAsync + QueryAsync 的基本流程与 CancellationToken 中断路径。
3. 字段迁移/RepairTable
   - 在已有数据库上新增一个新字段，构造 `SqliteUnified<T>` 后验证列被自动添加。
4. 并发写入压力测试（集成测试）
   - 多线程/多任务并发调用 Push，观察是否产生锁或异常。

## 质量门（Quick quality gates）
- Build: 使用 `dotnet build DRX.Environment.sln` 验证构建（建议在 CI 中运行）。
- Lint/Typecheck: 项目采用 .NET 9，建议使用 Roslyn 分析器进行静态检查。
- Tests: 上述单元/集成测试需要在 CI 中执行并作为合并门禁。

## 下一步与改进建议
- 在 `Push` / `PushAsync` 中为大量子表插入加入批量插入或 prepared statement 的复用以提高性能。
- 为写操作添加可配置的重试策略与退避（database locked 场景）。
- 增加对循环引用模型的检测与友好报错。
- 将示例单元测试加入 `Examples/` 并在 CI 中自动运行。
- 为 `DictionaryEntrySurrogate` 的表名生成策略添加可配置化选项。

## 假设与待核实项（Assumptions / Verified items）
- 已核实：`IDataBase` 在仓库中定义包含 `Id` 属性；`IDataTable` 在接口层定义 `ParentId` 与 `TableName`（接口本身未定义 `Id`）。
- 建议：子表实体实现 `Id` 属性（推荐用于主键/索引与便捷更新），虽然接口层不强制要求。
- 假设：DateTime 的持久化以字符串形式一致序列化，使用时注意时区与格式一致性（如需严格格式，可在调用方或库中统一格式化）。

---

如果你希望，我可以：
- 将本 DEVGUIDE 文件提交到仓库（已完成）并在 CI 下运行一次 `dotnet build` 验证；
- 基于上述 "建议的单元测试" 创建一个最小的 xUnit 测试文件并运行一次测试。
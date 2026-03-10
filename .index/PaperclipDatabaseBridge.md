# DatabaseBridge
> SQLite 数据库脚本桥接层，提供原始 SQL 操作 + 便利 CRUD/DDL

## Classes
| 类名 | 简介 |
|------|------|
| `DatabaseBridge` | 静态类，通过连接字符串句柄提供 SQLite 原始 SQL 操作与便利封装 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `open(databasePath)` | `databasePath:string` | `string` | 打开/创建数据库，返回连接字符串句柄 |
| `execute(connStr, sql, params?)` | `connectionString:string, sql:string, parameters:object?` | `int` | 执行非查询 SQL |
| `query(connStr, sql, params?)` | `connectionString:string, sql:string, parameters:object?` | `object[]` | 查询返回动态对象数组 |
| `scalar(connStr, sql, params?)` | `connectionString:string, sql:string, parameters:object?` | `object?` | 查询单个标量值 |
| `transaction(connStr, sqls)` | `connectionString:string, sqlStatements:string[]` | `void` | 事务执行多条 SQL |
| `tables(connStr)` | `connectionString:string` | `string[]` | 获取所有表名 |
| `close(connStr)` | `connectionString:string` | `void` | 清理连接池 |
| `queryOne(connStr, sql, params?)` | `connectionString:string, sql:string, parameters:object?` | `object?` | 查询单行，无结果返回 null |
| `count(connStr, table, where?, params?)` | `connectionString:string, table:string, where:string?, parameters:object?` | `long` | 快速计数 |
| `exists(connStr, table, where, params?)` | `connectionString:string, table:string, where:string, parameters:object?` | `bool` | 行是否存在 |
| `insert(connStr, table, data)` | `connectionString:string, table:string, data:object` | `long` | 对象直接插入，返回 rowid |
| `insertBatch(connStr, table, items)` | `connectionString:string, table:string, items:object[]` | `int` | 事务批量插入 |
| `update(connStr, table, data, where, params?)` | `connectionString:string, table:string, data:object, where:string, parameters:object?` | `int` | 按条件更新 |
| `deleteWhere(connStr, table, where, params?)` | `connectionString:string, table:string, where:string, parameters:object?` | `int` | 按条件删除 |
| `upsert(connStr, table, data, conflictCols)` | `connectionString:string, table:string, data:object, conflictColumns:string[]` | `long` | INSERT OR UPDATE |
| `columns(connStr, table)` | `connectionString:string, table:string` | `object[]` | 获取表列信息 |
| `createTable(connStr, table, colDefs)` | `connectionString:string, table:string, columnDefs:object[]` | `void` | 便捷建表 |
| `dropTable(connStr, table)` | `connectionString:string, table:string` | `void` | 删除表 |
| `addColumn(connStr, table, col, type, default?)` | `connectionString:string, table:string, columnName:string, columnType:string, defaultValue:object?` | `void` | 添加列 |
| `createIndex(connStr, table, cols, unique?)` | `connectionString:string, table:string, columnNames:string[], unique:bool` | `void` | 创建索引 |

## Usage
```typescript
const db = Database.open("data.db");

// DDL
Database.createTable(db, "users", [
  { name: "id", type: "INTEGER", primaryKey: true },
  { name: "name", type: "TEXT", notNull: true },
  { name: "age", type: "INTEGER" }
]);
Database.createIndex(db, "users", ["name"], true);

// 便利 CRUD
const id = Database.insert(db, "users", { name: "Alice", age: 30 });
Database.insertBatch(db, "users", [
  { name: "Bob", age: 25 },
  { name: "Carol", age: 28 }
]);
Database.update(db, "users", { age: 31 }, "name = @name", { name: "Alice" });
Database.upsert(db, "users", { name: "Alice", age: 32 }, ["name"]);
Database.deleteWhere(db, "users", "age < @min", { min: 18 });

// 便利查询
const user = Database.queryOne(db, "SELECT * FROM users WHERE id = @id", { id: 1 });
const total = Database.count(db, "users");
const hasAlice = Database.exists(db, "users", "name = @n", { n: "Alice" });
const cols = Database.columns(db, "users");

// 原始 SQL
const rows = Database.query(db, "SELECT * FROM users WHERE age > @min", { min: 20 });
Database.close(db);
```

# JsonBridge
> JSON 序列化脚本桥接层

## Classes
| 类名 | 简介 |
|------|------|
| `JsonBridge` | 静态类，提供 .NET 侧 JSON 序列化/反序列化与文件读写 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `stringify(value, pretty?)` | `value:object?, pretty:bool` | `string` | 序列化为 JSON 字符串 |
| `parse(json)` | `json:string` | `object?` | 反序列化为动态对象 |
| `readFile(filePath)` | `filePath:string` | `object?` | 读取 JSON 文件并解析 |
| `writeFile(filePath, value, pretty?)` | `filePath:string, value:object?, pretty:bool` | `void` | 序列化并写入文件 |

## Usage
```typescript
const obj = Json.parse('{"name":"Alice","age":30}');
const str = Json.stringify(obj, true);
Json.writeFile("config.json", { port: 8080, host: "localhost" });
const config = Json.readFile("config.json");
```

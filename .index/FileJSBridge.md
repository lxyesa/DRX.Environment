# FileJSBridge
> File 常用静态文件 API 的 JavaScript 桥接。

## Classes
| 类名 | 简介 |
|------|------|
| `FileJSBridge` | 暴露 `File` 常用文件操作给脚本侧。 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `exists(path)` | `path:string` | `bool` | 判断文件是否存在。 |
| `readAllText(path, encodingName?)` | `path:string, encodingName?:string` | `string` | 读取文本内容。 |
| `writeAllText(path, content, encodingName?)` | `path:string, content:string, encodingName?:string` | `void` | 写入文本（自动建目录）。 |
| `appendAllText(path, content, encodingName?)` | `path:string, content:string, encodingName?:string` | `void` | 追加文本（自动建目录）。 |
| `readAllBytes(path)` | `path:string` | `byte[]` | 读取二进制内容。 |
| `writeAllBytes(path, data)` | `path:string, data:byte[]` | `void` | 写入二进制（自动建目录）。 |
| `delete(path)` | `path:string` | `void` | 删除文件（存在时）。 |
| `copy(sourceFileName, destFileName, overwrite?)` | `string, string, bool` | `void` | 复制文件。 |
| `move(sourceFileName, destFileName, overwrite?)` | `string, string, bool` | `void` | 移动文件。 |

## Usage
```csharp
// JS 侧
// File.writeAllText("./a.txt", "hello")
// const text = File.readAllText("./a.txt")
```

# FileStreamJSBridge
> FileStream 常用流打开/读写/定位 API 的 JavaScript 桥接。

## Classes
| 类名 | 简介 |
|------|------|
| `FileStreamJSBridge` | 暴露 `FileStream` 常见流控制方法给脚本侧。 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `openRead(path)` | `path:string` | `FileStream` | 只读打开文件流。 |
| `openWrite(path)` | `path:string` | `FileStream` | 写入打开或创建文件流。 |
| `openAppend(path)` | `path:string` | `FileStream` | 追加写入打开文件流。 |
| `readBytes(stream, count)` | `stream:FileStream, count:int` | `byte[]` | 读取指定字节数。 |
| `writeBytes(stream, data)` | `stream:FileStream, data:byte[]` | `void` | 写入字节数组。 |
| `readToEnd(stream, encodingName?)` | `stream:FileStream, encodingName?:string` | `string` | 从当前位置读到末尾。 |
| `writeText(stream, content, encodingName?)` | `stream:FileStream, content:string, encodingName?:string` | `void` | 文本写入并刷新。 |
| `getPosition(stream)` | `stream:FileStream` | `long` | 获取当前位置。 |
| `setPosition(stream, position)` | `stream:FileStream, position:long` | `void` | 设置当前位置。 |
| `getLength(stream)` | `stream:FileStream` | `long` | 获取流长度。 |
| `flush(stream)` | `stream:FileStream` | `void` | 刷新缓冲区。 |
| `close(stream)` | `stream:FileStream` | `void` | 关闭并释放流。 |

## Usage
```csharp
// JS 侧
// const fs = FileStream.openWrite("./a.txt")
// FileStream.writeText(fs, "hello")
// FileStream.close(fs)
```

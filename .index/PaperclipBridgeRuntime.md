# PaperclipBridgeRuntime
> Paperclip 运行时释放到脚本目录的 JS 桥接模板文件。它为桥接器对象提供 JS 友好占位与 JSDoc 注释，提升脚本编写体验。

## Files
| 文件 | 简介 |
|------|------|
| `Models/paperclip.bridges.js` | 为 `console`、`Logger/logger`、`File`、`FileStream` 提供预置方法壳（缺失时抛错），便于 IDE 提示与运行时诊断。 |

## APIs
| 对象 | 主要方法 |
|------|----------|
| `console` | `log/info/debug/warn/error` 及 `*Async` |
| `Logger` / `logger` | `log/info/debug/warn/error` 及 `*Async` |
| `File` | `exists/readAllText/writeAllText/appendAllText/readAllBytes/writeAllBytes/delete/copy/move` 及 `*Async` |
| `FileStream` | `openRead/openWrite/openAppend/readBytes/writeBytes/readToEnd/writeText/getPosition/setPosition/getLength/flush/close` 及 `*Async` |

## Usage
```javascript
// main.js 顶部可选引入（按宿主能力）
// load('paperclip.bridges.js')

Logger.info('hello');
if (File.exists('./a.txt')) {
  console.log(File.readAllText('./a.txt'));
}
```

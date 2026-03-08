# paperclip.module.fs.js
> Paperclip 文件系统能力模块，挂载 `Paperclip.fs`。

## APIs
| API | 说明 |
|-----|------|
| `exists/readText/writeText/appendText` | 文本读写。 |
| `readBytes/writeBytes` | 字节读写。 |
| `remove/copyTo/moveTo` | 文件删除/复制/移动。 |

## Usage
```javascript
const { fs } = Paperclip.use();
fs.writeText('./a.txt', 'ok');
```

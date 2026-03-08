# paperclip.module.stream.js
> Paperclip 流能力模块，挂载 `Paperclip.stream`。

## APIs
| API | 说明 |
|-----|------|
| `openRead/openWrite/openAppend` | 打开文件流。 |
| `readBytes/writeBytes/readToEnd/writeText` | 流读写。 |
| `getPosition/setPosition/getLength/flush/close` | 流状态管理。 |

## Usage
```javascript
const { stream } = Paperclip.use();
const s = stream.openWrite('./x.txt');
stream.writeText(s, 'hello');
stream.close(s);
```

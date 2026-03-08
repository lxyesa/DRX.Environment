# paperclip.module.log.js
> Paperclip 日志能力模块，挂载 `Paperclip.log`。

## APIs
| API | 说明 |
|-----|------|
| `Paperclip.log.info/warn/error/debug/log` | 同步日志输出。 |
| `Paperclip.log.*Async` | 异步日志输出。 |

## Usage
```javascript
const { log } = Paperclip.use();
log.info('hello');
```

# paperclip.module.js
> Paperclip 单文件 SDK 入口。内置桥接兜底、全局命名空间、模块注册器，以及 log/fs/stream/dotnet 子模块实现。

## Classes
| 类名 | 简介 |
|------|------|
| `Paperclip` | 全局 SDK 命名空间，包含日志与文件系统封装。 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `Paperclip.registerModule(name, moduleValue)` | `string, any` | `any` | 注册子模块并同步到 `Paperclip.<name>`。 |
| `Paperclip.use(names?)` | `string[]?` | `object` | 按需返回模块集合；未传时返回全部已注册模块。 |
| `Paperclip.requireMethod(api, method)` | `string, string` | `Function` | 构建带错误保护的桥接调用包装器。 |
| `Paperclip.getModule(name)` | `string` | `any` | 获取已注册模块实例。 |

## Built-in Modules
| 模块 | 简介 |
|------|------|
| `log` | 日志桥接封装，提供 `trace/success/fatal` 等 JS 别名。 |
| `fs` | 文件系统 API，提供 `readFile/writeFile/deleteFile` 等友好命名并兼容旧接口。 |
| `stream` | 文件流 API，提供 `createReadStream/position/length` 等友好命名并兼容旧接口。 |
| `dotnet` | .NET 类型访问分组（`io/text/runtime/...`）与 `type/requireType` 动态解析。 |

## Usage
```javascript
// 方式1: 全局 API（legacy + module runtime 均可）
const { log, fs, dotnet } = Paperclip.use(['log', 'fs', 'dotnet']);

log.info('start');
fs.writeText('./demo.txt', 'hello');

const Path = dotnet.io.Path();
log.info(Path.Combine('.', 'demo.txt'));
```

```typescript
// 方式2: 包式 import（Module Runtime 下）
import { log, fs, dotnet } from '@paperclip/sdk';
// 或
import sdk from '@paperclip/sdk';
sdk.use(['log']);
```

## @paperclip/sdk 包入口
- 文件末尾的导出层 IIFE 构造 namespace 对象，包含 `{ default, Paperclip, log, fs, stream, dotnet, use, registerModule, createModule, getModule, requireMethod, version }`。
- 全局预加载（`ExecuteFile`）时导出层返回值被忽略，不影响现有行为。
- 模块加载器 `Execute(source)` 时，最终表达式值即为 SDK namespace。

## TypeScript
- 类型声明文件：`Models/TypeScript/paperclip.module.d.ts`
- 全局声明：`declare global { ... const Paperclip: PaperclipNamespace; }`
- 模块声明：`declare module "@paperclip/sdk" { ... }` — 与运行时导出层一致。
- 在 `tsconfig.json` 已包含 `Models/**/*.d.ts` 时，全局 API 与包式 import 均获得类型提示。

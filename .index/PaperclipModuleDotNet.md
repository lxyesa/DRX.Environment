# paperclip.module.dotnet.js
> Paperclip .NET 直连模块，挂载 `Paperclip.dotnet`。

## APIs
| API | 说明 |
|-----|------|
| `dotnet.type(fullName)` | 动态解析任意 .NET 类型（host.type）。 |
| `dotnet.io.*` | `File/Directory/Path/FileInfo/DirectoryInfo` 类型入口。 |
| `dotnet.text.*` | `Encoding/StringBuilder/JsonSerializer` 类型入口。 |
| `dotnet.runtime.*` | `Environment/DateTime/Guid/TimeSpan/Convert/Math` 类型入口。 |

## Usage
```javascript
const { dotnet } = Paperclip.use();
const Path = dotnet.io.Path();
const p = Path.Combine('.', 'a.txt');
```

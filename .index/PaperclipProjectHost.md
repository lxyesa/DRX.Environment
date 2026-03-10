# ProjectHost
> Paperclip `project` 子命令处理器，负责项目创建与删除。

## Classes
| 类名 | 简介 |
|------|------|
| `ProjectHost` | 创建 `project.json` / `main.ts`，并删除指定项目目录。 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `LoadEmbeddedGlobalDts()` | (无) | `string` | 从程序集嵌入资源读取 global.d.ts 内容。 |
| `LoadEmbeddedResource(logicalName)` | `string` | `string` | 从程序集嵌入资源读取指定模板文件内容。 |
| `CreateProject(projectName)` | `string` | `void` | 创建项目目录并写入初始文件。 |
| `CreateHttpProject(projectName)` | `string` | `void` | 创建 HTTP 服务器模板项目（main.ts + routes + public/index.html）。 |
| `DeleteProject(projectName)` | `string` | `void` | 递归删除项目目录（安全校验后）。 |

## Usage
```csharp
ProjectHost.CreateProject("demo");
ProjectHost.DeleteProject("demo");
```

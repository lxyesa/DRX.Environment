# PaperclipScriptHost

## File
`Library/Applications/Clients/Paperclip/Hosting/Runtime/ScriptHost.cs`

## Namespace
`DrxPaperclip.Hosting`

## Purpose
脚本执行器，处理 `run` 子命令：检测入口文件（文件/目录）、TypeScript 转译、模块图加载或直接执行，返回退出码。

## Class: `ScriptHost`
- **Static class**

### Methods
| Method | Signature | Purpose |
|--------|-----------|---------|
| `Run` | `static int Run(PaperclipOptions options, EngineBootstrap bootstrap)` | 执行脚本并返回退出码 |
| `DetectEntryPoint` | `private static string DetectEntryPoint(string path)` | 检测入口：文件直接使用；目录则查找 project.json main / index.js / index.ts |
| `IsTypeScript` | `private static bool IsTypeScript(string filePath)` | 判断文件是否为 TypeScript（.ts/.mts/.cts） |

### Run() 执行流程
1. `DetectEntryPoint(options.ScriptPath)` → 确定入口文件
2. 若入口为 TypeScript → `JavaScript.TranspileTypeScriptFile(path, projectRoot)` 转译
   - 若 `bootstrap.TranspileCache` 可用，先尝试命中缓存；miss 时再转译并回写缓存
3. `options.NoModules` 为 true → `engine.Execute(source)` 或 `engine.ExecuteFile(path)` 直接执行
4. `options.NoModules` 为 false → `moduleLoader.LoadModuleGraph(entryPath, executeModule)` 模块模式

### DetectEntryPoint() 规则
1. 路径是文件 → 直接返回
2. 路径是目录：
   a. 检查 `project.json` → 读取 `main` 字段
   b. 检查 `index.js` / `index.ts` / `index.mjs`
   c. 均无 → 抛出 FileNotFoundException

## Dependencies
- `DrxPaperclip.Cli.PaperclipOptions`
- `DrxPaperclip.Hosting.EngineBootstrap`
- `Drx.Sdk.Shared.JavaScript.JavaScript` (TranspileTypeScriptFile)
- `Drx.Sdk.Shared.JavaScript.Engine.ModuleLoader` (LoadModuleGraph)
- `Drx.Sdk.Shared.JavaScript.Abstractions.IJavaScriptEngine` (Execute/ExecuteFile)

## Spec Reference
- **Requirements**: FR-1, FR-2, FR-3
- **Design**: §2.3 ScriptHost

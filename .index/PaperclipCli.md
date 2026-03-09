# PaperclipCli

## PaperclipOptions.cs
- **Path**: `Library/Applications/Clients/Paperclip/Cli/PaperclipOptions.cs`
- **Namespace**: `DrxPaperclip.Cli`
- **Type**: `sealed class`
- **Purpose**: CLI 参数解析结果的纯数据持有类，不含任何业务逻辑或验证。

### Public Properties
| Property | Type | Default | Description |
|---|---|---|---|
| `ScriptPath` | `string?` | `null` | run 子命令的目标路径 |
| `IsRepl` | `bool` | `false` | 是否为 repl 子命令 |
| `IsHelp` | `bool` | `false` | --help 标志 |
| `IsVersion` | `bool` | `false` | --version 标志 |
| `Debug` | `bool` | `false` | --debug 诊断输出标志 |
| `NoModules` | `bool` | `false` | --no-modules 禁用模块系统 |
| `TypeScriptPath` | `string?` | `null` | --typescript-path 自定义 TS 编译器路径 |
| `AllowPaths` | `List<string>` | `[]` | --allow-path 安全白名单目录（可多次） |
| `PluginPaths` | `List<string>` | `[]` | --plugin 插件 DLL 路径（可多次） |
| `ErrorMessage` | `string?` | `null` | 解析错误信息，非 null 表示参数解析失败 |

### Spec Traceability
- **Requirement**: FR-5 (CLI 参数体系)
- **Design**: §2.1 PaperclipOptions structure

---

## CliParser.cs
- **Path**: `Library/Applications/Clients/Paperclip/Cli/CliParser.cs`
- **Namespace**: `DrxPaperclip.Cli`
- **Type**: `static class`
- **Purpose**: 手写 CLI 参数解析器，将 `string[] args` 转换为 `PaperclipOptions`。

### Public Methods
| Method | Signature | Description |
|---|---|---|
| `Parse` | `static PaperclipOptions Parse(string[] args)` | 解析命令行参数，返回填充完毕的选项对象 |

### Parsing Rules
1. 无参数 → `IsHelp = true`
2. 第一个非 `-` 参数识别子命令：`run` / `repl`
3. `run` 后下一个非 `-` 参数为 `ScriptPath`
4. 非 `run`/`repl` 的首个参数视为 `run <path>` 简写
5. `--key value` 配对解析（`--allow-path`、`--plugin`、`--typescript-path`）
6. 布尔标志：`--help`/`-h`、`--version`/`-v`、`--debug`、`--no-modules`
7. 未识别参数 → 设置 `ErrorMessage` 返回
8. 缺少必需值 → 设置 `ErrorMessage` 返回

### Spec Traceability
- **Requirement**: FR-5 (CLI 参数体系)
- **Design**: §2.1 CliParser 解析逻辑

---

## HelpText.cs
- **Path**: `Library/Applications/Clients/Paperclip/Cli/HelpText.cs`
- **Namespace**: `DrxPaperclip.Cli`
- **Type**: `static class`
- **Purpose**: 帮助文本与版本号常量，纯常量无逻辑。

### Public Methods
| Method | Signature | Description |
|---|---|---|
| `GetHelpText` | `static string GetHelpText()` | 返回完整使用说明，包含所有子命令和选项 |
| `GetVersion` | `static string GetVersion()` | 返回版本字符串 |

### Help Text Coverage
- 子命令：`run <path>`、`repl`
- 选项：`--help`/`-h`、`--version`/`-v`、`--debug`、`--no-modules`、`--allow-path <dir>`、`--plugin <path>`、`--typescript-path <dir>`

### Spec Traceability
- **Requirement**: FR-5 (CLI 参数体系) — `paperclip --help` 显示所有子命令和参数
- **Design**: §2.1 CLI interface spec

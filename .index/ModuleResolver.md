# ModuleResolver
> Module Runtime 解析器：对 specifier 进行 deterministic 分类与解析，支持 builtin/relative/absolute/workspace imports map/node_modules 裸包解析，并产出标准化解析错误与解析来源。

## Classes
| 类名 | 简介 |
|------|------|
| `ModuleResolver` | 解析入口与静态导入解析流水线，支持 builtin/relative/absolute/workspace imports map/node_modules。 |
| `ModuleResolutionResult` | 解析结果模型，包含 kind、resolved path、attempts。 |
| `ModuleResolutionException` | 标准化解析异常，包含 code/specifier/from/attempts/reason。 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `Resolve(specifier, fromFilePath)` | `string, string?` | `ModuleResolutionResult` | 分类并按固定顺序解析单个 specifier，若注入了 `ImportSecurityPolicy` 则在解析成功后自动校验安全边界（含符号链接）。 |
| `ResolveForEntry(entryFilePath)` | `string` | `ModuleResolutionResult` | 解析入口文件（绝对路径）。 |
| `ResolveStaticImportsRecursively(entryFilePath)` | `string` | `IReadOnlyList<ModuleResolutionResult>` | 递归扫描并解析静态导入，保持稳定顺序。 |
| `Classify(specifier)` | `string` | `ModuleSpecifierKind` | specifier 分类：builtin/relative/absolute/bare。 |
| `ResolveNodeModulesPackage(specifier, fromFilePath, attempts)` | `string, string?, List<string>` | `ModuleResolutionResult?` | Node-style node_modules 逐级向上查找裸包，解析 package.json main 与 index 入口。 |
| `ResolvePackageEntry(packageDir, specifier, fromFilePath, attempts)` | `string, string, string?, List<string>` | `ModuleResolutionResult?` | 读取 package.json 解析 exports/conditions → module → main → index.* 入口。 |
| `ResolvePackageExports(exportsElement, packageDir, specifier, subpath, fromFilePath, conditions, attempts)` | `JsonElement, string, string, string?, string?, string[], List<string>` | `ModuleResolutionResult?` | 解析 package.json exports 字段，支持字符串/对象/子路径条件映射，deterministic fallback。 |
| `ResolveExportsConditions(conditionsObj, packageDir, specifier, fromFilePath, conditions, attempts)` | `JsonElement, string, string, string?, string[], List<string>` | `ModuleResolutionResult?` | 按优先级遍历条件对象（import/require/default），记录匹配与跳过过程。 |
| `SplitBareSpecifier(specifier)` | `string` | `(string packageName, string? subpath)` | 拆分裸包名与子路径（支持 @scope/pkg/sub）。 |

## Usage
```csharp
var options = ModuleRuntimeOptions.CreateSecureDefaults(projectRoot);
options.AllowNodeModulesResolution = true;

var policy = new ImportSecurityPolicy(options);
var resolver = new ModuleResolver(options, new Dictionary<string, string>
{
    ["@paperclip/sdk"] = sdkModuleFilePath
});

var entry = resolver.ResolveForEntry(entryPath);
var imports = resolver.ResolveStaticImportsRecursively(entryPath);
// 裸包 import 'lodash' → node_modules/lodash/package.json → exports → module → main → index.*
// exports 条件：import > require > default（deterministic），命中过程全程记录在 Attempts 中
// 子路径：import 'lodash/fp' → exports["./"+"fp"] 条件匹配 → 或回退直接路径（仅无 exports 时）
```

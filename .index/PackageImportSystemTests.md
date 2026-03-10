# PackageImportSystemTests
> Package Import System 的测试工程文档（xUnit + FluentAssertions），覆盖解析、缓存、互操作、安全、诊断与集成性能验证。

## Classes
| 类名 | 简介 |
|------|------|
| `ModuleResolverTests` | 模块解析行为与错误路径测试 |
| `ModuleCacheTests` | 缓存命中、single-flight、失效与统计测试 |
| `InteropBridgeTests` | ESM/CJS 互操作桥接测试 |
| `ImportSecurityPolicyTests` | 路径白名单与安全策略测试 |
| `ModuleRecordTests` | ModuleRecord 状态机与转换测试 |
| `ModuleDiagnosticsTests` | 诊断事件与摘要输出测试 |
| `ModuleLoaderIntegrationTests` | 模块加载器集成场景测试 |
| `PerformanceBenchmarkTests` | 性能基准与回归阈值测试 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `InvalidateDependents_ShouldKeepRoot_AndInvalidateDependentsOnly()` | `-` | `void` | 验证仅级联失效依赖者，不失效根模块 |
| `InvalidateWithDependents_ShouldInvalidateRootAndDependents_KeepUnrelated()` | `-` | `void` | 验证根+依赖者同时失效且不影响无关模块 |
| `InvalidateWithDependents_CycleGraph_ShouldNotLoop()` | `-` | `void` | 验证循环依赖图失效不会死循环 |

## Usage
```csharp
// 在仓库根目录执行
// dotnet test Examples/PackageImportSystemTests/PackageImportSystemTests.csproj
```

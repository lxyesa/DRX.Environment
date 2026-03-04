# 资产管理验收测试

## 概述

本测试项目为 `developer-center-asset-management-permission-zero` 规范提供全面的验收测试覆盖。

## 测试覆盖范围

### 7.1 权限矩阵测试 (`PermissionMatrixTests.cs`)
- ✅ T1.1-T1.4: 各权限组(0/2/3/999)对 system-only API 的访问控制
- ✅ T1.5: 前端 Tab 可见性逻辑验证
- ✅ isSystem vs isAdmin 权限边界验证

### 7.2 状态流转测试 (`StateTransitionTests.cs`)
- ✅ T2.1-T2.2: 退回（return）合法/非法流转
- ✅ T2.3-T2.4: 下架（off-shelf）合法/非法流转
- ✅ T2.5-T2.6: 强制重审（force-review）合法/非法流转
- ✅ T2.7-T2.8: 恢复上架（relist）合法/非法流转
- ✅ T2.9: 审核通过后状态为 ApprovedPendingPublish(2) 验证
- ✅ 下架与软删除独立性验证
- ✅ 完整状态流转矩阵测试

### 7.3 禁改字段与审计记录测试 (`FieldBlacklistAndAuditTests.cs`)
- ✅ T3.1: 禁止修改 id/assetId 字段
- ✅ T3.2: 禁止修改 authorId/developerId 字段
- ✅ T3.3: 允许修改其他业务字段
- ✅ T3.4: 字段更新审计记录生成
- ✅ T3.5: 动作操作审计记录生成
- ✅ T3.6: 失败操作错误上下文保留
- ✅ T3.7: 审计记录按资产ID查询

### 回归测试 (`DeveloperCenterRegressionTests.cs`)
- ✅ 原有三大功能（我的资源/提交资源/审核管理）不退化
- ✅ Admin API 向后兼容
- ✅ System API 独立命名空间
- ✅ 权限收敛范围限定验证
- ✅ Tab 可见性矩阵验证

### 集成测试 (`EndToEndIntegrationTests.cs`)
- 🔲 E2E 权限测试（需要运行中的服务器）
- 🔲 E2E 状态流转测试
- 🔲 E2E 字段更新测试
- 🔲 E2E 审计查询测试

## 运行测试

### 前置条件
- .NET 7.0 SDK
- 项目已编译成功

### 运行所有单元测试
```bash
cd Examples/AssetManagementAcceptanceTests
dotnet test
```

### 运行特定测试类
```bash
# 权限矩阵测试
dotnet test --filter "FullyQualifiedName~PermissionMatrixTests"

# 状态流转测试
dotnet test --filter "FullyQualifiedName~StateTransitionTests"

# 字段与审计测试
dotnet test --filter "FullyQualifiedName~FieldBlacklistAndAuditTests"

# 回归测试
dotnet test --filter "FullyQualifiedName~DeveloperCenterRegressionTests"
```

### 运行集成测试（需要服务器）
```bash
# 先启动 KaxSocket 服务器，然后：
dotnet test --filter "Category=Integration"
```

### 排除集成测试
```bash
dotnet test --filter "Category!=Integration"
```

## 验收标准对照

| 验收标准 | 测试覆盖 |
|---------|---------|
| 1. 权限组0可见可操作，2/3/999不可见且API被拒绝 | PermissionMatrixTests |
| 2. 审核通过后状态为2(待发布) | StateTransitionTests.T2.9 |
| 3. 下架进入独立状态4，不修改IsDeleted | StateTransitionTests.OffShelf_ShouldBeIndependentFromSoftDelete |
| 4. DeveloperId/AssetId不可修改 | FieldBlacklistAndAuditTests.T3.1-T3.2 |
| 5. 退回/重审/强制更新可用且写入审计 | FieldBlacklistAndAuditTests.T3.5 |
| 6. 非法状态流转被阻断 | StateTransitionTests 各非法流转测试 |

## 需求映射

- **R1**: 页面入口与可见性 → PermissionMatrixTests, DeveloperCenterRegressionTests
- **R3**: 资产字段管理 → FieldBlacklistAndAuditTests
- **R5**: 状态流转 → StateTransitionTests
- **R6**: 审计与追踪 → FieldBlacklistAndAuditTests
- **R7**: 兼容与迁移 → DeveloperCenterRegressionTests

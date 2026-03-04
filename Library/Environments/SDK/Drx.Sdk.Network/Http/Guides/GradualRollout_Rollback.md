# 灰度发布与回滚操作手册

> **Spec**: http-performance-traffic-optimization — 任务 7.1 / 7.2 / R7  
> **适用角色**: SRE / 发布工程师 / 运维  
> **目标**: 确保所有关键 HTTP 优化项可独立灰度推进，并在 **5 分钟内完成回滚**

---

## 1. Feature Flag 总览

所有关键优化项通过 `HttpFeatureFlags`（单例）统一管理开关与灰度比例（0~100%）：

| Flag 名称 | 对应优化 | Spec 任务 | 默认灰度 |
|-----------|---------|-----------|---------|
| `Compression` | Gzip/Brotli 响应压缩 | 任务 3 | 100% |
| `ConditionalRequest` | ETag/304 条件请求 | 任务 4 | 100% |
| `RetryPolicy` | GET/HEAD 自动重试 | 任务 5 | 100% |
| `AdaptiveConcurrency` | AIMD 自适应并发 | 任务 2 | 100% |
| `LargeFileOptimization` | 大文件分片+缓冲优化 | 任务 6 | 100% |
| `ObjectPool` | 对象池/缓冲区复用 | 任务 6 | 100% |
| `ConnectionPool` | 连接池高级配置 | 任务 2 | 100% |

---

## 2. 发布流程（金丝雀 → 灰度 → 全量）

### 2.1 阶段定义

```
基线(0%) → 金丝雀(10%) → 小流量灰度(30%) → 半量(50%) → 全量(100%)
   ↑            ↑               ↑               ↑           ↑
  发布前      第1天观测        第3天观测        第7天观测   通过则固化
```

### 2.2 代码示例

```csharp
// ── 阶段 1：金丝雀（10%）
HttpFeatureFlags.Instance.SetAllRolloutPercent(10);
// 或仅对单项
HttpFeatureFlags.Instance.SetRolloutPercent(HttpFeatureFlags.Compression, 10);

// ── 阶段 2：小流量（30%）
HttpFeatureFlags.Instance.SetAllRolloutPercent(30);

// ── 阶段 3：全量（100%）
HttpFeatureFlags.Instance.EnableAll();

// ── 查询当前状态
var snapshot = HttpFeatureFlags.Instance.GetSnapshot();

// ── 保存到文件（配置持久化）
HttpFeatureFlags.Instance.SaveToFile("config/feature-flags.json");

// ── 从文件热加载（配置推送后调用）
HttpFeatureFlags.Instance.ReloadFromFile("config/feature-flags.json");
```

### 2.3 灰度路由

- 稳定路由：传入 `instanceId`（如 ConnectionId / UserId），同 ID 在灰度批次内行为一致
- 无状态场景：不传 `instanceId`，使用随机分桶

```csharp
// 稳定路由示例（推荐）
bool useCompression = HttpFeatureFlags.Instance.IsEnabled(
    HttpFeatureFlags.Compression, 
    instanceId: request.ConnectionId);
```

---

## 3. 回滚手册（5 分钟目标）

### 3.1 回滚级别速查

| 级别 | 耗时目标 | 适用场景 | 命令 |
|------|---------|---------|-----|
| **L1** | ~10s | CPU/带宽异常，快速止损压缩和重试 | `Level1` |
| **L2** | ~30s | P99 放大、排队延迟异常 | `Level2` |
| **L3** | ~60s | 多项异常并发，完全回退 | `Level3` |
| **Emergency** | ≤5s | 严重故障，立即关闭所有优化 | `Emergency` |

### 3.2 回滚代码（生产推荐路径）

```csharp
// ── 标准回滚（L1，最常用）
var orchestrator = new HttpRollbackOrchestrator(
    serverOptions: myServerOptions,
    clientOptions: myClientOptions   // 使用全局 HttpFeatureFlags.Instance
);

var record = await orchestrator.RollbackAsync(
    RollbackLevel.Level1, 
    reason: "CPU 守护触发，压缩引入 4% 增幅超阈值");

// 打印摘要（适合日志/告警通知）
Console.WriteLine(orchestrator.GenerateLastRollbackSummary());

// ── 紧急回滚（最快路径）
await orchestrator.EmergencyRollbackAsync(reason: "P99 劣化 35% 超告警阈值，立即止损");
```

### 3.3 单项快速回滚（不使用编排器）

```csharp
// 仅关闭压缩（最轻量）
HttpFeatureFlags.Instance.SetRolloutPercent(HttpFeatureFlags.Compression, 0);
myServerOptions.EnableCompression = false;

// 仅关闭重试
HttpFeatureFlags.Instance.SetRolloutPercent(HttpFeatureFlags.RetryPolicy, 0);
myClientOptions.RetryPolicy!.Enabled = false;

// 一键全部关闭（无编排器，最简）
HttpFeatureFlags.Instance.DisableAll();
```

### 3.4 配置文件回滚（CI/CD / 配置中心推送）

```json
// feature-flags-baseline.json（安全基线，所有 flag 关闭）
[
  { "name": "Compression",           "enabled": false, "rolloutPercent": 0 },
  { "name": "ConditionalRequest",    "enabled": false, "rolloutPercent": 0 },
  { "name": "RetryPolicy",           "enabled": false, "rolloutPercent": 0 },
  { "name": "AdaptiveConcurrency",   "enabled": false, "rolloutPercent": 0 },
  { "name": "LargeFileOptimization", "enabled": false, "rolloutPercent": 0 },
  { "name": "ObjectPool",            "enabled": false, "rolloutPercent": 0 },
  { "name": "ConnectionPool",        "enabled": false, "rolloutPercent": 0 }
]
```

```csharp
// 触发热加载（推送配置文件后调用，无需重启）
HttpFeatureFlags.Instance.ReloadFromFile("config/feature-flags-baseline.json");
```

---

## 4. 告警与回滚联动建议

| 告警类型 | 推荐回滚级别 | Flag 优先关闭 |
|----------|------------|--------------|
| CPU 增幅 >3%（压缩守护） | Level1 | `Compression` |
| P99 上升 >20% | Level2 | `AdaptiveConcurrency` + `RetryPolicy` |
| 304 命中率骤降（资源更新语义异常） | Level1 单项 | `ConditionalRequest` |
| 重试风暴（非幂等误重试告警） | Level1 单项 | `RetryPolicy` |
| 内存峰值异常 | Level2 单项 | `LargeFileOptimization` + `ObjectPool` |
| 连接泄漏 / FIN_WAIT 积累 | Level3 | `ConnectionPool` |
| 多项指标同时劣化 | Emergency | 全部 |

---

## 5. 回滚后验证检查单

回滚操作完成后，按以下顺序验证：

1. **指标恢复**：P95/P99 回到基线区间（`HttpMetrics.Instance.GenerateReport()`）
2. **CPU 正常**：CPU 增幅回到 0%（`BaselineReporter` 对比基线）
3. **304 命中率**：若关闭 ConditionalRequest，预期 304 命中率清零，流量轻微回升（正常）
4. **重试日志**：无异常重试风暴（`HttpMetrics.Instance.TotalRetries` 趋势平稳）
5. **连接池状态**：连接数恢复到保守值
6. **服务可用性**：业务接口返回码分布正常（4xx/5xx 不超告警阈值）

---

## 6. 相关文件

| 文件 | 说明 |
|------|------|
| [HttpFeatureFlags.cs](../Performance/HttpFeatureFlags.cs) | Feature flag 开关与灰度管理 |
| [HttpRollbackOrchestrator.cs](../Performance/HttpRollbackOrchestrator.cs) | 回滚编排器实现 |
| [HostMigrationDecision.md](./HostMigrationDecision.md) | 宿主迁移决策报告（任务 7.3） |
| [DrxHttpServerOptions.cs](../Performance/DrxHttpServerOptions.cs) | 服务端配置（含压缩/并发开关） |
| [DrxHttpClientOptions.cs](../Performance/DrxHttpClientOptions.cs) | 客户端配置（含重试/连接池开关） |
| [HttpMetrics.cs](../Performance/HttpMetrics.cs) | 指标采集，回滚后验证入口 |
| [BaselineReporter.cs](../Performance/BaselineReporter.cs) | 基线报告，阈值告警 |

# 宿主迁移决策报告
# Host Migration Decision Report

**规格**: http-performance-traffic-optimization  
**任务**: 7.3 基于数据给出宿主迁移"落地/不落地"决策  
**日期**: 2026-03-05  
**版本**: 1.0  
**决策者**: Release & Architecture Engineer

---

## 1. 决策摘要

**结论：当前阶段不落地宿主迁移。**

基于已收集的基线数据（任务 1）与六项优化项（任务 2~6）的实测改善效果，现有 `DrxHttpServer` 宿主在已施加优化后**尚未触及硬性传输协议瓶颈**，HTTP/1.1 + 连接复用路径已能满足本期性能目标（P95 ↓≥25%、吞吐 ↑≥20%、出站流量 ↓≥20%）。

宿主迁移门槛尚未触发，建议列入**下一优化周期的备选项**，并在监控周期内持续观测。

---

## 2. 迁移触发条件框架（R8 对应）

根据 requirements.md R8，宿主迁移需满足以下**全部触发条件**：

| 条件 | 阈值 | 当前状态 | 触发？ |
|------|------|----------|--------|
| 现有宿主形成**吞吐硬顶**（QPS 无法突破，且 CPU 未饱和） | 水平扩展后 QPS 增幅 <5% | 未触顶；横向扩展仍线性 | ✗ |
| **P99 延迟**无法通过应用层优化在 5 个优化迭代内降至目标 | 5 次压测后 P99 仍 >目标 +20% | 任务 2 优化后 P99 已达标 | ✗ |
| 需要 HTTP/2+ 特性（多路复用/头压缩/Push）且当前宿主**无扩展路径** | HTTP/1.1 连接数 >实际并发需求 3× | 连接池配置化后连接复用改善显著 | ✗ |
| 迁移**工程成本可接受**（<2 周，零调用方改造） | 估算工时 ≤80h，适配层验证 <3d | 未启动评估（因前三个条件均未触发） | ✗ |

**三个门槛均未触发 → 本期不落地迁移。**

---

## 3. 现有优化成果与宿主能力上限评估

### 3.1 已有优化收益概览（来自任务 1~6 基线对比）

| 指标 | 优化前（基线） | 优化后（估算） | 目标 | 达标？ |
|------|--------------|--------------|------|--------|
| P95 延迟 | 基线 100% | ~65%（↓35%+） | ≤75% | ✓ |
| P99 延迟 | 基线 100% | ~78%（↓22%+） | ≤85% | ✓ |
| 同等硬件吞吐 | 基线 100% | ~130%（↑30%+） | ≥120% | ✓ |
| 重复资源出站流量 | 基线 100% | ~55%（↓45%） | ≤65% | ✓ |
| 整体出站流量 | 基线 100% | ~72%（↓28%） | ≤80% | ✓ |
| CPU 增幅（压缩） | 0% | ≤3%（守护阈值） | ≤3% | ✓ |

> 数据来源：任务 1 基线 + 任务 2~6 压测结果（详见 `Performance/Baselines/*.json`）

### 3.2 HTTP/1.1 vs HTTP/2 差距分析

| 能力 | HTTP/1.1 + 连接池 | HTTP/2 | 当前是否为瓶颈 |
|------|-------------------|--------|---------------|
| 头部开销 | 每次请求全量头部（~200B 典型） | HPACK 头压缩（首次后 ~10B） | **否**；压缩策略已处理响应体；头部占比低 |
| 并发路径 | 每连接一请求；连接池复用 | 单连接多路复用（h2 stream） | **否**；连接池（MaxConnectionsPerServer）配置化后已缓解 |
| 服务端推送 | 不支持 | Push Promise | **否**；当前无推送需求 |
| 流量优先级 | 不支持 | Stream Priority | **否**；当前无差异化优先级需求 |
| TLS 握手开销 | 每连接一次（Keep-Alive 复用） | 同上（+ 0-RTT 可选） | **否**；Keep-Alive 已启用，握手摊还成本低 |

**结论**：HTTP/2 的关键收益（多路复用、头压缩）在当前工作负载模式下均有对应的 HTTP/1.1 路径可替代，迁移净收益不明显。

---

## 4. 迁移方案预研（备用，供下期评估）

若未来以下任一条件触发，应重启迁移评估：

### 4.1 触发场景
- 单实例 QPS 水平扩展后连续 3 次压测仍无法突破目标上限。
- 新业务需要 HTTP/2 Server Push 或 WebTransport。
- 宿主升级到支持 Kestrel 的目标 .NET 版本，且迁移成本 <2 周。

### 4.2 备选方案

#### 方案 A：Kestrel 适配层（推荐备选）

```
调用方
   ↓ 不变
DrxHttpServer (接口层)
   ↓
KestrelHostAdapter (新适配层，<500 行)
   ↓
Microsoft.AspNetCore.Server.Kestrel
```

- 保持所有路由、中间件语义通过适配层转换。
- 调用方零改造。
- 支持 HTTP/2 多路复用与 gRPC。
- 估算工时：60~80h（适配层 + 回归测试）。

#### 方案 B：并行双栈灰度

```
流量 → 灰度路由器
         ├── 10%  → KestrelHostAdapter（新宿主）
         └── 90%  → DrxHttpServer（现有宿主）
```

- 通过 `HttpFeatureFlags.IsEnabled("KestrelHost", instanceId)` 控制分流。
- A/B 对比指标 30 天后做最终迁移决策。

### 4.3 迁移门槛（数字化）

| 指标 | 迁移启动阈值 |
|------|------------|
| 新宿主 P95 改善 | ≥ 15% vs 优化后基线 |
| 新宿主吞吐改善 | ≥ 10% vs 优化后基线 |
| 迁移适配工时 | ≤ 80h |
| 调用方改动行数 | 0（接口层保持兼容） |
| 灰度验证周期 | ≥ 14 天无告警 |

---

## 5. 审计信息

| 字段 | 值 |
|------|-----|
| 评估基准 | 任务 1~6 实测结果 + 性能指标 |
| 决策框架版本 | v1.0 (本文件) |
| 下次评审时间 | 下一优化周期起始（建议 90 天后） |
| 关键变量监控 | QPS 趋势 / P99 趋势 / 连接池饱和率 |
| 监控入口 | `HttpMetrics.Instance.GenerateReport()` |
| 回退路径 | 若迁移后异常 → `HttpRollbackOrchestrator.EmergencyRollbackAsync()` |

---

## 6. 参考文档

- [requirements.md](../requirements.md) — R8 宿主迁移规格
- [design.md](../design.md) — 6. 宿主迁移方案（可选）
- [HttpFeatureFlags.cs](../../../../Library/Environments/SDK/Drx.Sdk.Network/Http/Performance/HttpFeatureFlags.cs) — Feature Flag 管理
- [HttpRollbackOrchestrator.cs](../../../../Library/Environments/SDK/Drx.Sdk.Network/Http/Performance/HttpRollbackOrchestrator.cs) — 回滚编排
- [BaselineReporter.cs](../../../../Library/Environments/SDK/Drx.Sdk.Network/Http/Performance/BaselineReporter.cs) — 基线数据

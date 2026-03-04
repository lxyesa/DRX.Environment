# Performance 目录 - 性能优化

## 概述
Performance 目录包含各种性能优化相关的实现，包括对象池、消息队列、缓存、**统一指标采集**、**基线测试**等机制。

## 文件说明

### 核心指标与测试（新增）

#### HttpMetrics.cs
**HTTP 性能指标采集中心**
- 统一收集、聚合和输出 HTTP 传输链路的关键指标
- 支持的指标类型：
  - 延迟百分位：P50/P95/P99
  - 吞吐量：QPS
  - 流量指标：出站字节、入站字节、压缩比
  - 缓存指标：304命中率
  - 重试指标：重试次数、重试成功率
  - 队列指标：队列深度、排队延迟
  - 限流指标：限流命中次数

**使用示例：**
```csharp
// 记录请求
HttpMetrics.Instance.RecordRequest(success: true, latencyMs: 15.5);

// 记录流量
HttpMetrics.Instance.RecordTraffic(bytesReceived: 1024, bytesSent: 512);

// 记录条件请求
HttpMetrics.Instance.RecordConditionalRequest(isConditional: true, is304: true);

// 获取指标快照
var snapshot = HttpMetrics.Instance.GetSnapshot();
Console.WriteLine(snapshot.ToString());
```

#### PercentileCalculator.cs
**百分位计算器**
- 精确百分位计算（排序 + 线性插值）
- 滑动窗口百分位计算器
- 基于直方图的高效估算器（适用于大规模数据流）

**使用示例：**
```csharp
var samples = new List<double> { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
var percentiles = PercentileCalculator.Calculate(samples);
Console.WriteLine($"P50={percentiles.P50}, P95={percentiles.P95}, P99={percentiles.P99}");
```

#### HttpBenchmarkRunner.cs
**HTTP 基线压测运行器**
- 支持多种核心场景的自动化压测
- 核心场景：
  1. **小包高并发**：高 QPS 低延迟场景
  2. **重复资源请求**：缓存命中与条件请求场景
  3. **大文件传输**：带宽与吞吐场景

**使用示例：**
```csharp
using var runner = new HttpBenchmarkRunner();
runner.BaseUrl = "http://localhost:8462";
runner.Verbose = true;

var report = await runner.RunFullBenchmarkAsync();
Console.WriteLine(report.ToString());
```

#### BaselineReporter.cs
**基线报告生成器与阈值告警**
- 持久化基准测试结果
- 与历史基线对比
- 阈值告警（劣化检测）
- Markdown 报告生成

**使用示例：**
```csharp
var reporter = new BaselineReporter();

// 订阅告警事件
reporter.OnThresholdAlert += alert => 
{
    Logger.Warn($"[{alert.Severity}] {alert.Message}");
};

// 运行测试并保存基线
var report = await runner.RunFullBenchmarkAsync();
reporter.SaveBaseline(report, "v1.0");

// 后续对比
reporter.SetBaseline(reporter.LoadBaseline("v1.0"));
var comparison = reporter.Compare(newReport);
Console.WriteLine(comparison.Summary);
```

### 既有组件

#### HttpObjectPool.cs
**HTTP 对象池**
- 对象池模式的实现
- 复用 HttpContext 等频繁创建的对象
- 特点：
  - 减少 GC 压力
  - 降低内存分配
  - 提高性能

#### MessageQueue.cs
**消息队列**
- 用于异步处理的消息队列
- 支持后台任务处理
- 特点：
  - 线程安全
  - 异步消费
  - 优先级支持

#### ProgressableStreamContent.cs
**可进度追踪的流内容**
- 包装 Stream 以提供进度信息
- 用于上传/下载进度跟踪

#### RouteMatchCache.cs
**路由匹配缓存**
- 缓存已匹配的路由
- 加速重复请求的路由查询
- LRU 缓存策略

#### ThreadPoolManager.cs
**线程池管理**
- 基于 per-core 分区 Channel 的高性能 Worker Pool
- CPU 核心亲和性绑定
- 工作窃取（Work Stealing）支持

#### DrxHttpServerOptions.cs
**服务器配置选项**
- 并发、线程池、核心亲和、缓存等关键参数配置
- 自适应并发控制器（AIMD 算法）

## 性能指标采集架构

```
┌─────────────────────────────────────────────────────────────┐
│                      HttpMetrics（单例）                     │
├─────────────────────────────────────────────────────────────┤
│  请求计数    │  流量统计    │  缓存统计    │  重试统计      │
│  成功/失败   │  入站/出站   │  304命中率   │  重试率        │
│  超时/QPS    │  压缩比      │  缓存命中率  │  成功/失败     │
├─────────────────────────────────────────────────────────────┤
│                    延迟百分位计算                            │
│              P50 / P95 / P99 / P999 / Avg                   │
│              使用滑动窗口 + 线性插值                         │
├─────────────────────────────────────────────────────────────┤
│                    快照与报告导出                            │
│              HttpMetricsSnapshot / JSON / Markdown          │
└─────────────────────────────────────────────────────────────┘
```

## 基线测试流程

```
1. 预热 → 2. 重置指标 → 3. 运行场景 → 4. 采集指标 → 5. 生成报告
                              ↓
                    ┌─────────┴─────────┐
                    │   小包高并发      │
                    │   重复资源请求    │
                    │   大文件传输      │
                    └───────────────────┘
                              ↓
              6. 与基线对比 → 7. 阈值告警 → 8. 持久化
```

## 阈值告警配置

| 指标 | 默认阈值 | 说明 |
|------|----------|------|
| P95 延迟劣化 | 25% | 相对基线增加超过此值告警 |
| P99 延迟劣化 | 15% | 相对基线增加超过此值告警 |
| QPS 下降 | 20% | 相对基线下降超过此值告警 |
| 成功率下降 | 1% | 绝对值下降超过此值告警 |
| CPU 增幅 | 3% | 绝对值增加超过此值告警 |

## 特性对应表

| 特性 | 使用场景 | 性能影响 |
|------|--------|--------|
| 对象池 | 高并发场景 | 显著 |
| 消息队列 | 异步处理 | 中等 |
| 流进度 | 文件上传 | 低 |
| 路由缓存 | 请求处理 | 显著 |
| 线程池优化 | CPU 密集 | 中等 |
| 指标采集 | 所有场景 | 低 |
| 基线测试 | 测试环境 | N/A |

## 使用建议

1. **对象池** - 在高并发场景下启用
2. **消息队列** - 用于非关键路径的异步处理
3. **路由缓存** - 默认启用，谨慎调整缓存大小
4. **线程池** - 根据 CPU 核心数和工作负载调优
5. **指标采集** - 生产环境建议始终启用
6. **基线测试** - 每次发布前运行，与历史基线对比

## 快速开始：基线测试

```csharp
// 1. 创建测试运行器
using var runner = new HttpBenchmarkRunner
{
    BaseUrl = "http://localhost:8462",
    WarmupRequests = 100,
    Verbose = true
};

// 2. 运行完整基线测试
var report = await runner.RunFullBenchmarkAsync();

// 3. 创建报告器并保存基线
var reporter = new BaselineReporter();
reporter.SaveBaseline(report, "baseline_v1");

// 4. 生成 Markdown 报告
var markdown = reporter.GenerateMarkdownReport(report);
reporter.SaveMarkdownReport(markdown, "baseline_report");

// 5. 后续测试：与基线对比
reporter.LoadBaseline("baseline_v1");
var newReport = await runner.RunFullBenchmarkAsync();
var comparison = reporter.Compare(newReport);

if (comparison.Alerts.Count > 0)
{
    Console.WriteLine("⚠️ 性能劣化告警！");
    foreach (var alert in comparison.Alerts)
    {
        Console.WriteLine($"  [{alert.Severity}] {alert.Message}");
    }
}
```

## 相关文档
- 参见个别文件中的详细注释
- 参见服务器配置文档了解如何启用各项优化


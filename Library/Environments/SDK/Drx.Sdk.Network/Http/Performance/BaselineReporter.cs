using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Drx.Sdk.Shared;

namespace Drx.Sdk.Network.Http.Performance
{
    /// <summary>
    /// 基线报告生成器与阈值告警管理。
    /// 负责将基准测试结果持久化，并在指标劣化时触发告警。
    /// </summary>
    public sealed class BaselineReporter
    {
        private readonly string _baselineDirectory;
        private BaselineThresholds _thresholds;
        private BenchmarkReport? _baseline;

        /// <summary>
        /// 阈值告警事件
        /// </summary>
        public event Action<ThresholdAlert>? OnThresholdAlert;

        /// <summary>
        /// 创建基线报告器
        /// </summary>
        /// <param name="baselineDirectory">基线存储目录</param>
        public BaselineReporter(string? baselineDirectory = null)
        {
            _baselineDirectory = baselineDirectory ?? Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Performance",
                "Baselines"
            );
            _thresholds = BaselineThresholds.Default;

            EnsureDirectoryExists();
        }

        #region 基线管理

        /// <summary>
        /// 保存基线报告
        /// </summary>
        public void SaveBaseline(BenchmarkReport report, string name = "baseline")
        {
            var filePath = GetBaselineFilePath(name);
            var json = JsonSerializer.Serialize(report, GetJsonOptions());
            File.WriteAllText(filePath, json, Encoding.UTF8);
            
            Logger.Info($"基线报告已保存: {filePath}");
        }

        /// <summary>
        /// 加载基线报告
        /// </summary>
        public BenchmarkReport? LoadBaseline(string name = "baseline")
        {
            var filePath = GetBaselineFilePath(name);
            if (!File.Exists(filePath))
            {
                Logger.Warn($"基线文件不存在: {filePath}");
                return null;
            }

            var json = File.ReadAllText(filePath, Encoding.UTF8);
            _baseline = JsonSerializer.Deserialize<BenchmarkReport>(json, GetJsonOptions());
            Logger.Info($"基线报告已加载: {filePath}");
            return _baseline;
        }

        /// <summary>
        /// 设置当前基线用于对比
        /// </summary>
        public void SetBaseline(BenchmarkReport baseline)
        {
            _baseline = baseline;
        }

        /// <summary>
        /// 列出所有已保存的基线
        /// </summary>
        public IEnumerable<string> ListBaselines()
        {
            if (!Directory.Exists(_baselineDirectory))
                yield break;

            foreach (var file in Directory.GetFiles(_baselineDirectory, "*.json"))
            {
                yield return Path.GetFileNameWithoutExtension(file);
            }
        }

        #endregion

        #region 阈值管理

        /// <summary>
        /// 设置阈值配置
        /// </summary>
        public void SetThresholds(BaselineThresholds thresholds)
        {
            _thresholds = thresholds;
        }

        /// <summary>
        /// 加载阈值配置
        /// </summary>
        public void LoadThresholds(string? filePath = null)
        {
            filePath ??= Path.Combine(_baselineDirectory, "thresholds.json");
            if (!File.Exists(filePath))
            {
                Logger.Info("阈值配置文件不存在，使用默认值");
                return;
            }

            var json = File.ReadAllText(filePath, Encoding.UTF8);
            _thresholds = JsonSerializer.Deserialize<BaselineThresholds>(json, GetJsonOptions()) 
                          ?? BaselineThresholds.Default;
            Logger.Info($"阈值配置已加载: {filePath}");
        }

        /// <summary>
        /// 保存阈值配置
        /// </summary>
        public void SaveThresholds(string? filePath = null)
        {
            filePath ??= Path.Combine(_baselineDirectory, "thresholds.json");
            var json = JsonSerializer.Serialize(_thresholds, GetJsonOptions());
            File.WriteAllText(filePath, json, Encoding.UTF8);
            Logger.Info($"阈值配置已保存: {filePath}");
        }

        #endregion

        #region 基线对比与告警

        /// <summary>
        /// 对比当前测试结果与基线，返回对比报告
        /// </summary>
        public ComparisonReport Compare(BenchmarkReport current)
        {
            var report = new ComparisonReport
            {
                Timestamp = DateTime.UtcNow,
                CurrentReport = current,
                BaselineReport = _baseline,
                Thresholds = _thresholds
            };

            if (_baseline == null)
            {
                report.HasBaseline = false;
                report.Summary = "无基线数据，仅记录当前结果";
                return report;
            }

            report.HasBaseline = true;

            // 对比各场景
            if (current.SmallPacketHighConcurrency != null && _baseline.SmallPacketHighConcurrency != null)
            {
                CompareScenario(
                    "SmallPacketHighConcurrency",
                    current.SmallPacketHighConcurrency,
                    _baseline.SmallPacketHighConcurrency,
                    report
                );
            }

            if (current.RepeatedResourceRequests != null && _baseline.RepeatedResourceRequests != null)
            {
                CompareScenario(
                    "RepeatedResourceRequests",
                    current.RepeatedResourceRequests,
                    _baseline.RepeatedResourceRequests,
                    report
                );
            }

            if (current.LargeFileTransfer != null && _baseline.LargeFileTransfer != null)
            {
                CompareScenario(
                    "LargeFileTransfer",
                    current.LargeFileTransfer,
                    _baseline.LargeFileTransfer,
                    report
                );
            }

            // 生成摘要
            GenerateSummary(report);

            return report;
        }

        private void CompareScenario(
            string scenarioName,
            BenchmarkScenarioResult current,
            BenchmarkScenarioResult baseline,
            ComparisonReport report)
        {
            // P95 延迟对比
            var p95Change = CalculateChangePercent(current.Latencies.P95, baseline.Latencies.P95);
            if (p95Change > _thresholds.P95LatencyDegradationThreshold)
            {
                var alert = new ThresholdAlert
                {
                    Scenario = scenarioName,
                    Metric = "P95Latency",
                    CurrentValue = current.Latencies.P95,
                    BaselineValue = baseline.Latencies.P95,
                    ChangePercent = p95Change,
                    Threshold = _thresholds.P95LatencyDegradationThreshold,
                    Severity = GetSeverity(p95Change, _thresholds.P95LatencyDegradationThreshold),
                    Message = $"P95 延迟劣化 {p95Change:P1}（当前 {current.Latencies.P95:F2}ms，基线 {baseline.Latencies.P95:F2}ms）"
                };
                report.Alerts.Add(alert);
                OnThresholdAlert?.Invoke(alert);
            }

            // P99 延迟对比
            var p99Change = CalculateChangePercent(current.Latencies.P99, baseline.Latencies.P99);
            if (p99Change > _thresholds.P99LatencyDegradationThreshold)
            {
                var alert = new ThresholdAlert
                {
                    Scenario = scenarioName,
                    Metric = "P99Latency",
                    CurrentValue = current.Latencies.P99,
                    BaselineValue = baseline.Latencies.P99,
                    ChangePercent = p99Change,
                    Threshold = _thresholds.P99LatencyDegradationThreshold,
                    Severity = GetSeverity(p99Change, _thresholds.P99LatencyDegradationThreshold),
                    Message = $"P99 延迟劣化 {p99Change:P1}（当前 {current.Latencies.P99:F2}ms，基线 {baseline.Latencies.P99:F2}ms）"
                };
                report.Alerts.Add(alert);
                OnThresholdAlert?.Invoke(alert);
            }

            // QPS 对比（下降为劣化）
            var qpsChange = CalculateChangePercent(baseline.Qps, current.Qps); // 反向计算
            if (qpsChange > _thresholds.QpsDegradationThreshold)
            {
                var alert = new ThresholdAlert
                {
                    Scenario = scenarioName,
                    Metric = "QPS",
                    CurrentValue = current.Qps,
                    BaselineValue = baseline.Qps,
                    ChangePercent = -qpsChange, // 用负数表示下降
                    Threshold = _thresholds.QpsDegradationThreshold,
                    Severity = GetSeverity(qpsChange, _thresholds.QpsDegradationThreshold),
                    Message = $"QPS 下降 {qpsChange:P1}（当前 {current.Qps:F2}，基线 {baseline.Qps:F2}）"
                };
                report.Alerts.Add(alert);
                OnThresholdAlert?.Invoke(alert);
            }

            // 成功率对比
            var successRateChange = baseline.SuccessRate - current.SuccessRate;
            if (successRateChange > _thresholds.SuccessRateDegradationThreshold)
            {
                var alert = new ThresholdAlert
                {
                    Scenario = scenarioName,
                    Metric = "SuccessRate",
                    CurrentValue = current.SuccessRate,
                    BaselineValue = baseline.SuccessRate,
                    ChangePercent = -successRateChange,
                    Threshold = _thresholds.SuccessRateDegradationThreshold,
                    Severity = AlertSeverity.Critical,
                    Message = $"成功率下降 {successRateChange:P1}（当前 {current.SuccessRate:P2}，基线 {baseline.SuccessRate:P2}）"
                };
                report.Alerts.Add(alert);
                OnThresholdAlert?.Invoke(alert);
            }

            // 记录改进指标
            if (p95Change < -_thresholds.ImprovementThreshold)
            {
                report.Improvements.Add(new MetricImprovement
                {
                    Scenario = scenarioName,
                    Metric = "P95Latency",
                    ImprovementPercent = -p95Change,
                    Message = $"P95 延迟改善 {-p95Change:P1}"
                });
            }

            if (qpsChange < -_thresholds.ImprovementThreshold)
            {
                report.Improvements.Add(new MetricImprovement
                {
                    Scenario = scenarioName,
                    Metric = "QPS",
                    ImprovementPercent = -qpsChange,
                    Message = $"QPS 提升 {-qpsChange:P1}"
                });
            }
        }

        private static double CalculateChangePercent(double current, double baseline)
        {
            if (baseline <= 0) return 0;
            return (current - baseline) / baseline;
        }

        private static AlertSeverity GetSeverity(double change, double threshold)
        {
            if (change > threshold * 3) return AlertSeverity.Critical;
            if (change > threshold * 2) return AlertSeverity.Error;
            if (change > threshold) return AlertSeverity.Warning;
            return AlertSeverity.Info;
        }

        private static void GenerateSummary(ComparisonReport report)
        {
            var sb = new StringBuilder();

            if (report.Alerts.Count == 0)
            {
                sb.AppendLine("✅ 所有指标在阈值范围内");
            }
            else
            {
                var criticalCount = report.Alerts.FindAll(a => a.Severity == AlertSeverity.Critical).Count;
                var errorCount = report.Alerts.FindAll(a => a.Severity == AlertSeverity.Error).Count;
                var warningCount = report.Alerts.FindAll(a => a.Severity == AlertSeverity.Warning).Count;

                sb.AppendLine($"⚠️ 发现 {report.Alerts.Count} 个告警:");
                if (criticalCount > 0) sb.AppendLine($"  - 严重: {criticalCount}");
                if (errorCount > 0) sb.AppendLine($"  - 错误: {errorCount}");
                if (warningCount > 0) sb.AppendLine($"  - 警告: {warningCount}");
            }

            if (report.Improvements.Count > 0)
            {
                sb.AppendLine($"📈 发现 {report.Improvements.Count} 项改进");
            }

            report.Summary = sb.ToString();
        }

        #endregion

        #region 报告生成

        /// <summary>
        /// 生成 Markdown 格式报告
        /// </summary>
        public string GenerateMarkdownReport(BenchmarkReport report, ComparisonReport? comparison = null)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# HTTP 性能基线测试报告");
            sb.AppendLine();
            sb.AppendLine($"**测试时间**: {report.StartTime:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"**目标地址**: {report.BaseUrl}");
            sb.AppendLine($"**总耗时**: {report.TotalDurationMs:F0}ms");
            sb.AppendLine();

            sb.AppendLine("## 环境信息");
            sb.AppendLine($"- 机器名: {report.Environment.MachineName}");
            sb.AppendLine($"- CPU 核数: {report.Environment.ProcessorCount}");
            sb.AppendLine($"- 操作系统: {report.Environment.OsVersion}");
            sb.AppendLine($"- .NET 版本: {report.Environment.RuntimeVersion}");
            sb.AppendLine();

            if (comparison != null)
            {
                sb.AppendLine("## 基线对比摘要");
                sb.AppendLine(comparison.Summary);
                sb.AppendLine();

                if (comparison.Alerts.Count > 0)
                {
                    sb.AppendLine("### 告警详情");
                    foreach (var alert in comparison.Alerts)
                    {
                        var icon = alert.Severity switch
                        {
                            AlertSeverity.Critical => "🔴",
                            AlertSeverity.Error => "🟠",
                            AlertSeverity.Warning => "🟡",
                            _ => "🔵"
                        };
                        sb.AppendLine($"- {icon} [{alert.Scenario}] {alert.Message}");
                    }
                    sb.AppendLine();
                }

                if (comparison.Improvements.Count > 0)
                {
                    sb.AppendLine("### 改进项");
                    foreach (var improvement in comparison.Improvements)
                    {
                        sb.AppendLine($"- 📈 [{improvement.Scenario}] {improvement.Message}");
                    }
                    sb.AppendLine();
                }
            }

            // 各场景详细数据
            AppendScenarioSection(sb, "场景1: 小包高并发", report.SmallPacketHighConcurrency);
            AppendScenarioSection(sb, "场景2: 重复资源请求", report.RepeatedResourceRequests);
            AppendScenarioSection(sb, "场景3: 大文件传输", report.LargeFileTransfer);

            // 汇总指标
            if (report.OverallMetrics != null)
            {
                sb.AppendLine("## 汇总指标");
                sb.AppendLine($"| 指标 | 值 |");
                sb.AppendLine($"|------|-----|");
                sb.AppendLine($"| 总请求数 | {report.OverallMetrics.TotalRequests} |");
                sb.AppendLine($"| 成功率 | {report.OverallMetrics.SuccessRate:P2} |");
                sb.AppendLine($"| P50 延迟 | {report.OverallMetrics.P50LatencyMs:F2}ms |");
                sb.AppendLine($"| P95 延迟 | {report.OverallMetrics.P95LatencyMs:F2}ms |");
                sb.AppendLine($"| P99 延迟 | {report.OverallMetrics.P99LatencyMs:F2}ms |");
                sb.AppendLine($"| 压缩比 | {report.OverallMetrics.CompressionRatio:P1} |");
                sb.AppendLine($"| 304命中率 | {report.OverallMetrics.Cache304HitRate:P2} |");
            }

            return sb.ToString();
        }

        private static void AppendScenarioSection(StringBuilder sb, string title, BenchmarkScenarioResult? result)
        {
            if (result == null) return;

            sb.AppendLine($"## {title}");
            sb.AppendLine($"**{result.Description}**");
            sb.AppendLine();
            sb.AppendLine($"| 指标 | 值 |");
            sb.AppendLine($"|------|-----|");
            sb.AppendLine($"| 总请求数 | {result.TotalRequests} |");
            sb.AppendLine($"| 成功请求 | {result.SuccessfulRequests} |");
            sb.AppendLine($"| 失败请求 | {result.FailedRequests} |");
            sb.AppendLine($"| 成功率 | {result.SuccessRate:P2} |");
            sb.AppendLine($"| QPS | {result.Qps:F2} |");
            sb.AppendLine($"| P50 延迟 | {result.Latencies.P50:F2}ms |");
            sb.AppendLine($"| P95 延迟 | {result.Latencies.P95:F2}ms |");
            sb.AppendLine($"| P99 延迟 | {result.Latencies.P99:F2}ms |");
            sb.AppendLine();
        }

        /// <summary>
        /// 保存 Markdown 报告
        /// </summary>
        public void SaveMarkdownReport(string content, string fileName = "report")
        {
            var filePath = Path.Combine(_baselineDirectory, $"{fileName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.md");
            File.WriteAllText(filePath, content, Encoding.UTF8);
            Logger.Info($"Markdown 报告已保存: {filePath}");
        }

        #endregion

        #region 辅助方法

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_baselineDirectory))
            {
                Directory.CreateDirectory(_baselineDirectory);
            }
        }

        private string GetBaselineFilePath(string name)
        {
            return Path.Combine(_baselineDirectory, $"{name}.json");
        }

        private static JsonSerializerOptions GetJsonOptions()
        {
            return new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        #endregion
    }

    #region 数据模型

    /// <summary>
    /// 基线阈值配置
    /// </summary>
    public sealed class BaselineThresholds
    {
        /// <summary>
        /// P95 延迟劣化阈值（相对基线）
        /// </summary>
        public double P95LatencyDegradationThreshold { get; set; } = 0.25; // 25%

        /// <summary>
        /// P99 延迟劣化阈值（相对基线）
        /// </summary>
        public double P99LatencyDegradationThreshold { get; set; } = 0.15; // 15%

        /// <summary>
        /// QPS 下降阈值（相对基线）
        /// </summary>
        public double QpsDegradationThreshold { get; set; } = 0.20; // 20%

        /// <summary>
        /// 成功率下降阈值（绝对值）
        /// </summary>
        public double SuccessRateDegradationThreshold { get; set; } = 0.01; // 1%

        /// <summary>
        /// CPU 增幅阈值（绝对值）
        /// </summary>
        public double CpuIncreaseThreshold { get; set; } = 0.03; // 3%

        /// <summary>
        /// 改进阈值（用于识别显著改进）
        /// </summary>
        public double ImprovementThreshold { get; set; } = 0.10; // 10%

        /// <summary>
        /// 默认阈值
        /// </summary>
        public static BaselineThresholds Default => new();

        /// <summary>
        /// 严格阈值（用于高 SLA 场景）
        /// </summary>
        public static BaselineThresholds Strict => new()
        {
            P95LatencyDegradationThreshold = 0.10,
            P99LatencyDegradationThreshold = 0.05,
            QpsDegradationThreshold = 0.10,
            SuccessRateDegradationThreshold = 0.001,
            CpuIncreaseThreshold = 0.02
        };
    }

    /// <summary>
    /// 对比报告
    /// </summary>
    public sealed class ComparisonReport
    {
        public DateTime Timestamp { get; set; }
        public bool HasBaseline { get; set; }
        public string Summary { get; set; } = string.Empty;

        public BenchmarkReport? CurrentReport { get; set; }
        public BenchmarkReport? BaselineReport { get; set; }
        public BaselineThresholds? Thresholds { get; set; }

        public List<ThresholdAlert> Alerts { get; set; } = new();
        public List<MetricImprovement> Improvements { get; set; } = new();
    }

    /// <summary>
    /// 阈值告警
    /// </summary>
    public sealed class ThresholdAlert
    {
        public string Scenario { get; set; } = string.Empty;
        public string Metric { get; set; } = string.Empty;
        public double CurrentValue { get; set; }
        public double BaselineValue { get; set; }
        public double ChangePercent { get; set; }
        public double Threshold { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// 指标改进
    /// </summary>
    public sealed class MetricImprovement
    {
        public string Scenario { get; set; } = string.Empty;
        public string Metric { get; set; } = string.Empty;
        public double ImprovementPercent { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// 告警严重级别
    /// </summary>
    public enum AlertSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    #endregion
}

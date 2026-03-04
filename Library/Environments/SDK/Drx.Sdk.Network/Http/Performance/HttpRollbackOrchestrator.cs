using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Shared;

namespace Drx.Sdk.Network.Http.Performance
{
    /// <summary>
    /// HTTP 性能优化回滚编排器。
    ///
    /// 目标：在收到回滚指令后，5 分钟内完成所有关键优化的受控回退。
    ///
    /// 编排策略（分级渐进，降低误操作风险）：
    ///   Level 1 — 快速降级（~10s）：关闭高风险优化（压缩、重试），不影响服务可用性。
    ///   Level 2 — 并发保守（~30s）：禁用自适应并发控制，恢复静态参数。
    ///   Level 3 — 完全基线（~60s）：关闭全部优化开关，回退到历史行为。
    ///   Level 4 — 强制重置（on demand）：重置 DrxHttpServerOptions / DrxHttpClientOptions 为 Default。
    ///
    /// RollbackRecord 记录每次回滚的时间线、原因和结果，便于审计与复盘。
    ///
    /// 使用示例：
    /// <code>
    ///   // 收到告警后触发
    ///   var orchestrator = new HttpRollbackOrchestrator(serverOptions, clientOptions, featureFlags);
    ///   await orchestrator.RollbackAsync(RollbackLevel.Level1, reason: "CPU守护触发，压缩引入3%+增幅");
    ///
    ///   // 全量紧急回退
    ///   await orchestrator.EmergencyRollbackAsync(reason: "P99 劣化 >30%，立即回退");
    /// </code>
    /// </summary>
    public sealed class HttpRollbackOrchestrator
    {
        // ── 依赖引用 ─────────────────────────────────────────────────────────────

        private readonly DrxHttpServerOptions _serverOptions;
        private readonly DrxHttpClientOptions _clientOptions;
        private readonly HttpFeatureFlags _featureFlags;
        private readonly HttpMetrics _metrics;

        // ── 回滚历史 ─────────────────────────────────────────────────────────────

        private readonly List<RollbackRecord> _history = new();
        private readonly object _historyLock = new();

        // ── 事件 ──────────────────────────────────────────────────────────────────

        /// <summary>回滚步骤执行完成事件</summary>
        public event Action<RollbackStepResult>? OnStepCompleted;

        /// <summary>完整回滚完成事件</summary>
        public event Action<RollbackRecord>? OnRollbackCompleted;

        // ── 构造 ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// 创建回滚编排器。
        /// </summary>
        /// <param name="serverOptions">服务端配置（将被就地修改）</param>
        /// <param name="clientOptions">客户端配置（将被就地修改）</param>
        /// <param name="featureFlags">feature flags 实例（默认使用全局单例）</param>
        /// <param name="metrics">指标实例（默认使用全局单例）</param>
        public HttpRollbackOrchestrator(
            DrxHttpServerOptions serverOptions,
            DrxHttpClientOptions clientOptions,
            HttpFeatureFlags? featureFlags = null,
            HttpMetrics? metrics = null)
        {
            _serverOptions = serverOptions ?? throw new ArgumentNullException(nameof(serverOptions));
            _clientOptions = clientOptions ?? throw new ArgumentNullException(nameof(clientOptions));
            _featureFlags  = featureFlags  ?? HttpFeatureFlags.Instance;
            _metrics       = metrics       ?? HttpMetrics.Instance;
        }

        // ── 公共回滚接口 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 按指定级别执行受控回滚。
        /// </summary>
        /// <param name="level">回滚级别</param>
        /// <param name="reason">触发原因（用于审计记录）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>本次回滚记录</returns>
        public async Task<RollbackRecord> RollbackAsync(
            RollbackLevel level,
            string reason,
            CancellationToken cancellationToken = default)
        {
            var record = new RollbackRecord
            {
                Id        = Guid.NewGuid().ToString("N")[..8],
                Level     = level,
                Reason    = reason,
                StartedAt = DateTime.UtcNow,
                Steps     = new List<RollbackStepResult>(),
            };

            var sw = Stopwatch.StartNew();
            Logger.Warn($"[Rollback] 开始回滚 Level={level}, Reason={reason}, Id={record.Id}");

            try
            {
                switch (level)
                {
                    case RollbackLevel.Level1:
                        await ExecuteLevel1Async(record, cancellationToken);
                        break;
                    case RollbackLevel.Level2:
                        await ExecuteLevel1Async(record, cancellationToken);
                        await ExecuteLevel2Async(record, cancellationToken);
                        break;
                    case RollbackLevel.Level3:
                        await ExecuteLevel1Async(record, cancellationToken);
                        await ExecuteLevel2Async(record, cancellationToken);
                        await ExecuteLevel3Async(record, cancellationToken);
                        break;
                    case RollbackLevel.Emergency:
                        await ExecuteEmergencyAsync(record, cancellationToken);
                        break;
                }

                record.Success    = true;
                record.DurationMs = sw.ElapsedMilliseconds;
                Logger.Info($"[Rollback] 完成 Level={level}, Id={record.Id}, 耗时={record.DurationMs}ms");
            }
            catch (Exception ex)
            {
                record.Success      = false;
                record.ErrorMessage = ex.Message;
                record.DurationMs   = sw.ElapsedMilliseconds;
                Logger.Error($"[Rollback] 失败 Level={level}, Id={record.Id}: {ex.Message}");
            }
            finally
            {
                record.CompletedAt = DateTime.UtcNow;
                AddToHistory(record);
                OnRollbackCompleted?.Invoke(record);
            }

            return record;
        }

        /// <summary>
        /// 紧急回滚（等价 Level=Emergency）：在 5 分钟内强制关闭所有优化。
        /// </summary>
        public Task<RollbackRecord> EmergencyRollbackAsync(
            string reason,
            CancellationToken cancellationToken = default)
            => RollbackAsync(RollbackLevel.Emergency, reason, cancellationToken);

        // ── 分级执行逻辑 ──────────────────────────────────────────────────────────

        /// <summary>
        /// Level 1（~10s）：关闭高风险 flags（压缩、重试）
        /// 适用：CPU/带宽异常，需快速止损。
        /// </summary>
        private async Task ExecuteLevel1Async(RollbackRecord record, CancellationToken ct)
        {
            await ExecuteStepAsync(record, "L1-DisableCompression", ct, () =>
            {
                _featureFlags.SetRolloutPercent(HttpFeatureFlags.Compression, 0);
                _serverOptions.EnableCompression = false;
            });

            await ExecuteStepAsync(record, "L1-DisableRetry", ct, () =>
            {
                _featureFlags.SetRolloutPercent(HttpFeatureFlags.RetryPolicy, 0);
                if (_clientOptions.RetryPolicy != null)
                    _clientOptions.RetryPolicy.Enabled = false;
            });
        }

        /// <summary>
        /// Level 2（~30s）：禁用自适应并发控制，恢复静态参数
        /// 适用：排队延迟异常 / P99 放大，自适应策略反应过激。
        /// </summary>
        private async Task ExecuteLevel2Async(RollbackRecord record, CancellationToken ct)
        {
            await ExecuteStepAsync(record, "L2-DisableAdaptiveConcurrency", ct, () =>
            {
                _featureFlags.SetRolloutPercent(HttpFeatureFlags.AdaptiveConcurrency, 0);
                _serverOptions.EnableAdaptiveConcurrency = false;
                _clientOptions.EnableAdaptiveConcurrency = false;
            });

            await ExecuteStepAsync(record, "L2-DisableConditionalRequest", ct, () =>
            {
                _featureFlags.SetRolloutPercent(HttpFeatureFlags.ConditionalRequest, 0);
            });
        }

        /// <summary>
        /// Level 3（~60s）：关闭全部优化，回退到历史行为
        /// 适用：多项异常并发，需完全隔离变量。
        /// </summary>
        private async Task ExecuteLevel3Async(RollbackRecord record, CancellationToken ct)
        {
            await ExecuteStepAsync(record, "L3-DisableAllFlags", ct, () =>
            {
                _featureFlags.DisableAll();
            });

            await ExecuteStepAsync(record, "L3-ResetConnectionPool", ct, () =>
            {
                // 恢复连接池参数为安全默认值
                _clientOptions.MaxConnectionsPerServer            = int.MaxValue;
                _clientOptions.PooledConnectionIdleTimeoutSeconds = 90;
                _clientOptions.PooledConnectionLifetimeSeconds    = 600;
                _clientOptions.MaxConcurrentRequests              = 10;
                _clientOptions.RequestQueueCapacity               = 100;
                _featureFlags.SetRolloutPercent(HttpFeatureFlags.ConnectionPool, 0);
            });

            await ExecuteStepAsync(record, "L3-ResetServerConcurrency", ct, () =>
            {
                // 恢复服务端并发参数为保守值
                _serverOptions.EnableAdaptiveConcurrency = false;
                _serverOptions.QueueWatermarkPercent     = 80;
                _featureFlags.SetRolloutPercent(HttpFeatureFlags.LargeFileOptimization, 0);
                _featureFlags.SetRolloutPercent(HttpFeatureFlags.ObjectPool, 0);
            });
        }

        /// <summary>
        /// Emergency（最快路径）：跳过分级，立即关闭所有优化。
        /// 目标：从触发指令到全部关闭 ≤ 5 秒。
        /// </summary>
        private async Task ExecuteEmergencyAsync(RollbackRecord record, CancellationToken ct)
        {
            await ExecuteStepAsync(record, "Emergency-DisableAllFlags", ct, () =>
            {
                _featureFlags.DisableAll();
            });

            await ExecuteStepAsync(record, "Emergency-DisableServerCompression", ct, () =>
            {
                _serverOptions.EnableCompression        = false;
                _serverOptions.EnableAdaptiveConcurrency = false;
            });

            await ExecuteStepAsync(record, "Emergency-DisableClientRetry", ct, () =>
            {
                if (_clientOptions.RetryPolicy != null)
                    _clientOptions.RetryPolicy.Enabled = false;
                _clientOptions.EnableAdaptiveConcurrency = false;
            });

            Logger.Warn("[Rollback] Emergency 紧急回滚完成，所有优化已关闭。请检查服务状态后再评估增量恢复。");
        }

        // ── 步骤执行包装 ──────────────────────────────────────────────────────────

        private async Task ExecuteStepAsync(
            RollbackRecord record,
            string stepName,
            CancellationToken ct,
            Action action)
        {
            var step = new RollbackStepResult
            {
                Name      = stepName,
                StartedAt = DateTime.UtcNow,
            };

            var sw = Stopwatch.StartNew();
            try
            {
                ct.ThrowIfCancellationRequested();
                action();
                step.Success  = true;
                step.Message  = "OK";
                Logger.Info($"[Rollback] 步骤 '{stepName}' 完成, {sw.ElapsedMilliseconds}ms");
            }
            catch (OperationCanceledException)
            {
                step.Success  = false;
                step.Message  = "已取消";
                Logger.Warn($"[Rollback] 步骤 '{stepName}' 被取消");
                throw;
            }
            catch (Exception ex)
            {
                step.Success  = false;
                step.Message  = ex.Message;
                Logger.Error($"[Rollback] 步骤 '{stepName}' 失败: {ex.Message}");
            }
            finally
            {
                step.DurationMs   = sw.ElapsedMilliseconds;
                step.CompletedAt  = DateTime.UtcNow;
                record.Steps.Add(step);
                OnStepCompleted?.Invoke(step);
            }

            // 给 async 调用链提供取消检查点（即使 action 是同步的）
            await Task.Yield();
        }

        // ── 历史记录 ──────────────────────────────────────────────────────────────

        private void AddToHistory(RollbackRecord record)
        {
            lock (_historyLock)
            {
                _history.Add(record);
                // 只保留最近 50 条，防止内存无限增长
                if (_history.Count > 50)
                    _history.RemoveAt(0);
            }
        }

        /// <summary>
        /// 获取回滚历史（只读副本）
        /// </summary>
        public IReadOnlyList<RollbackRecord> GetHistory()
        {
            lock (_historyLock)
            {
                return _history.ToArray();
            }
        }

        // ── 报告生成 ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 生成最近一次回滚的文本摘要，便于日志、告警通知。
        /// </summary>
        public string GenerateLastRollbackSummary()
        {
            RollbackRecord? last;
            lock (_historyLock)
            {
                if (_history.Count == 0) return "(无历史回滚记录)";
                last = _history[_history.Count - 1];
            }

            return FormatRollbackSummary(last);
        }

        internal static string FormatRollbackSummary(RollbackRecord record)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== 回滚报告 [{record.Id}] ===");
            sb.AppendLine($"级别      : {record.Level}");
            sb.AppendLine($"触发原因  : {record.Reason}");
            sb.AppendLine($"开始时间  : {record.StartedAt:u}");
            sb.AppendLine($"完成时间  : {record.CompletedAt:u}");
            sb.AppendLine($"总耗时    : {record.DurationMs}ms");
            sb.AppendLine($"结果      : {(record.Success ? "✓ 成功" : "✗ 失败")}");
            if (!string.IsNullOrEmpty(record.ErrorMessage))
                sb.AppendLine($"错误信息  : {record.ErrorMessage}");

            sb.AppendLine($"--- 步骤明细 ---");
            foreach (var step in record.Steps)
            {
                var icon = step.Success ? "✓" : "✗";
                sb.AppendLine($"  {icon} [{step.DurationMs,4}ms] {step.Name}: {step.Message}");
            }
            return sb.ToString();
        }
    }

    // ── 枚举与数据模型 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 回滚级别定义
    /// </summary>
    public enum RollbackLevel
    {
        /// <summary>
        /// 一级回滚（~10s）：仅关闭高风险优化（压缩、重试）。
        /// 适用：CPU/带宽异常，影响范围小。
        /// </summary>
        Level1 = 1,

        /// <summary>
        /// 二级回滚（~30s）：在 Level1 基础上额外禁用自适应并发与条件请求。
        /// 适用：排队延迟或 P99 异常。
        /// </summary>
        Level2 = 2,

        /// <summary>
        /// 三级回滚（~60s）：关闭全部优化，连接池/并发恢复为保守默认值。
        /// 适用：多项异常并发，完全隔离变量。
        /// </summary>
        Level3 = 3,

        /// <summary>
        /// 紧急回滚（≤5s）：最快路径，立即关闭所有优化。
        /// 适用：严重故障、需立即止损。
        /// </summary>
        Emergency = 99,
    }

    /// <summary>
    /// 单次回滚操作的完整记录（审计用）
    /// </summary>
    public sealed class RollbackRecord
    {
        public string Id          { get; internal set; } = string.Empty;
        public RollbackLevel Level{ get; internal set; }
        public string Reason      { get; internal set; } = string.Empty;
        public DateTime StartedAt { get; internal set; }
        public DateTime CompletedAt { get; internal set; }
        public long DurationMs    { get; internal set; }
        public bool Success       { get; internal set; }
        public string? ErrorMessage { get; internal set; }
        public List<RollbackStepResult> Steps { get; internal set; } = new();
    }

    /// <summary>
    /// 单个回滚步骤的执行结果
    /// </summary>
    public sealed class RollbackStepResult
    {
        public string Name        { get; internal set; } = string.Empty;
        public DateTime StartedAt { get; internal set; }
        public DateTime CompletedAt { get; internal set; }
        public long DurationMs    { get; internal set; }
        public bool Success       { get; internal set; }
        public string? Message    { get; internal set; }
    }
}

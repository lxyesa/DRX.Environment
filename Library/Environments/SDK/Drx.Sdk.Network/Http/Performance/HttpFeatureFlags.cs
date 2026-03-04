using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Drx.Sdk.Shared;

namespace Drx.Sdk.Network.Http.Performance
{
    /// <summary>
    /// HTTP 性能优化功能开关（Feature Flags）与灰度策略管理。
    ///
    /// 覆盖所有关键优化项（对应 R7）：
    ///   - Compression：响应压缩
    ///   - ConditionalRequest：条件请求（304 优化）
    ///   - RetryPolicy：幂等请求自动重试
    ///   - AdaptiveConcurrency：自适应并发控制
    ///   - LargeFileOptimization：大文件传输优化
    ///   - ObjectPool：对象池 / 缓冲复用
    ///   - ConnectionPool：连接池高级策略
    ///
    /// 设计特性：
    ///   1. 每项优化独立开关，可精细控制而不影响其他项。
    ///   2. 支持灰度百分比（0~100）：0 = 完全关闭，100 = 全量开放。
    ///   3. 灰度基于 instanceId 的哈希（稳定路由，同一实例在同一批次内行为一致）。
    ///   4. 支持从 JSON 配置文件热加载，无需重启进程（<see cref="ReloadFromFile"/>）。
    ///   5. 所有状态读写均线程安全（Volatile.Read/Write + lock）。
    ///   6. 触发 <see cref="OnFlagChanged"/> 事件通知，便于依赖组件动态响应。
    ///
    /// 快速回滚方式（符合 5 分钟目标）：
    ///   - 方案 A：调用 <see cref="DisableAll"/> 一键关闭所有优化，回退到历史基线行为。
    ///   - 方案 B：调用 <see cref="SetRolloutPercent"/> 将目标 flag 的灰度比例设为 0。
    ///   - 方案 C：更新配置文件后调用 <see cref="ReloadFromFile"/>（配置中心 / Git 推送均可触发）。
    ///   - 方案 D：通过 <see cref="HttpRollbackOrchestrator"/> 编排多步回滚流程（推荐生产环境）。
    /// </summary>
    public sealed class HttpFeatureFlags
    {
        // ── 单例 ────────────────────────────────────────────────────────────────

        private static HttpFeatureFlags? _instance;
        private static readonly object _instanceLock = new();

        /// <summary>
        /// 全局单例实例。
        /// 调用方可通过此实例统一读取/修改所有 feature flag。
        /// </summary>
        public static HttpFeatureFlags Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        _instance ??= new HttpFeatureFlags();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 替换全局单例（测试/集成场景使用）。
        /// </summary>
        public static void SetInstance(HttpFeatureFlags flags)
        {
            lock (_instanceLock)
            {
                _instance = flags ?? throw new ArgumentNullException(nameof(flags));
            }
        }

        // ── Feature Flag 状态 ────────────────────────────────────────────────────

        private readonly Dictionary<string, FeatureFlagEntry> _flags;
        private readonly object _flagsLock = new();

        /// <summary>
        /// Flag 变更通知事件（flagName, oldPercent, newPercent）
        /// </summary>
        public event Action<string, int, int>? OnFlagChanged;

        // ── 已知 Flag 名称常量 ────────────────────────────────────────────────────

        /// <summary>响应压缩（Gzip/Brotli）开关</summary>
        public const string Compression = "Compression";

        /// <summary>条件请求自动化（ETag / If-None-Match / 304）开关</summary>
        public const string ConditionalRequest = "ConditionalRequest";

        /// <summary>幂等请求自动重试（GET/HEAD 指数退避）开关</summary>
        public const string RetryPolicy = "RetryPolicy";

        /// <summary>服务端自适应并发控制（AIMD）开关</summary>
        public const string AdaptiveConcurrency = "AdaptiveConcurrency";

        /// <summary>大文件传输分片 + 缓冲池优化开关</summary>
        public const string LargeFileOptimization = "LargeFileOptimization";

        /// <summary>对象池 / 缓冲区复用优化开关</summary>
        public const string ObjectPool = "ObjectPool";

        /// <summary>连接池高级配置（MaxConnectionsPerServer 等）开关</summary>
        public const string ConnectionPool = "ConnectionPool";

        // ── 构造 ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// 创建 HttpFeatureFlags，默认所有优化项全量启用（100% 灰度）。
        /// </summary>
        public HttpFeatureFlags()
        {
            _flags = new Dictionary<string, FeatureFlagEntry>(StringComparer.OrdinalIgnoreCase)
            {
                [Compression]           = new FeatureFlagEntry(Compression,           rolloutPercent: 100),
                [ConditionalRequest]    = new FeatureFlagEntry(ConditionalRequest,    rolloutPercent: 100),
                [RetryPolicy]           = new FeatureFlagEntry(RetryPolicy,           rolloutPercent: 100),
                [AdaptiveConcurrency]   = new FeatureFlagEntry(AdaptiveConcurrency,   rolloutPercent: 100),
                [LargeFileOptimization] = new FeatureFlagEntry(LargeFileOptimization, rolloutPercent: 100),
                [ObjectPool]            = new FeatureFlagEntry(ObjectPool,            rolloutPercent: 100),
                [ConnectionPool]        = new FeatureFlagEntry(ConnectionPool,        rolloutPercent: 100),
            };
        }

        /// <summary>
        /// 创建"安全基线"预设：所有优化项关闭（灰度=0），用于快速回滚到未优化状态。
        /// </summary>
        public static HttpFeatureFlags SafeBaseline()
        {
            var flags = new HttpFeatureFlags();
            flags.DisableAll();
            return flags;
        }

        /// <summary>
        /// 创建"小流量灰度"预设：所有优化项灰度 10%，用于金丝雀发布验证。
        /// </summary>
        public static HttpFeatureFlags SmallTrafficCanary()
        {
            var flags = new HttpFeatureFlags();
            flags.SetAllRolloutPercent(10);
            return flags;
        }

        // ── 核心读取方法 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 判断指定 flag 对当前实例是否启用。
        /// 基于 instanceId 的稳定哈希路由，保证同一实例在同一灰度批次内行为一致。
        /// </summary>
        /// <param name="flagName">feature flag 名称（使用常量）</param>
        /// <param name="instanceId">实例标识（可用 ConnectionId、UserId、SessionId 等）。
        ///   null 时使用随机数（非稳定路由，适合无状态场景）。</param>
        public bool IsEnabled(string flagName, string? instanceId = null)
        {
            FeatureFlagEntry entry;
            lock (_flagsLock)
            {
                if (!_flags.TryGetValue(flagName, out entry))
                    return false; // 未知 flag：保守默认关闭
            }

            if (!entry.Enabled) return false;
            if (entry.RolloutPercent <= 0) return false;
            if (entry.RolloutPercent >= 100) return true;

            // 灰度路由：对 instanceId 取哈希，稳定分桶
            int bucket = instanceId != null
                ? (Math.Abs(instanceId.GetHashCode()) % 100)
                : (Environment.TickCount % 100);

            return bucket < entry.RolloutPercent;
        }

        /// <summary>
        /// 直接查询 flag 的"启用+全量"状态（不走灰度路由），供服务端全局配置读取时使用。
        /// </summary>
        public bool IsFullyEnabled(string flagName)
        {
            lock (_flagsLock)
            {
                return _flags.TryGetValue(flagName, out var entry) && entry.Enabled && entry.RolloutPercent >= 100;
            }
        }

        // ── 修改方法 ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 设置指定 flag 的灰度百分比（0=关闭，100=全量）。
        /// </summary>
        public void SetRolloutPercent(string flagName, int percent)
        {
            percent = Math.Clamp(percent, 0, 100);
            int oldPercent;

            lock (_flagsLock)
            {
                if (!_flags.TryGetValue(flagName, out var entry))
                {
                    _flags[flagName] = new FeatureFlagEntry(flagName, rolloutPercent: percent);
                    Logger.Info($"[FeatureFlags] 新增 flag '{flagName}'，灰度={percent}%");
                    OnFlagChanged?.Invoke(flagName, -1, percent);
                    return;
                }

                oldPercent = entry.RolloutPercent;
                entry.RolloutPercent = percent;
                if (percent == 0) entry.Enabled = false;
                else if (!entry.Enabled) entry.Enabled = true;
            }

            Logger.Info($"[FeatureFlags] flag '{flagName}' 灰度设置: {oldPercent}% → {percent}%");
            OnFlagChanged?.Invoke(flagName, oldPercent, percent);
        }

        /// <summary>
        /// 强制启用/禁用指定 flag（不影响灰度百分比）。
        /// </summary>
        public void SetEnabled(string flagName, bool enabled)
        {
            int oldPercent;
            lock (_flagsLock)
            {
                if (!_flags.TryGetValue(flagName, out var entry)) return;
                oldPercent = entry.RolloutPercent;
                entry.Enabled = enabled;
            }

            var state = enabled ? "启用" : "禁用";
            Logger.Info($"[FeatureFlags] flag '{flagName}' 已{state}（灰度={oldPercent}%）");
            OnFlagChanged?.Invoke(flagName, oldPercent, enabled ? oldPercent : 0);
        }

        /// <summary>
        /// 关闭所有 feature flags（一键回滚到安全基线）。
        /// </summary>
        public void DisableAll()
        {
            lock (_flagsLock)
            {
                foreach (var entry in _flags.Values)
                {
                    entry.Enabled = false;
                    entry.RolloutPercent = 0;
                }
            }
            Logger.Warn("[FeatureFlags] 所有功能开关已关闭（安全基线模式）");
        }

        /// <summary>
        /// 为所有 flags 统一设置灰度百分比。
        /// </summary>
        public void SetAllRolloutPercent(int percent)
        {
            percent = Math.Clamp(percent, 0, 100);
            lock (_flagsLock)
            {
                foreach (var entry in _flags.Values)
                {
                    entry.RolloutPercent = percent;
                    entry.Enabled = percent > 0;
                }
            }
            Logger.Info($"[FeatureFlags] 所有 flags 灰度统一设置为 {percent}%");
        }

        /// <summary>
        /// 恢复所有 flags 为全量启用状态。
        /// </summary>
        public void EnableAll()
        {
            lock (_flagsLock)
            {
                foreach (var entry in _flags.Values)
                {
                    entry.Enabled = true;
                    entry.RolloutPercent = 100;
                }
            }
            Logger.Info("[FeatureFlags] 所有功能开关已全量启用");
        }

        // ── 快照与热加载 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 获取所有 flags 的当前状态快照（只读副本）。
        /// </summary>
        public IReadOnlyList<FeatureFlagSnapshot> GetSnapshot()
        {
            lock (_flagsLock)
            {
                var list = new List<FeatureFlagSnapshot>(_flags.Count);
                foreach (var entry in _flags.Values)
                {
                    list.Add(new FeatureFlagSnapshot
                    {
                        Name          = entry.Name,
                        Enabled       = entry.Enabled,
                        RolloutPercent = entry.RolloutPercent,
                        Description   = entry.Description,
                        LastModified  = entry.LastModified,
                    });
                }
                return list;
            }
        }

        /// <summary>
        /// 将当前 flags 状态序列化为 JSON 字符串（用于持久化或推送到配置中心）。
        /// </summary>
        public string ToJson()
        {
            var snapshot = GetSnapshot();
            return JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            });
        }

        /// <summary>
        /// 从 JSON 字符串热加载 flag 状态（不重启进程即可生效）。
        /// 仅更新已存在的 flag，不会删除未在 JSON 中出现的 flag。
        /// </summary>
        public void LoadFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            var items = JsonSerializer.Deserialize<List<FeatureFlagSnapshot>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

            if (items == null) return;

            lock (_flagsLock)
            {
                foreach (var item in items)
                {
                    if (_flags.TryGetValue(item.Name, out var entry))
                    {
                        entry.Enabled       = item.Enabled;
                        entry.RolloutPercent = Math.Clamp(item.RolloutPercent, 0, 100);
                        entry.Description   = item.Description;
                        entry.LastModified  = DateTime.UtcNow;
                    }
                }
            }

            Logger.Info($"[FeatureFlags] 从 JSON 热加载 {items.Count} 个 flags");
        }

        /// <summary>
        /// 从本地 JSON 文件热加载（支持文件监听或定期轮询触发）。
        /// </summary>
        public void ReloadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Logger.Warn($"[FeatureFlags] 配置文件不存在: {filePath}");
                return;
            }

            var json = File.ReadAllText(filePath, Encoding.UTF8);
            LoadFromJson(json);
            Logger.Info($"[FeatureFlags] 从文件加载完毕: {filePath}");
        }

        /// <summary>
        /// 将当前状态保存到本地 JSON 文件。
        /// </summary>
        public void SaveToFile(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(filePath, ToJson(), Encoding.UTF8);
            Logger.Info($"[FeatureFlags] 当前 flags 已保存到: {filePath}");
        }
    }

    // ── 内部可变状态 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Feature flag 运行时条目（可变，内部使用）。
    /// </summary>
    internal sealed class FeatureFlagEntry
    {
        public string Name { get; }
        public bool Enabled { get; set; }
        public int RolloutPercent { get; set; }
        public string? Description { get; set; }
        public DateTime LastModified { get; set; }

        public FeatureFlagEntry(string name, bool enabled = true, int rolloutPercent = 100, string? description = null)
        {
            Name           = name;
            Enabled        = enabled;
            RolloutPercent = rolloutPercent;
            Description    = description;
            LastModified   = DateTime.UtcNow;
        }
    }

    // ── 只读快照（序列化 / 事件通知） ─────────────────────────────────────────────

    /// <summary>
    /// Feature flag 状态快照（只读，用于序列化和报告）。
    /// </summary>
    public sealed class FeatureFlagSnapshot
    {
        /// <summary>flag 名称</summary>
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        /// <summary>是否启用</summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; init; }

        /// <summary>灰度百分比（0~100）</summary>
        [JsonPropertyName("rolloutPercent")]
        public int RolloutPercent { get; init; }

        /// <summary>描述</summary>
        [JsonPropertyName("description")]
        public string? Description { get; init; }

        /// <summary>最后修改时间</summary>
        [JsonPropertyName("lastModified")]
        public DateTime LastModified { get; init; }
    }
}

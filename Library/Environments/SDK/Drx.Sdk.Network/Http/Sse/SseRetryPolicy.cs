using System;

namespace Drx.Sdk.Network.Http.Sse
{
    /// <summary>
    /// SSE 自动重连策略
    /// </summary>
    public class SseRetryPolicy
    {
        /// <summary>
        /// 初始重连延迟（毫秒）
        /// </summary>
        public int InitialDelayMs { get; set; } = 1000;

        /// <summary>
        /// 最大重连延迟（毫秒）
        /// </summary>
        public int MaxDelayMs { get; set; } = 30000;

        /// <summary>
        /// 延迟递增倍率
        /// </summary>
        public double Multiplier { get; set; } = 2.0;

        /// <summary>
        /// 最大重试次数，-1 表示无限重试
        /// </summary>
        public int MaxRetries { get; set; } = -1;

        /// <summary>
        /// 默认策略：指数退避，1s 起步，最大 30s，无限重试
        /// </summary>
        public static SseRetryPolicy Default => new();

        /// <summary>
        /// 创建指数退避策略
        /// </summary>
        public static SseRetryPolicy ExponentialBackoff(int initialMs = 1000, int maxMs = 30000, double multiplier = 2.0, int maxRetries = -1)
        {
            return new SseRetryPolicy
            {
                InitialDelayMs = initialMs,
                MaxDelayMs = maxMs,
                Multiplier = multiplier,
                MaxRetries = maxRetries
            };
        }

        /// <summary>
        /// 不重连策略
        /// </summary>
        public static SseRetryPolicy None => new() { MaxRetries = 0 };

        /// <summary>
        /// 根据当前重试次数计算延迟
        /// </summary>
        internal int CalculateDelay(int attempt)
        {
            var delay = (int)(InitialDelayMs * Math.Pow(Multiplier, attempt));
            return Math.Min(delay, MaxDelayMs);
        }
    }
}

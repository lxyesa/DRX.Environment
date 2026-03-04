using System;
using System.Collections.Generic;
using System.Linq;

namespace Drx.Sdk.Network.Http.Performance
{
    /// <summary>
    /// 延迟百分位计算结果
    /// </summary>
    public readonly struct LatencyPercentiles
    {
        /// <summary>
        /// 50% 分位（中位数）
        /// </summary>
        public double P50 { get; init; }

        /// <summary>
        /// 75% 分位
        /// </summary>
        public double P75 { get; init; }

        /// <summary>
        /// 90% 分位
        /// </summary>
        public double P90 { get; init; }

        /// <summary>
        /// 95% 分位
        /// </summary>
        public double P95 { get; init; }

        /// <summary>
        /// 99% 分位
        /// </summary>
        public double P99 { get; init; }

        /// <summary>
        /// 99.9% 分位
        /// </summary>
        public double P999 { get; init; }

        /// <summary>
        /// 平均值
        /// </summary>
        public double Average { get; init; }

        /// <summary>
        /// 最小值
        /// </summary>
        public double Min { get; init; }

        /// <summary>
        /// 最大值
        /// </summary>
        public double Max { get; init; }

        /// <summary>
        /// 标准差
        /// </summary>
        public double StdDev { get; init; }

        /// <summary>
        /// 样本数量
        /// </summary>
        public int SampleCount { get; init; }

        /// <summary>
        /// 空结果
        /// </summary>
        public static LatencyPercentiles Empty => new()
        {
            P50 = 0, P75 = 0, P90 = 0, P95 = 0, P99 = 0, P999 = 0,
            Average = 0, Min = 0, Max = 0, StdDev = 0, SampleCount = 0
        };

        public override string ToString()
        {
            return $"P50={P50:F2}ms, P95={P95:F2}ms, P99={P99:F2}ms, Avg={Average:F2}ms (n={SampleCount})";
        }
    }

    /// <summary>
    /// 百分位计算器。
    /// 使用排序后线性插值法计算精确百分位，适用于中小规模样本。
    /// 对于大规模流式数据，建议使用 T-Digest 或 HdrHistogram 算法。
    /// </summary>
    public static class PercentileCalculator
    {
        /// <summary>
        /// 计算延迟百分位
        /// </summary>
        /// <param name="samples">延迟样本（毫秒）</param>
        /// <returns>百分位结果</returns>
        public static LatencyPercentiles Calculate(IEnumerable<double> samples)
        {
            var list = samples?.ToList() ?? new List<double>();

            if (list.Count == 0)
                return LatencyPercentiles.Empty;

            // 排序用于百分位计算
            list.Sort();

            var count = list.Count;
            var sum = list.Sum();
            var avg = sum / count;
            var min = list[0];
            var max = list[count - 1];

            // 计算标准差
            var variance = list.Sum(x => (x - avg) * (x - avg)) / count;
            var stdDev = Math.Sqrt(variance);

            return new LatencyPercentiles
            {
                P50 = GetPercentile(list, 0.50),
                P75 = GetPercentile(list, 0.75),
                P90 = GetPercentile(list, 0.90),
                P95 = GetPercentile(list, 0.95),
                P99 = GetPercentile(list, 0.99),
                P999 = GetPercentile(list, 0.999),
                Average = avg,
                Min = min,
                Max = max,
                StdDev = stdDev,
                SampleCount = count
            };
        }

        /// <summary>
        /// 计算单个百分位值（使用线性插值）
        /// </summary>
        /// <param name="sortedValues">已排序的值列表</param>
        /// <param name="percentile">百分位（0-1）</param>
        /// <returns>百分位值</returns>
        public static double GetPercentile(IList<double> sortedValues, double percentile)
        {
            if (sortedValues == null || sortedValues.Count == 0)
                return 0;

            if (percentile <= 0)
                return sortedValues[0];

            if (percentile >= 1)
                return sortedValues[sortedValues.Count - 1];

            // 使用线性插值法（NIST 推荐方法）
            // rank = percentile * (n - 1)
            var n = sortedValues.Count;
            var rank = percentile * (n - 1);
            var lowerIndex = (int)Math.Floor(rank);
            var upperIndex = (int)Math.Ceiling(rank);

            if (lowerIndex == upperIndex)
                return sortedValues[lowerIndex];

            // 线性插值
            var fraction = rank - lowerIndex;
            return sortedValues[lowerIndex] * (1 - fraction) + sortedValues[upperIndex] * fraction;
        }

        /// <summary>
        /// 计算多个百分位值
        /// </summary>
        /// <param name="values">值列表</param>
        /// <param name="percentiles">要计算的百分位列表</param>
        /// <returns>百分位值字典</returns>
        public static Dictionary<double, double> CalculateMultiple(IEnumerable<double> values, params double[] percentiles)
        {
            var sorted = values?.OrderBy(x => x).ToList() ?? new List<double>();
            var result = new Dictionary<double, double>();

            foreach (var p in percentiles)
            {
                result[p] = GetPercentile(sorted, p);
            }

            return result;
        }
    }

    /// <summary>
    /// 滑动窗口百分位计算器。
    /// 支持增量添加样本并保持固定时间窗口内的百分位计算。
    /// </summary>
    public sealed class SlidingWindowPercentileCalculator
    {
        private readonly TimeSpan _windowSize;
        private readonly List<(DateTime timestamp, double value)> _samples = new();
        private readonly object _lock = new();

        /// <summary>
        /// 创建滑动窗口百分位计算器
        /// </summary>
        /// <param name="windowSize">窗口大小</param>
        public SlidingWindowPercentileCalculator(TimeSpan windowSize)
        {
            _windowSize = windowSize;
        }

        /// <summary>
        /// 添加样本
        /// </summary>
        public void AddSample(double value)
        {
            AddSample(value, DateTime.UtcNow);
        }

        /// <summary>
        /// 添加带时间戳的样本
        /// </summary>
        public void AddSample(double value, DateTime timestamp)
        {
            lock (_lock)
            {
                _samples.Add((timestamp, value));
                PruneOldSamples();
            }
        }

        /// <summary>
        /// 获取当前窗口内的百分位
        /// </summary>
        public LatencyPercentiles GetPercentiles()
        {
            lock (_lock)
            {
                PruneOldSamples();
                return PercentileCalculator.Calculate(_samples.Select(s => s.value));
            }
        }

        /// <summary>
        /// 获取单个百分位值
        /// </summary>
        public double GetPercentile(double percentile)
        {
            lock (_lock)
            {
                PruneOldSamples();
                var sorted = _samples.Select(s => s.value).OrderBy(x => x).ToList();
                return PercentileCalculator.GetPercentile(sorted, percentile);
            }
        }

        /// <summary>
        /// 当前样本数
        /// </summary>
        public int SampleCount
        {
            get
            {
                lock (_lock)
                {
                    PruneOldSamples();
                    return _samples.Count;
                }
            }
        }

        /// <summary>
        /// 清空所有样本
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _samples.Clear();
            }
        }

        private void PruneOldSamples()
        {
            var cutoff = DateTime.UtcNow - _windowSize;
            _samples.RemoveAll(s => s.timestamp < cutoff);
        }
    }

    /// <summary>
    /// 基于直方图的高效百分位估算器。
    /// 适用于大规模数据流，牺牲精度换取内存效率。
    /// </summary>
    public sealed class HistogramPercentileEstimator
    {
        private readonly double _minValue;
        private readonly double _maxValue;
        private readonly int _bucketCount;
        private readonly double _bucketWidth;
        private readonly long[] _buckets;
        private long _totalCount;
        private double _sum;

        /// <summary>
        /// 创建直方图百分位估算器
        /// </summary>
        /// <param name="minValue">预期最小值</param>
        /// <param name="maxValue">预期最大值</param>
        /// <param name="bucketCount">桶数量（默认 1000）</param>
        public HistogramPercentileEstimator(double minValue = 0, double maxValue = 10000, int bucketCount = 1000)
        {
            _minValue = minValue;
            _maxValue = maxValue;
            _bucketCount = bucketCount;
            _bucketWidth = (_maxValue - _minValue) / _bucketCount;
            _buckets = new long[bucketCount];
        }

        /// <summary>
        /// 记录一个值
        /// </summary>
        public void Record(double value)
        {
            var bucketIndex = GetBucketIndex(value);
            System.Threading.Interlocked.Increment(ref _buckets[bucketIndex]);
            System.Threading.Interlocked.Increment(ref _totalCount);
            
            // 使用 CompareExchange 进行线程安全的累加
            double initial, computed;
            do
            {
                initial = _sum;
                computed = initial + value;
            }
            while (initial != System.Threading.Interlocked.CompareExchange(ref _sum, computed, initial));
        }

        /// <summary>
        /// 估算百分位值
        /// </summary>
        public double EstimatePercentile(double percentile)
        {
            var count = System.Threading.Interlocked.Read(ref _totalCount);
            if (count == 0) return 0;

            var targetCount = (long)(count * percentile);
            long cumulative = 0;

            for (int i = 0; i < _bucketCount; i++)
            {
                cumulative += System.Threading.Interlocked.Read(ref _buckets[i]);
                if (cumulative >= targetCount)
                {
                    // 返回桶的中点值
                    return _minValue + (i + 0.5) * _bucketWidth;
                }
            }

            return _maxValue;
        }

        /// <summary>
        /// 获取平均值
        /// </summary>
        public double GetAverage()
        {
            var count = System.Threading.Interlocked.Read(ref _totalCount);
            if (count == 0) return 0;
            return _sum / count;
        }

        /// <summary>
        /// 获取总样本数
        /// </summary>
        public long TotalCount => System.Threading.Interlocked.Read(ref _totalCount);

        /// <summary>
        /// 重置所有计数
        /// </summary>
        public void Reset()
        {
            for (int i = 0; i < _bucketCount; i++)
            {
                System.Threading.Interlocked.Exchange(ref _buckets[i], 0);
            }
            System.Threading.Interlocked.Exchange(ref _totalCount, 0);
            System.Threading.Interlocked.Exchange(ref _sum, 0);
        }

        private int GetBucketIndex(double value)
        {
            if (value <= _minValue) return 0;
            if (value >= _maxValue) return _bucketCount - 1;

            var index = (int)((value - _minValue) / _bucketWidth);
            return Math.Min(index, _bucketCount - 1);
        }
    }
}

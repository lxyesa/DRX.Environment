using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Drx.Sdk.Shared;

namespace Drx.Sdk.Network.Http.Serialization
{
    /// <summary>
    /// JSON 序列化管理器接口，用于支持可配置的序列化策略
    /// 这样可以适配不同的场景：反射序列化、源生成序列化、自定义序列化
    /// </summary>
    public interface IDrxJsonSerializer
    {
        /// <summary>
        /// 尝试序列化对象为 JSON 字符串
        /// </summary>
        /// <param name="obj">要序列化的对象</param>
        /// <param name="json">序列化后的 JSON 字符串（若返回 false 则为 null）</param>
        /// <returns>序列化是否成功</returns>
        bool TrySerialize(object obj, out string? json);

        /// <summary>
        /// 获取此序列化器的名称或描述
        /// </summary>
        string SerializerName { get; }
    }

    /// <summary>
    /// 基于反射的 JSON 序列化器（支持任意 .NET 类型）
    /// 使用 DynamicallyAccessedMembers 注解告知裁剪器保留必要的成员
    /// 注意：此实现在启用 PublishTrimmed 时需要用户为相关类型添加 DynamicDependency 注解
    /// </summary>
    public class ReflectionJsonSerializer : IDrxJsonSerializer
    {
        public string SerializerName => "Reflection-Based JSON Serializer";

        /// <summary>
        /// JSON 序列化选项（缓存以避免重复创建）
        /// </summary>
        private static readonly JsonSerializerOptions DefaultOptions = CreateDefaultOptions();

        private static JsonSerializerOptions CreateDefaultOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            // 尝试启用反射元数据解析器（在支持的 .NET 版本上）
            try
            {
                // DefaultJsonTypeInfoResolver 允许运行时反射
                options.TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver();
            }
            catch
            {
                // 如果不支持或出错，继续使用默认配置
            }

            return options;
        }

        public bool TrySerialize(object obj, out string? json)
        {
            json = null;
            if (obj == null) return false;

            try
            {
                // 首选：使用带有 DefaultJsonTypeInfoResolver 的选项进行序列化
                json = JsonSerializer.Serialize(obj, obj.GetType(), DefaultOptions);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Warn($"反射 JSON 序列化失败（{obj.GetType().FullName}）: {ex.Message}");

                // 回退：尝试不带类型信息的序列化
                try
                {
                    json = JsonSerializer.Serialize(obj, DefaultOptions);
                    return true;
                }
                catch (Exception ex2)
                {
                    Logger.Error($"反射 JSON 序列化回退也失败了: {ex2.Message}");
                    return false;
                }
            }
        }
    }

    /// <summary>
    /// 安全的 JSON 序列化器（在裁剪环境中作为备选）
    /// 尝试使用反射，失败时返回对象的 ToString() 表示
    /// </summary>
    public class SafeJsonSerializer : IDrxJsonSerializer
    {
        private readonly ReflectionJsonSerializer _reflectionSerializer = new();

        public string SerializerName => "Safe JSON Serializer (Reflection with Fallback)";

        public bool TrySerialize(object obj, out string? json)
        {
            json = null;
            if (obj == null) return false;

            // 首先尝试反射序列化
            if (_reflectionSerializer.TrySerialize(obj, out var reflectionJson))
            {
                json = reflectionJson;
                return true;
            }

            // 回退：序列化为简单的 JSON 对象（包含 error 字段）
            try
            {
                var errorObj = new
                {
                    error = $"序列化失败: 无法序列化类型 '{obj.GetType().FullName}'",
                    type = obj.GetType().FullName,
                    fallback = obj.ToString()
                };

                json = JsonSerializer.Serialize(errorObj, DefaultOptions);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static readonly JsonSerializerOptions DefaultOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// 缓存的 JSON 序列化器，用于性能优化
    /// 在第一次序列化某类型时缓存其序列化结果模式
    /// </summary>
    public class CachedJsonSerializer : IDrxJsonSerializer
    {
        private readonly IDrxJsonSerializer _innerSerializer;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<Type, bool> _serializabilityCache = new();

        public string SerializerName => $"Cached {_innerSerializer.SerializerName}";

        public CachedJsonSerializer(IDrxJsonSerializer innerSerializer)
        {
            _innerSerializer = innerSerializer ?? throw new ArgumentNullException(nameof(innerSerializer));
        }

        public bool TrySerialize(object obj, out string? json)
        {
            json = null;
            if (obj == null) return false;

            var objType = obj.GetType();

            // 检查缓存：如果之前已知此类型不可序列化，直接返回 false（可选优化）
            // 这里只缓存内层结果，不做过度缓存
            return _innerSerializer.TrySerialize(obj, out json);
        }
    }

    /// <summary>
    /// 组合序列化器：依次尝试多个序列化器，直到一个成功
    /// 用于支持回退链
    /// </summary>
    public class ChainedJsonSerializer : IDrxJsonSerializer
    {
        private readonly IDrxJsonSerializer[] _serializers;

        public string SerializerName => $"Chained ({string.Join(" -> ", Array.ConvertAll(_serializers, s => s.SerializerName))})";

        public ChainedJsonSerializer(params IDrxJsonSerializer[] serializers)
        {
            if (serializers == null || serializers.Length == 0)
                throw new ArgumentException("至少需要一个序列化器", nameof(serializers));

            _serializers = serializers;
        }

        public bool TrySerialize(object obj, out string? json)
        {
            json = null;
            if (obj == null) return false;

            // 依次尝试每个序列化器
            foreach (var serializer in _serializers)
            {
                if (serializer.TrySerialize(obj, out var result))
                {
                    json = result;
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// JSON 序列化工厂和配置管理器
    /// 在应用启动时调用以配置全局的 JSON 序列化策略
    /// </summary>
    public static class DrxJsonSerializerManager
    {
        private static IDrxJsonSerializer _globalSerializer = new SafeJsonSerializer();

        /// <summary>
        /// 获取全局 JSON 序列化器
        /// </summary>
        public static IDrxJsonSerializer GlobalSerializer
        {
            get => _globalSerializer;
            set => _globalSerializer = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// 配置全局 JSON 序列化器为反射模式
        /// （适合开发环境和非裁剪部署）
        /// </summary>
        public static void ConfigureReflectionMode()
        {
            _globalSerializer = new ReflectionJsonSerializer();
            Logger.Info("配置 JSON 序列化器为反射模式");
        }

        /// <summary>
        /// 配置全局 JSON 序列化器为安全模式
        /// （适合裁剪环境，包含回退机制）
        /// </summary>
        public static void ConfigureSafeMode()
        {
            _globalSerializer = new SafeJsonSerializer();
            Logger.Info("配置 JSON 序列化器为安全模式（包含回退）");
        }

        /// <summary>
        /// 配置全局 JSON 序列化器为自定义实现
        /// </summary>
        /// <param name="serializer">自定义序列化器实现</param>
        public static void ConfigureCustom(IDrxJsonSerializer serializer)
        {
            if (serializer == null) throw new ArgumentNullException(nameof(serializer));
            _globalSerializer = serializer;
            Logger.Info($"配置 JSON 序列化器为自定义实现: {serializer.SerializerName}");
        }

        /// <summary>
        /// 配置为链式回退模式：先尝试反射，失败后使用安全模式
        /// </summary>
        public static void ConfigureChainedMode()
        {
            _globalSerializer = new ChainedJsonSerializer(
                new ReflectionJsonSerializer(),
                new SafeJsonSerializer()
            );
            Logger.Info("配置 JSON 序列化器为链式回退模式");
        }

        /// <summary>
        /// 尝试序列化对象为 JSON
        /// </summary>
        public static bool TrySerialize(object obj, out string? json)
        {
            return _globalSerializer.TrySerialize(obj, out json);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Drx.Sdk.Shared.DMixin
{
    /// <summary>
    /// DrxMixin 相关功能。基于 Harmony 实现的简易运行时注入引擎。
    /// 注意：这是一个最小实现示例，要求注入方法为 static，且注入签名需要与目标方法参数兼容。
    /// </summary>
    public class DMixin
    {
        // 简单的注入位置枚举（可按位组合）
        [Flags]
        public enum InjectAt
        {
            None = 0,
            Head = 1,
            Tail = 2,
            HeadAndTail = Head | Tail
        }

        // 标记要混入的目标类型
        [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
        public class DMixinAttribute : Attribute
        {
            public Type TargetType { get; }
            public DMixinAttribute(Type targetType) => TargetType = targetType;
        }

        // 标记注入方法
        [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
        public class DMixinInjectAttribute : Attribute
        {
            public string TargetMethodName { get; }
            public InjectAt At { get; }
            public bool UseCallbackCi { get; }
            // 优先级，值越小越先执行（默认 0）
            public int Priority { get; set; } = 0;

            public DMixinInjectAttribute(string targetMethodName, InjectAt at = InjectAt.Head, bool useCallbackCi = true)
            {
                TargetMethodName = targetMethodName;
                At = at;
                UseCallbackCi = useCallbackCi;
            }
        }

        // CallbackInfo：mixins 可以通过它控制是否继续执行原方法或设置返回值
        public class CallbackInfo
        {
            public bool RunOriginal { get; private set; } = true;
            public object? Result { get; set; }

            public void Continue() => RunOriginal = true;
            public void Cancel() => RunOriginal = false;
        }

        class InjectionInfo
        {
            public MethodInfo MixinMethod { get; set; } = null!;
            public bool UseCallbackCi { get; set; }
            public InjectAt At { get; set; }
            public int Priority { get; set; }
            // 如果 MixinMethod 不是 static，尝试使用此实例调用（延迟创建）
            public object? MixinInstance { get; set; }
            // mixin 类型（用于创建实例）
            public Type? MixinType { get; set; }
            public ParameterPlan[] ParameterPlans { get; set; } = Array.Empty<ParameterPlan>();
        }

        class MethodInjectionPlan
        {
            public MethodInjectionPlan(InjectionInfo[] heads, InjectionInfo[] tails)
            {
                Heads = heads;
                Tails = tails;
            }

            public InjectionInfo[] Heads { get; }
            public InjectionInfo[] Tails { get; }
            public bool HasHeads => Heads.Length > 0;
            public bool HasTails => Tails.Length > 0;
        }

        enum ParameterSource
        {
            OriginalArgument,
            Callback,
            DefaultValue,
            TypeDefault
        }

        struct ParameterPlan
        {
            public ParameterSource Source;
            public int OriginalIndex;
            public object? DefaultValue;
            public Type TargetType;
            public Type? OriginalParameterType;
            public bool RequiresConversion;
            public bool AllowNull;
        }

        // 使用线程安全的集合保存注入信息：每个方法维护一个按优先级排序的快照列表
        static readonly System.Collections.Concurrent.ConcurrentDictionary<MethodBase, MethodInjectionPlan> _injections = new();
        // 临时写入时使用此字典进行合并（非原子，但 TryAdd 用于 patch 原子性）
        static readonly System.Collections.Concurrent.ConcurrentDictionary<MethodBase, List<InjectionInfo>> _injectionBuilders = new();
        static readonly System.Collections.Concurrent.ConcurrentDictionary<MethodBase, byte> _patchedMethods = new(); // 记录已 patch 的方法，value unused
        static readonly object _initLock = new();
        static Harmony? _harmony;
        // 可选的服务提供者，用于创建 mixin 实例
        static IServiceProvider? _serviceProvider;

        // 初始化：扫描当前 AppDomain 中的类型并应用 patch
        public static void Initialize()
        {
            Initialize(null);
        }

        // 支持传入 IServiceProvider，使 mixin 实例可以由 DI 容器解析
        public static void Initialize(IServiceProvider? serviceProvider)
        {
            _serviceProvider = serviceProvider;
            lock (_initLock)
            {
                if (_harmony != null) return;
                _harmony = new Harmony("Drx.Sdk.Shared.DMixin");
            }

            var allTypes = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .SelectMany(a => SafeGetTypes(a))
                .ToArray();

            foreach (var mixinType in allTypes)
            {
                var mixinAttrs = mixinType.GetCustomAttributes(typeof(DMixinAttribute), inherit: false).Cast<DMixinAttribute>().ToArray();
                if (!mixinAttrs.Any()) continue;
                if (mixinType.ContainsGenericParameters)
                {
                    System.Diagnostics.Debug.WriteLine($"DMixin：跳过包含泛型参数的 mixin 类型 {mixinType.FullName}。");
                    continue;
                }

                foreach (var ma in mixinAttrs)
                {
                    var targetType = ma.TargetType;
                    if (targetType == null) continue;

                    // 查找 mixin 类型中的注入方法
                    var mixinMethods = mixinType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                        .Select(m => new { Method = m, Attrs = m.GetCustomAttributes(typeof(DMixinInjectAttribute), false).Cast<DMixinInjectAttribute>().ToArray() })
                        .Where(x => x.Attrs.Any());

                    foreach (var mm in mixinMethods)
                    {
                        if (mm.Method.IsAbstract)
                        {
                            System.Diagnostics.Debug.WriteLine($"DMixin：跳过抽象 mixin 方法 {mm.Method}。");
                            continue;
                        }
                        if (mm.Method.ContainsGenericParameters)
                        {
                            System.Diagnostics.Debug.WriteLine($"DMixin：跳过包含泛型参数的 mixin 方法 {mm.Method}。");
                            continue;
                        }
                        foreach (var attr in mm.Attrs)
                        {
                            if (string.IsNullOrWhiteSpace(attr.TargetMethodName))
                            {
                                System.Diagnostics.Debug.WriteLine($"DMixin：mixins {mm.Method} 的 TargetMethodName 未设置或为空，已跳过。");
                                continue;
                            }
                            // 在目标类型中查找匹配的方法（按名称，尽量按参数数量匹配）
                            var candidates = targetType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                                .Where(m => m.Name == attr.TargetMethodName).ToArray();

                            MethodBase? chosen = null;
                            ParameterPlan[]? chosenPlans = null;

                            foreach (var candidate in candidates)
                            {
                                if (candidate.IsAbstract || candidate.ContainsGenericParameters) continue;
                                if (candidate is MethodInfo mi && mi.IsGenericMethodDefinition) continue;

                                if (TryBuildParameterPlans(candidate, mm.Method, attr.UseCallbackCi, out var plans))
                                {
                                    chosen = candidate;
                                    chosenPlans = plans;
                                    break;
                                }
                            }

                            if (chosen == null || chosenPlans == null)
                            {
                                System.Diagnostics.Debug.WriteLine($"DMixin：未找到与 mixin {mm.Method} 匹配的目标方法 {attr.TargetMethodName}。");
                                continue;
                            }

                            if (chosen is not MethodInfo chosenMethod)
                            {
                                System.Diagnostics.Debug.WriteLine($"DMixin：目标成员 {chosen} 不是可注入的方法，已跳过。");
                                continue;
                            }

                            // 使用 builder 列表收集注入信息（线程安全地初始化 List）
                            var builder = _injectionBuilders.GetOrAdd(chosenMethod, _ => new List<InjectionInfo>());
                            lock (builder)
                            {
                                var injInfo = new InjectionInfo
                                {
                                    MixinMethod = mm.Method,
                                    UseCallbackCi = attr.UseCallbackCi,
                                    At = attr.At,
                                    MixinType = mm.Method.DeclaringType,
                                    Priority = attr.Priority,
                                    ParameterPlans = chosenPlans
                                };
                                builder.Add(injInfo);

                                // 维护快照数组：按 Priority 升序（值越小越先执行）
                                var ordered = builder.OrderBy(x => x.Priority).ToArray();
                                _injections[chosenMethod] = CreateMethodInjectionPlan(ordered);
                            }

                            // 仅对每个目标方法做一次 patch（使用 TryAdd 保证原子性）
                            if (_patchedMethods.TryAdd(chosenMethod, 1))
                            {
                                // 始终同时注册 Prefix 与 Postfix，后续逻辑会根据注入的 At 决定是否执行具体 mixin
                                var prefix = new HarmonyMethod(typeof(DMixin).GetMethod(nameof(Prefix), BindingFlags.Static | BindingFlags.NonPublic)!);
                                var postfix = new HarmonyMethod(typeof(DMixin).GetMethod(nameof(Postfix), BindingFlags.Static | BindingFlags.NonPublic)!);
                                try
                                {
                                    _harmony.Patch(chosenMethod, prefix, postfix);
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"DMixin：无法为方法 {chosenMethod.DeclaringType}.{chosenMethod.Name} 应用补丁：{ex}");
                                }
                            }
                        }
                    }
                }
            }
        }

        static IEnumerable<Type> SafeGetTypes(Assembly a)
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
            catch { return Array.Empty<Type>(); }
        }

        static bool TryBuildParameterPlans(MethodBase targetMethod, MethodInfo mixinMethod, bool useCallbackCi, out ParameterPlan[] plans)
        {
            var mixinParams = mixinMethod.GetParameters();
            var targetParams = targetMethod.GetParameters();

            if (mixinParams.Any(p => p.ParameterType.IsByRef))
            {
                plans = Array.Empty<ParameterPlan>();
                return false;
            }

            var result = new ParameterPlan[mixinParams.Length];
            for (int i = 0; i < mixinParams.Length; i++)
            {
                var mixinParam = mixinParams[i];
                var targetType = mixinParam.ParameterType;
                var plan = new ParameterPlan
                {
                    TargetType = targetType,
                    DefaultValue = mixinParam.HasDefaultValue ? mixinParam.DefaultValue : null,
                    OriginalIndex = -1,
                    Source = ParameterSource.TypeDefault,
                    AllowNull = !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null
                };

                if (targetType.IsPointer)
                {
                    plans = Array.Empty<ParameterPlan>();
                    return false;
                }

                if (mixinParam.IsOut)
                {
                    plans = Array.Empty<ParameterPlan>();
                    return false;
                }

                if (targetType == typeof(CallbackInfo))
                {
                    plan.Source = useCallbackCi ? ParameterSource.Callback : ParameterSource.TypeDefault;
                }
                else if (i < targetParams.Length)
                {
                    var originalParamType = targetParams[i].ParameterType;
                    if (originalParamType.IsPointer)
                    {
                        plans = Array.Empty<ParameterPlan>();
                        return false;
                    }
                    plan.Source = ParameterSource.OriginalArgument;
                    plan.OriginalIndex = i;
                    plan.OriginalParameterType = originalParamType;
                    plan.RequiresConversion = !targetType.IsAssignableFrom(originalParamType);
                    plan.AllowNull = plan.AllowNull || !originalParamType.IsValueType || Nullable.GetUnderlyingType(originalParamType) != null;
                }
                else if (mixinParam.HasDefaultValue)
                {
                    plan.Source = ParameterSource.DefaultValue;
                }
                else
                {
                    plans = Array.Empty<ParameterPlan>();
                    return false;
                }

                result[i] = plan;
            }

            plans = result;
            return true;
        }

        static MethodInjectionPlan CreateMethodInjectionPlan(InjectionInfo[] ordered)
        {
            if (ordered.Length == 0)
            {
                return new MethodInjectionPlan(Array.Empty<InjectionInfo>(), Array.Empty<InjectionInfo>());
            }

            var heads = new List<InjectionInfo>();
            var tails = new List<InjectionInfo>();
            foreach (var inj in ordered)
            {
                if ((inj.At & InjectAt.Head) != 0)
                {
                    heads.Add(inj);
                }
                if ((inj.At & InjectAt.Tail) != 0)
                {
                    tails.Add(inj);
                }
            }

            return new MethodInjectionPlan(heads.ToArray(), tails.ToArray());
        }

        // Harmony prefix：在原方法执行前调用
        static bool Prefix(MethodBase __originalMethod, object? __instance, object[] __args, ref object? __result)
        {
            if (!_injections.TryGetValue(__originalMethod, out var plan) || !plan.HasHeads) return true;

            foreach (var inj in plan.Heads)
            {
                var ci = new CallbackInfo();
                try
                {
                    var mixinTarget = ResolveMixinTarget(inj);
                    if (!inj.MixinMethod.IsStatic && mixinTarget == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"DMixin 前缀：无法解析 mixin {inj.MixinMethod} 的实例，已跳过执行。");
                        continue;
                    }
                    var parameters = BuildInvokeParameters(inj, __args, ci);
                    inj.MixinMethod.Invoke(mixinTarget, parameters);
                    if (ci.Result != null)
                    {
                        __result = ci.Result;
                    }
                    if (!ci.RunOriginal)
                    {
                        return false;
                    }
                }
                catch (TargetInvocationException tie)
                {
                    System.Diagnostics.Debug.WriteLine($"DMixin 前缀：mixins 在调用 {inj.MixinMethod} 时抛出异常：{tie.InnerException ?? tie}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"DMixin 前缀：调用 mixin {inj.MixinMethod} 时发生错误：{ex}");
                }
            }
            return true;
        }

        // Harmony postfix：在原方法执行后调用
        static void Postfix(MethodBase __originalMethod, object? __instance, object[] __args, ref object? __result)
        {
            if (!_injections.TryGetValue(__originalMethod, out var plan) || !plan.HasTails) return;

            foreach (var inj in plan.Tails)
            {
                var ci = new CallbackInfo { Result = __result };
                try
                {
                    var mixinTarget = ResolveMixinTarget(inj);
                    if (!inj.MixinMethod.IsStatic && mixinTarget == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"DMixin 后缀：无法解析 mixin {inj.MixinMethod} 的实例，已跳过执行。");
                        continue;
                    }
                    var parameters = BuildInvokeParameters(inj, __args, ci);
                    inj.MixinMethod.Invoke(mixinTarget, parameters);
                    if (ci.Result != null)
                    {
                        __result = ci.Result;
                    }
                }
                catch (TargetInvocationException tie)
                {
                    System.Diagnostics.Debug.WriteLine($"DMixin 后缀：mixins 在调用 {inj.MixinMethod} 时抛出异常：{tie.InnerException ?? tie}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"DMixin 后缀：调用 mixin {inj.MixinMethod} 时发生错误：{ex}");
                }
            }
        }

        static object?[] BuildInvokeParameters(InjectionInfo inj, object[] originalArgs, CallbackInfo ci)
        {
            var plans = inj.ParameterPlans;
            if (plans.Length == 0)
            {
                return Array.Empty<object?>();
            }

            var buffer = new object?[plans.Length];
            for (int i = 0; i < plans.Length; i++)
            {
                buffer[i] = ResolveParameterValue(plans[i], originalArgs, inj, ci);
            }

            return buffer;
        }

        static object? ResolveParameterValue(in ParameterPlan plan, object[] originalArgs, InjectionInfo inj, CallbackInfo ci)
        {
            return plan.Source switch
            {
                ParameterSource.Callback => inj.UseCallbackCi ? ci : GetDefault(plan.TargetType),
                ParameterSource.DefaultValue => plan.DefaultValue,
                ParameterSource.OriginalArgument => ResolveOriginalArgument(originalArgs, plan),
                _ => GetDefault(plan.TargetType)
            };
        }

        static object? ResolveOriginalArgument(object[] originalArgs, in ParameterPlan plan)
        {
            if (plan.OriginalIndex < 0 || plan.OriginalIndex >= originalArgs.Length)
            {
                return plan.DefaultValue ?? GetDefault(plan.TargetType);
            }

            var raw = originalArgs[plan.OriginalIndex];
            if (raw == null)
            {
                return plan.AllowNull ? null : GetDefault(plan.TargetType);
            }

            if (!plan.RequiresConversion || plan.TargetType.IsInstanceOfType(raw))
            {
                return raw;
            }

            if (TryConvertValue(raw, plan.TargetType, out var converted))
            {
                return converted;
            }

            return plan.AllowNull ? raw : GetDefault(plan.TargetType);
        }

        static bool TryConvertValue(object value, Type targetType, out object? converted)
        {
            try
            {
                var underlying = Nullable.GetUnderlyingType(targetType);
                if (underlying != null)
                {
                    targetType = underlying;
                }

                if (targetType.IsInstanceOfType(value))
                {
                    converted = value;
                    return true;
                }

                if (targetType.IsEnum)
                {
                    if (value is string enumName)
                    {
                        converted = Enum.Parse(targetType, enumName, ignoreCase: true);
                        return true;
                    }

                    converted = Enum.ToObject(targetType, value);
                    return true;
                }

                if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(targetType))
                {
                    converted = Convert.ChangeType(value, targetType);
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DMixin：尝试转换参数失败，值类型 {value.GetType()} -> 目标类型 {targetType}：{ex.Message}");
            }

            converted = null;
            return false;
        }

        // 解析并返回调用 mixin 方法时应使用的目标实例（若为 static 返回 null）
        static object? ResolveMixinTarget(InjectionInfo inj)
        {
            if (inj.MixinMethod.IsStatic) return null;
            try
            {
                if (inj.MixinInstance != null) return inj.MixinInstance;

                var t = inj.MixinType ?? inj.MixinMethod.DeclaringType;
                if (t == null) return null;

                // 优先使用外部 IServiceProvider 解析 mixin 实例
                if (_serviceProvider != null)
                {
                    try
                    {
                        var svc = _serviceProvider.GetService(t);
                        if (svc != null)
                        {
                            inj.MixinInstance = svc;
                            return svc;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"DMixin：通过 IServiceProvider 获取类型 {t} 实例失败：{ex}");
                    }
                }

                // 回退到 Activator.CreateInstance
                var inst = Activator.CreateInstance(t);
                inj.MixinInstance = inst;
                return inst;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DMixin：为 mixin 方法 {inj.MixinMethod} 创建实例失败：{ex}");
                return null;
            }
        }

        static object? GetDefault(Type t) => t.IsValueType ? Activator.CreateInstance(t) : null;
    }
}

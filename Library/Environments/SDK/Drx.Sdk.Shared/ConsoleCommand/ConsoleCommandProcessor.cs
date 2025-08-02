using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DRX.Framework;

namespace Drx.Sdk.Shared.ConsoleCommand
{
    /// <summary>
    /// 控制台命令处理器，支持注册命令及其处理逻辑，并在后台线程监听控制台输入。
    /// 支持主命令、子命令和分支命令的链式调用。
    /// </summary>
    public class ConsoleCommandProcessor
    {
        /// <summary>
        /// 命令名到命令处理器类型的映射缓存
        /// </summary>
        private static readonly ConcurrentDictionary<string, Type> CommandTypeCache = new();

        /// <summary>
        /// 命令类型到子命令名与方法的映射缓存
        /// </summary>
        private static readonly ConcurrentDictionary<Type, Dictionary<string, MethodInfo>> SubCommandCache = new();

        /// <summary>
        /// 命令类型到分支命令名与方法的映射缓存
        /// </summary>
        private static readonly ConcurrentDictionary<Type, Dictionary<string, MethodInfo>> BranchCommandCache = new();

        /// <summary>
        /// 静态构造函数，自动注册所有命令
        /// </summary>
        static ConsoleCommandProcessor()
        {
            RegisterAllCommands();
        }

        /// <summary>
        /// 扫描程序集，注册所有实现 ICommandHandler 的命令及其子命令和分支命令
        /// </summary>
        private static void RegisterAllCommands()
        {
            var handlerType = typeof(ICommandHandler);
            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
                })
                .Where(t => handlerType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in types)
            {
                var cmdAttr = type.GetCustomAttributes(typeof(CommandAttribute), false).FirstOrDefault() as CommandAttribute;
                if (cmdAttr == null || string.IsNullOrWhiteSpace(cmdAttr.Name))
                    continue;

                CommandTypeCache[cmdAttr.Name.ToLowerInvariant()] = type;

                // 子命令注册
                var subCmds = new Dictionary<string, MethodInfo>();
                foreach (var method in type.GetMethods())
                {
                    var subAttr = method.GetCustomAttributes(typeof(SubCommandAttribute), false).FirstOrDefault() as SubCommandAttribute;
                    if (subAttr != null && !string.IsNullOrWhiteSpace(subAttr.Name))
                    {
                        subCmds[subAttr.Name.ToLowerInvariant()] = method;
                    }
                }
                SubCommandCache[type] = subCmds;

                // 分支命令注册
                var branchCmds = new Dictionary<string, MethodInfo>();
                foreach (var method in type.GetMethods())
                {
                    var branchAttr = method.GetCustomAttributes(typeof(BranchCommandAttribute), false).FirstOrDefault() as BranchCommandAttribute;
                    if (branchAttr != null && !string.IsNullOrWhiteSpace(branchAttr.Name))
                    {
                        branchCmds[branchAttr.Name.ToLowerInvariant()] = method;
                    }
                }
                BranchCommandCache[type] = branchCmds;
                Logger.Debug($"已注册命令: {cmdAttr.Name} ({type.FullName}, 子命令: {string.Join(", ", subCmds.Keys)}, 分支命令: {string.Join(", ", branchCmds.Keys)})");
            }
        }

        /// <summary>
        /// 解析并执行控制台输入命令
        /// </summary>
        /// <param name="input">控制台输入字符串</param>
        /// <returns>执行结果异常（成功为null）</returns>
        public static Exception ExecuteCommand(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return new ArgumentException("命令不能为空");

            // 新格式：/command arg1 arg2 -subcommand arg1 arg2 -branchcommand arg1 arg2
            var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return new ArgumentException("命令不能为空");

            // 主命令必须以/开头
            var cmdToken = parts[0];
            if (!cmdToken.StartsWith("/"))
                return new ArgumentException("命令格式错误，主命令需以/开头");

            var cmdName = cmdToken.Substring(1).ToLowerInvariant();
            if (!CommandTypeCache.TryGetValue(cmdName, out var handlerType))
            {
                Logger.Error($"未知命令: {cmdName}");
                return new InvalidOperationException($"命令不存在: {cmdName}");
            }
            var handler = Activator.CreateInstance(handlerType) as ICommandHandler;
            if (handler == null)
                return new InvalidOperationException($"无法实例化命令处理器: {cmdName}");

            var subCmds = SubCommandCache.TryGetValue(handlerType, out var subDict) ? subDict : null;
            var branchCmds = BranchCommandCache.TryGetValue(handlerType, out var branchDict) ? branchDict : null;

            // 支持多个子命令和分支命令链式解析
            if (subCmds != null || branchCmds != null)
            {
                // 记录所有子命令和分支命令的索引
                var cmdIndices = new List<int>();
                for (int i = 1; i < parts.Length; i++)
                {
                    if (parts[i].StartsWith("-"))
                        cmdIndices.Add(i);
                }

                if (cmdIndices.Count > 0)
                {
                    // 主命令参数为第一个-之前的所有参数
                    var mainArgs = cmdIndices[0] > 1
                        ? parts.Skip(1).Take(cmdIndices[0] - 1).ToArray()
                        : Array.Empty<string>();

                    // 记录已执行的命令栈，用于分支命令的父子关系验证
                    var executedCmdStack = new Stack<string>();

                    // 依次执行每个子命令或分支命令
                    for (int i = 0; i < cmdIndices.Count; i++)
                    {
                        int idx = cmdIndices[i];
                        var cmdTokenInner = parts[idx];
                        var cmdNameInner = cmdTokenInner.Substring(1).ToLowerInvariant();
                        
                        // 尝试查找子命令
                        if (subCmds != null && subCmds.TryGetValue(cmdNameInner, out var subMethod))
                        {
                            // 校验子命令层级
                            var subAttr = subMethod.GetCustomAttributes(typeof(SubCommandAttribute), false).FirstOrDefault() as SubCommandAttribute;
                            int expectedLevel = i + 1;
                            int actualLevel = subAttr?.Level ?? 1;
                            if (actualLevel != expectedLevel)
                            {
                                Logger.Error($"子命令“{cmdNameInner}”调用层级错误：声明为{actualLevel}级，实际为{expectedLevel}级。");
                                return new InvalidOperationException($"子命令“{cmdNameInner}”只能作为第{actualLevel}级子命令被调用，当前为第{expectedLevel}级。");
                            }

                            // 子命令参数为本子命令到下一个命令（或结尾）之间的参数
                            int nextIdx = (i + 1 < cmdIndices.Count) ? cmdIndices[i + 1] : parts.Length;
                            var subArgs = parts.Skip(idx + 1).Take(nextIdx - idx - 1).ToArray();

                            try
                            {
                                var parameters = subMethod.GetParameters();
                                if (parameters.Length == 2 &&
                                    parameters[0].ParameterType == typeof(string[]) &&
                                    parameters[1].ParameterType == typeof(string[]))
                                {
                                    // 新签名：SubCommand(string[] mainArgs, string[] subArgs)
                                    var ex = subMethod.Invoke(handler, new object[] { mainArgs, subArgs }) as Exception;
                                    if (ex != null) return ex;
                                }
                                else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string[]))
                                {
                                    // 兼容旧签名：SubCommand(string[] subArgs)
                                    var ex = subMethod.Invoke(handler, new object[] { subArgs }) as Exception;
                                    if (ex != null) return ex;
                                }
                                else
                                {
                                    return new InvalidOperationException($"子命令方法签名不正确: {cmdNameInner}");
                                }

                                // 将子命令添加到已执行栈
                                executedCmdStack.Push(cmdNameInner);
                            }
                            catch (Exception ex)
                            {
                                return ex.InnerException ?? ex;
                            }
                        }
                        // 尝试查找分支命令
                        else if (branchCmds != null && branchCmds.TryGetValue(cmdNameInner, out var branchMethod))
                        {
                            // 校验分支命令的父子关系
                            var branchAttr = branchMethod.GetCustomAttributes(typeof(BranchCommandAttribute), false).FirstOrDefault() as BranchCommandAttribute;
                            if (branchAttr != null && !string.IsNullOrWhiteSpace(branchAttr.ParentName))
                            {
                                var parentName = branchAttr.ParentName.ToLowerInvariant();
                                if (!executedCmdStack.Contains(parentName))
                                {
                                    Logger.Error($"分支命令“{cmdNameInner}”的父命令“{parentName}”未执行。");
                                    return new InvalidOperationException($"分支命令“{cmdNameInner}”必须在父命令“{parentName}”之后执行。");
                                }
                            }

                            // 分支命令参数为本分支命令到下一个命令（或结尾）之间的参数
                            int nextIdx = (i + 1 < cmdIndices.Count) ? cmdIndices[i + 1] : parts.Length;
                            var branchArgs = parts.Skip(idx + 1).Take(nextIdx - idx - 1).ToArray();

                            try
                            {
                                var parameters = branchMethod.GetParameters();
                                if (parameters.Length == 2 &&
                                    parameters[0].ParameterType == typeof(string[]) &&
                                    parameters[1].ParameterType == typeof(string[]))
                                {
                                    // 新签名：BranchCommand(string[] mainArgs, string[] branchArgs)
                                    var ex = branchMethod.Invoke(handler, new object[] { mainArgs, branchArgs }) as Exception;
                                    if (ex != null) return ex;
                                }
                                else if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string[]))
                                {
                                    // 兼容旧签名：BranchCommand(string[] branchArgs)
                                    var ex = branchMethod.Invoke(handler, new object[] { branchArgs }) as Exception;
                                    if (ex != null) return ex;
                                }
                                else
                                {
                                    return new InvalidOperationException($"分支命令方法签名不正确: {cmdNameInner}");
                                }

                                // 将分支命令添加到已执行栈
                                executedCmdStack.Push(cmdNameInner);
                            }
                            catch (Exception ex)
                            {
                                return ex.InnerException ?? ex;
                            }
                        }
                        else
                        {
                            return new InvalidOperationException($"未知命令: {cmdNameInner}");
                        }
                    }
                    return null; // 所有命令执行成功
                }
            }

            // 没有子命令，或未找到
            var mainOnlyArgs = parts.Skip(1).ToArray();
            try
            {
                return handler.Execute(mainOnlyArgs);
            }
            catch (Exception ex)
            {
                return ex.InnerException ?? ex;
            }
        }

        /// <summary>
        /// 获取所有命令及其描述、子命令及子命令描述
        /// </summary>
        public static List<CommandInfo> GetAllCommands()
        {
            var result = new List<CommandInfo>();
            foreach (var kv in CommandTypeCache)
            {
                var type = kv.Value;
                var cmdAttr = type.GetCustomAttributes(typeof(CommandAttribute), false).FirstOrDefault() as CommandAttribute;
                var cmdInfo = new CommandInfo
                {
                    Name = kv.Key,
                    Description = cmdAttr?.Description ?? "",
                    ArgDescriptions = cmdAttr?.ArgDescriptions ?? Array.Empty<string>(),
                    SubCommands = new List<SubCommandInfo>(),
                    BranchCommands = new List<BranchCommandInfo>()
                };
                if (SubCommandCache.TryGetValue(type, out var subDict))
                {
                    foreach (var subKv in subDict)
                    {
                        var method = subKv.Value;
                        var subAttr = method.GetCustomAttributes(typeof(SubCommandAttribute), false).FirstOrDefault() as SubCommandAttribute;
                        cmdInfo.SubCommands.Add(new SubCommandInfo
                        {
                            Name = subKv.Key,
                            Description = subAttr?.Description ?? "",
                            ArgDescriptions = subAttr?.ArgDescriptions ?? Array.Empty<string>(),
                            Level = subAttr?.Level ?? 1
                        });
                    }
                }
                if (BranchCommandCache.TryGetValue(type, out var branchDict))
                {
                    foreach (var branchKv in branchDict)
                    {
                        var method = branchKv.Value;
                        var branchAttr = method.GetCustomAttributes(typeof(BranchCommandAttribute), false).FirstOrDefault() as BranchCommandAttribute;
                        cmdInfo.BranchCommands.Add(new BranchCommandInfo
                        {
                            Name = branchKv.Key,
                            Description = branchAttr?.Description ?? "",
                            ArgDescriptions = branchAttr?.ArgDescriptions ?? Array.Empty<string>(),
                            ParentName = branchAttr?.ParentName ?? ""
                        });
                    }
                }
                result.Add(cmdInfo);
            }
            return result;
        }

        public class CommandInfo
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string[] ArgDescriptions { get; set; }
            public List<SubCommandInfo> SubCommands { get; set; }
            public List<BranchCommandInfo> BranchCommands { get; set; }
        }

        public class SubCommandInfo
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string[] ArgDescriptions { get; set; }
            public int Level { get; set; }
        }

        public class BranchCommandInfo
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string[] ArgDescriptions { get; set; }
            public string ParentName { get; set; }
        }
    }
}
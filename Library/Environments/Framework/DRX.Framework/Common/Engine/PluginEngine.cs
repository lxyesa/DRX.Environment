using DRX.Framework.Common.Enums;
using DRX.Framework.Common.Interface;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace DRX.Framework.Common.Engine
{
    public class PluginEngine : IEngine
    {
        private static readonly string PluginDirectory = Path.Combine(AppContext.BaseDirectory, "plugins");
        private static readonly List<Plugin> Plugins = new();

        public EngineType Type => EngineType.Other;

        public static void LoadPlugins(IEngine loader)
        {
            if (loader.Type == EngineType.Client)
            {
                Logger.Warring("当前引擎类型为 Client，不允许加载插件。");
                return;
            }

            if (!Directory.Exists(PluginDirectory))
            {
                Directory.CreateDirectory(PluginDirectory);
            }

            var pluginFiles = Directory.GetFiles(PluginDirectory, "*.dll");

            // 获取加载顺序
            var loadOrder = GetLoadOrder(pluginFiles);

            foreach (var dll in loadOrder)
            {
                try
                {
                    var plugin = new Plugin(dll);
                    plugin.Load(loader);
                    Plugins.Add(plugin);
                    Logger.Log("PluginEngine", $"加载插件: {plugin.Name}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"加载插件失败: {dll}, 错误: {ex.Message}");
                }
            }
        }

        public static void UnloadPlugins()
        {
            foreach (var plugin in Plugins)
            {
                try
                {
                    plugin.Unload();
                    plugin.Dispose();
                    Logger.Log("PluginEngine", $"卸载插件: {plugin.Name}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"卸载插件失败: {plugin.Name}, 错误: {ex.Message}");
                }
            }
            Plugins.Clear();
        }

        /// <summary>
        /// 重新加载所有插件。在安全的时候卸载现有插件并重新加载。
        /// </summary>
        /// <param name="loader">当前的引擎实例。</param>
        public static void ReloadPlugins(IEngine loader)
        {
            if (loader.Type == EngineType.Client)
            {
                Logger.Warring("当前引擎类型为 Client，不允许重新加载插件。");
                return;
            }

            try
            {
                Logger.Log("PluginEngine", "开始重新加载插件。");
                UnloadPlugins();
                LoadPlugins(loader);
                Logger.Log("PluginEngine", "插件重新加载完成。");
            }
            catch (Exception ex)
            {
                Logger.Error($"重新加载插件时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 按指定插件名称卸载插件。
        /// </summary>
        /// <param name="pluginName">插件的名称。</param>
        public static bool UnloadPlugin(string pluginName)
        {
            var plugin = Plugins.FirstOrDefault(p => p.Name != null && p.Name.Equals(pluginName, StringComparison.OrdinalIgnoreCase));
            if (plugin == null)
            {
                Logger.Warring($"未找到插件: {pluginName}");
                return false;
            }

            try
            {
                plugin.Unload();
                plugin.Dispose();
                Plugins.Remove(plugin);
                Logger.Log("PluginEngine", $"卸载插件: {plugin.Name}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"卸载插件失败: {plugin.Name}, 错误: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 按指定插件名称加载插件。
        /// </summary>
        /// <param name="pluginName">插件的名称（不含扩展名）。</param>
        /// <param name="loader">当前的引擎实例。</param>
        public static bool LoadPlugin(string pluginName, IEngine loader)
        {
            var dllPath = Path.Combine(PluginDirectory, $"{pluginName}.dll");
            if (!File.Exists(dllPath))
            {
                Logger.Warring($"插件文件未找到: {dllPath}");
                return false;
            }

            try
            {
                var plugin = new Plugin(dllPath);
                plugin.Load(loader);
                Plugins.Add(plugin);
                Logger.Log("PluginEngine", $"加载插件: {plugin.Name}");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"加载插件失败: {dllPath}, 错误: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 按指定插件名称重新加载插件。
        /// </summary>
        /// <param name="pluginName">插件的名称。</param>
        /// <param name="loader">当前的引擎实例。</param>
        public static bool ReloadPlugin(string pluginName, IEngine loader)
        {
            if (!UnloadPlugin(pluginName))
            {
                Logger.Warring($"无法卸载插件: {pluginName}, 因此无法重新加载。");
                return false;
            }

            return LoadPlugin(pluginName, loader);
        }

        /// <summary>
        /// 获取已加载插件的列表。
        /// </summary>
        public static List<IPlugin> GetLoadedPlugins()
        {
            return Plugins.Select(p => p.Instance).ToList();
        }

        /// <summary>
        /// 根据插件的依赖关系计算加载顺序。
        /// 被依赖次数多的插件优先加载，并确保依赖关系正确。
        /// </summary>
        /// <param name="pluginFiles">插件文件路径数组。</param>
        /// <returns>排序后的插件文件路径列表。</returns>
        private static List<string> GetLoadOrder(string[] pluginFiles)
        {
            // 提取插件名称与路径的映射
            var pluginNameToPath = pluginFiles.ToDictionary(
                path => Path.GetFileNameWithoutExtension(path),
                path => path,
                StringComparer.OrdinalIgnoreCase
            );

            // 构建依赖图
            var dependencyGraph = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var pluginName in pluginNameToPath.Keys)
            {
                dependencyGraph[pluginName] = [];
            }

            // 解析每个插件的依赖关系
            foreach (var path in pluginFiles)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(path);
                    var pluginType = typeof(IPlugin);
                    var pluginClass = assembly.GetTypes().FirstOrDefault(t => pluginType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);
                    if (pluginClass == null)
                        continue;

                    var pluginInstance = (IPlugin)Activator.CreateInstance(pluginClass)!;
                    foreach (var dependency in pluginInstance.Dependencies)
                    {
                        if (pluginNameToPath.ContainsKey(dependency))
                        {
                            dependencyGraph[pluginInstance.Name].Add(dependency);
                        }
                        else
                        {
                            // 如果依赖的插件不存在，记录警告
                            Logger.Warring($"插件 {pluginInstance.Name} 依赖的插件 {dependency} 未找到。");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"解析插件依赖失败: {path}, 错误: {ex.Message}");
                }
            }

            // 执行拓扑排序
            var sortedList = TopologicalSort(dependencyGraph);
            if (sortedList == null)
            {
                Logger.Warring("检测到循环依赖，无法确定插件加载顺序。");
                throw new InvalidOperationException("循环依赖存在于插件中。");
            }

            // 根据排序结果获取插件路径
            var sortedPlugins = sortedList.Select(name => pluginNameToPath[name]).ToList();

            return sortedPlugins;
        }

        /// <summary>
        /// 执行拓扑排序以确定插件加载顺序。
        /// </summary>
        /// <param name="dependencyGraph">插件依赖图。</param>
        /// <returns>排序后的插件名称列表，若检测到循环依赖则返回 null。</returns>
        private static List<string>? TopologicalSort(Dictionary<string, List<string>> dependencyGraph)
        {
            var sorted = new List<string>();
            var visited = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            return dependencyGraph.Keys.Where(node => !visited.ContainsKey(node)).Any(node => !Dfs(node, dependencyGraph, visited, sorted)) ? null : sorted;
        }

        /// <summary>
        /// 深度优先搜索辅助方法，用于拓扑排序。
        /// </summary>
        private static bool Dfs(string node, Dictionary<string, List<string>> graph, Dictionary<string, bool> visited, List<string> sorted)
        {
            visited[node] = true; // 标记为正在访问

            foreach (var neighbor in graph[node])
            {
                if (!visited.TryGetValue(neighbor, out var value))
                {
                    if (!Dfs(neighbor, graph, visited, sorted))
                        return false;
                }
                else if (value)
                {
                    // 检测到循环依赖
                    return false;
                }
            }

            visited[node] = false; // 标记为访问完成
            sorted.Add(node); // 添加到排序列表
            return true;
        }
    }

    public class Plugin : IDisposable
    {
        public IPlugin Instance { get; private set; }
        public string? Name { get; private set; }
        private readonly AssemblyLoadContext _loadContext;

        public Plugin(string path)
        {
            _loadContext = new PluginLoadContext(path);
            var assemblyName = new AssemblyName(Path.GetFileNameWithoutExtension(path));
            var assembly = _loadContext.LoadFromAssemblyName(assemblyName);
            var pluginType = typeof(IPlugin);

            foreach (var type in assembly.GetTypes())
            {
                if (!pluginType.IsAssignableFrom(type) || type.IsInterface || type.IsAbstract) continue;
                Instance = (IPlugin)Activator.CreateInstance(type)!;
                Name = Instance.Name;
                break;
            }

            if (Instance == null)
                throw new Exception($"在 {path} 中未找到实现 IPlugin 的类。");
        }

        public void Load(IEngine loader) => Instance.Load(loader);
        public void Unload() => Instance.Unload();

        public void Dispose()
        {
            _loadContext.Unload();
        }
    }

    // 自定义 AssemblyLoadContext
    internal class PluginLoadContext(string pluginPath) : AssemblyLoadContext(isCollectible: true)
    {
        private readonly AssemblyDependencyResolver _resolver = new(pluginPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            return assemblyPath != null ? LoadFromAssemblyPath(assemblyPath) : null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return libraryPath != null ? LoadUnmanagedDllFromPath(libraryPath) : IntPtr.Zero;
        }
    }
}

using System.Reflection;
using Drx.Sdk.Shared.JavaScript.Abstractions;

namespace DrxPaperclip.Hosting;

/// <summary>
/// 插件动态加载器。将 <c>--plugin</c> 指定的 DLL 加载为 <see cref="IJavaScriptPlugin"/> 实例。
/// 仅加载用户显式指定的 DLL，不自动扫描目录。
/// </summary>
public static class PluginLoader
{
    /// <summary>
    /// 加载指定的 DLL 文件列表，扫描其中实现 <see cref="IJavaScriptPlugin"/> 的非抽象公开类型并实例化返回。
    /// </summary>
    /// <param name="dllPaths">DLL 文件路径列表（绝对或相对路径均可）。</param>
    /// <returns>所有成功实例化的 <see cref="IJavaScriptPlugin"/> 列表。</returns>
    /// <exception cref="FileNotFoundException">DLL 路径不存在时抛出。</exception>
    /// <exception cref="InvalidOperationException">程序集加载失败或插件实例化失败时抛出。</exception>
    public static List<IJavaScriptPlugin> Load(IReadOnlyList<string> dllPaths)
    {
        var plugins = new List<IJavaScriptPlugin>();

        foreach (var dllPath in dllPaths)
        {
            var fullPath = Path.GetFullPath(dllPath);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    $"Plugin DLL not found: '{fullPath}'", fullPath);
            }

            Assembly assembly;
            try
            {
                assembly = Assembly.LoadFrom(fullPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to load plugin assembly '{fullPath}': {ex.Message}", ex);
            }

            var pluginTypes = assembly.GetExportedTypes()
                .Where(t => typeof(IJavaScriptPlugin).IsAssignableFrom(t)
                            && t.IsClass
                            && !t.IsAbstract);

            foreach (var pluginType in pluginTypes)
            {
                try
                {
                    var instance = (IJavaScriptPlugin)Activator.CreateInstance(pluginType)!;
                    plugins.Add(instance);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to instantiate plugin type '{pluginType.FullName}' from '{fullPath}': {ex.Message}", ex);
                }
            }
        }

        return plugins;
    }
}

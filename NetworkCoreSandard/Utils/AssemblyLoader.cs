// AssemblyLoader.cs
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

namespace NetworkCoreStandard.Utils
{
    public static class AssemblyLoader
    {
        private static Dictionary<string, Assembly> _loadedAssemblies = new Dictionary<string, Assembly>();

        static AssemblyLoader()
        {
            // AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
        }

        public static void LoadEmbeddedAssemblies()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            IEnumerable<string> resources = assembly.GetManifestResourceNames()
                .Where(x => x.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));

            foreach (string? resource in resources)
            {
                try
                {
                    using (Stream stream = assembly.GetManifestResourceStream(resource))
                    {
                        if (stream == null) continue;

                        using (MemoryStream memStream = new MemoryStream())
                        {
                            stream.CopyTo(memStream);
                            byte[] assemblyData = memStream.ToArray();
                            Assembly loadedAssembly = Assembly.Load(assemblyData);
                            string assemblyName = loadedAssembly.GetName().Name;
                            
                            if (!_loadedAssemblies.ContainsKey(assemblyName))
                            {
                                _loadedAssemblies.Add(assemblyName, loadedAssembly);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 可以添加日志记录
                    System.Diagnostics.Debug.WriteLine($"加载程序集失败: {resource}, 错误: {ex.Message}");
                }
            }
        }

        private static Assembly? ResolveAssembly(object sender, ResolveEventArgs args)
        {
            string assemblyName = new AssemblyName(args.Name).Name;
            return _loadedAssemblies.TryGetValue(assemblyName, out Assembly? assembly) ? assembly : null;
        }
    }
}
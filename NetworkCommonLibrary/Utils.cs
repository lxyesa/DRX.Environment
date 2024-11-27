
using System.Reflection;

public class Utils
{
    static Utils()
    {
        AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
    }

    private static Assembly? CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
    {
        var assemblyName = new AssemblyName(args.Name).Name;
        string resourceName = $"{typeof(Utils).Namespace}.Libs.{assemblyName}.dll";
        var assembly = typeof(Utils).Assembly;

        // 从资源中加载程序集
        using (Stream stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream == null) return null;

            using (MemoryStream ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return Assembly.Load(ms.ToArray());
            }
        }
    }
}
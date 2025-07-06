using System.IO.Compression;
using System.Reflection;

namespace Drx.Sdk.Resource.Utility;

public static class ResourceManager
{
    /// <summary>
    /// 将嵌入的资源文件解压到指定目录
    /// </summary>
    /// <param name="targetPath">解压目标路径</param>
    /// <param name="resourceName">资源名称（不包含命名空间前缀）</param>
    /// <param name="assembly">包含资源的程序集，默认为调用者所在程序集</param>
    /// <returns>返回是否成功</returns>
    public static bool Unzip(string targetPath, string resourceName, Assembly? assembly = null)
    {
        try
        {
            // 如果没有指定程序集，则使用调用者的程序集
            assembly ??= Assembly.GetCallingAssembly();
            
            var fullResourceName = $"{assembly?.GetName().Name}.{resourceName}";
            Console.WriteLine($"Unzipping {targetPath} to {fullResourceName}");

            // 确保目标目录存在
            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
            }
            
            // 获取资源流
            using var resourceStream = assembly?.GetManifestResourceStream(fullResourceName);
            if (resourceStream == null)
            {
                Console.WriteLine("Error: 资源不存在");
                return false; // 资源不存在
            }
            
            // 解压缩资源
            using var archive = new ZipArchive(resourceStream, ZipArchiveMode.Read);
            archive.ExtractToDirectory(targetPath, true); // 如果存在则覆盖
            
            return true;
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
            return false; // 解压过程中发生异常
        }
    }
    
    /// <summary>
    /// 获取嵌入资源列表
    /// </summary>
    /// <param name="assembly">程序集，默认为调用者所在程序集</param>
    /// <returns>资源��称列表</returns>
    public static string[] GetEmbeddedResourceNames(Assembly? assembly = null)
    {
        assembly ??= Assembly.GetCallingAssembly();
        return assembly.GetManifestResourceNames();
    }
}
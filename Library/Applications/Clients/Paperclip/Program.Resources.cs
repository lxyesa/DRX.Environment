using System.Reflection;

namespace DrxPaperclip;

public partial class Program
{
    private static IReadOnlyList<EmbeddedModelResource> DiscoverEmbeddedModelResources()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resources = assembly
            .GetManifestResourceNames()
            .Where(name => name.StartsWith(ModelResourcePrefix, StringComparison.Ordinal))
            .Select(name => new EmbeddedModelResource(
                name,
                name.Substring(ModelResourcePrefix.Length)
                    .Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar)))
            .OrderBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return resources;
    }

}

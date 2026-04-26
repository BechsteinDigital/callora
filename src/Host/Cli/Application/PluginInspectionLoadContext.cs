using System.Reflection;
using System.Runtime.Loader;

namespace Callora.Host.Cli.Application;

internal sealed class PluginInspectionLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _dependencyResolver;

    public PluginInspectionLoadContext(string assemblyPath)
        : base(isCollectible: true)
    {
        _dependencyResolver = new AssemblyDependencyResolver(assemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var resolvedPath = _dependencyResolver.ResolveAssemblyToPath(assemblyName);
        if (resolvedPath is null)
            return null;

        return LoadFromAssemblyPath(resolvedPath);
    }
}

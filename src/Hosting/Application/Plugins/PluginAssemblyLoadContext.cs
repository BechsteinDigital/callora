using System.Reflection;
using System.Runtime.Loader;

namespace Callora.Hosting.Application.Plugins;

internal sealed class PluginAssemblyLoadContext(string pluginAssemblyPath) : AssemblyLoadContext(
    name: $"CalloraPlugin:{Path.GetFileNameWithoutExtension(pluginAssemblyPath)}",
    isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(pluginAssemblyPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is { } name &&
            (name.Equals("Callora", StringComparison.Ordinal) ||
             name.StartsWith("Callora.", StringComparison.Ordinal)))
        {
            return null;
        }

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath is null ? null : LoadFromAssemblyPath(assemblyPath);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return libraryPath is null ? nint.Zero : LoadUnmanagedDllFromPath(libraryPath);
    }
}

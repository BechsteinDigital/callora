using System.Reflection;
using System.Runtime.Loader;

namespace Callora.Hosting.Application.Plugins;

internal sealed class PluginAssemblyLoadContext(
    string pluginAssemblyPath,
    SharedContractAssemblyRegistry? sharedContracts = null) : AssemblyLoadContext(
    name: $"CalloraPlugin:{Path.GetFileNameWithoutExtension(pluginAssemblyPath)}",
    isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(pluginAssemblyPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Callora.*-Verträge müssen aus dem Default-Kontext kommen, damit Host und Plugin
        // dieselben Contract-Typen teilen. CalloraVoipSdk (ohne Punkt) bleibt plugin-lokal.
        if (assemblyName.Name is { } name &&
            (name.Equals("Callora", StringComparison.Ordinal) ||
             name.StartsWith("Callora.", StringComparison.Ordinal)))
        {
            return null;
        }

        // Von Plugins mitgebrachte Contract-Assemblies teilen ihre Typidentität
        // über die Shared-Registry (PLAT-256) statt pro Plugin geladen zu werden.
        var sharedContract = sharedContracts?.TryResolve(assemblyName);
        if (sharedContract is not null)
        {
            return sharedContract;
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

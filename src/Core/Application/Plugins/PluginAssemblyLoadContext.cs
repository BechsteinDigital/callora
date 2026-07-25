using System.Reflection;
using System.Runtime.Loader;

namespace Callora.Core.Application.Plugins;

internal sealed class PluginAssemblyLoadContext(
    string pluginAssemblyPath,
    SharedContractAssemblyRegistry? sharedContracts = null) : AssemblyLoadContext(
    name: $"CalloraPlugin:{Path.GetFileNameWithoutExtension(pluginAssemblyPath)}",
    isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(pluginAssemblyPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is not { } name)
        {
            return null;
        }

        // Callora.*-Verträge müssen aus dem Default-Kontext kommen, damit Host und Plugin
        // dieselben Contract-Typen teilen. CalloraVoipSdk (ohne Punkt) bleibt plugin-lokal.
        if (name.Equals("Callora", StringComparison.Ordinal) ||
            name.StartsWith("Callora.", StringComparison.Ordinal))
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

        // Framework-/Laufzeit-Assemblies, die der Host selbst mitbringt (EF Core, Npgsql,
        // Microsoft.Extensions.*, System.*, …), müssen auf die bereits geladene Host-Kopie
        // auflösen. Sonst bekommt ein Plugin-Typ, der von einem Host-Framework-Basistyp
        // ableitet (z. B. ein DbContext), eine doppelte Typidentität und verletzt Host-
        // Generic-Constraints (IPluginDbContextFactory&lt;TContext&gt; where TContext : DbContext).
        // Nur Assemblies, die der Host nicht auflösen kann, fallen auf die plugin-lokale
        // Auflösung zurück — echte plugin-private Abhängigkeiten (CalloraVoipSdk, Concentus, …)
        // landen weiterhin in diesem collectible Kontext und bleiben entladbar.
        var hostProvided = TryResolveFromDefault(assemblyName);
        if (hostProvided is not null)
        {
            return hostProvided;
        }

        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return assemblyPath is null ? null : LoadFromAssemblyPath(assemblyPath);
    }

    private static Assembly? TryResolveFromDefault(AssemblyName assemblyName)
    {
        try
        {
            return Default.LoadFromAssemblyName(assemblyName);
        }
        catch (Exception exception)
            when (exception is FileNotFoundException or FileLoadException or BadImageFormatException)
        {
            // Nicht host-bereitgestellt (oder aus dem Default-Kontext nicht ladbar) —
            // der Aufrufer weicht auf die plugin-lokale Auflösung aus.
            return null;
        }
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return libraryPath is null ? nint.Zero : LoadUnmanagedDllFromPath(libraryPath);
    }
}

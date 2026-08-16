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
        if (assemblyName.Name is null)
        {
            return null;
        }

        // Hier stand bis 08/2026 ein Frühausstieg: Jeder Name, der "Callora" ist oder mit
        // "Callora." beginnt, bekam sofort null und kam damit aus dem Default-Kontext.
        // Als Aussage über PLATTFORM-Assemblies war das richtig; als Aussage über den
        // NAMENSRAUM war es zu breit. Interne Plugins tragen dasselbe Präfix (ADR-025),
        // und eine Vertrags-Assembly, die so ein Plugin mitbringt, stellt kein Host bereit:
        // Der Frühausstieg schickte sie in einen Kontext, der sie nicht hat, und der erste
        // Zugriff auf einen ihrer Typen endete in einer FileNotFoundException.
        //
        // Der Fallback darunter beantwortet dieselbe Frage genauer, und er stand schon
        // immer da — nur für Callora-Namen unerreichbar: Erst die geteilten Verträge,
        // dann was der Host tatsächlich stellt, erst dann plugin-lokal. Callora.Core
        // löst weiterhin auf die Host-Kopie auf, weil der Host sie referenziert; eine
        // plugin-eigene Callora.Plugin.X.Abstractions landet plugin-lokal, weil niemand
        // sonst sie hat. Beides ohne Namensliste, die gepflegt werden müsste.

        // Von Plugins mitgebrachte Contract-Assemblies teilen ihre Typidentität
        // über die Shared-Registry (PLAT-256) statt pro Plugin geladen zu werden.
        // Die Registry steht VOR dem Default-Kontext, weil nur sie die
        // Major-Version-Prüfung trägt; dass ein Plugin darüber keine host-gestellte
        // Assembly unterschieben kann, stellt sie selbst sicher (RegisterContractAssembly
        // nimmt host-gestellte Namen nur auf, ohne sie zu laden).
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

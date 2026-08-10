using Xunit;

namespace Callora.Core.Tests.Hosting;

/// <summary>
/// Führt die Tests, die einen echten Plugin-Ladekontext entladen, einzeln aus.
/// </summary>
/// <remarks>
/// <para>
/// Die Prüfung, ob ein <c>AssemblyLoadContext</c> wirklich verschwunden ist
/// (<c>AssemblyLoadContextUnload.WaitForCollection</c>), ruft <c>GC.Collect()</c> — und der
/// wirkt PROZESSWEIT. Läuft daneben ein zweiter Test, der gerade dasselbe Plugin geladen hat,
/// hält dessen Rahmen eine Referenz, die Sammlung schlägt fehl, und der erste Test meldet
/// „still pinned after unload" für ein Plugin, mit dem nichts verkehrt ist.
/// </para>
/// <para>
/// Serialisieren statt die Prüfung weicher machen: Die Prüfung ist das Einzige, was einen
/// echten Ladekontext-Leak überhaupt sichtbar macht. Sie zu lockern, weil der Testrunner
/// parallelisiert, hieße die Aussage aufzugeben, um das Werkzeug zu schonen — dieselbe
/// Überlegung wie bei <c>SurfaceRenderingCollection</c>.
/// </para>
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PluginLoadContextCollection
{
    public const string Name = "plugin-load-context";
}

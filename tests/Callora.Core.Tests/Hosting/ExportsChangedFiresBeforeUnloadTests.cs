using Callora.Core.Application.Options;
using Callora.Core.Application.Plugins;
using Callora.Core.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Callora.Core.Tests.Hosting;

/// <summary>
/// Wer aus den Exports eines Plugins etwas ableitet und festhält, erfährt vom Zurückziehen
/// rechtzeitig — nämlich bevor der Ladekontext entladen wird.
/// </summary>
/// <remarks>
/// <para>
/// Der Befund: Die Routing-Tabelle (<c>PluginApiEndpointDataSource</c>) baut aus den
/// <c>IApiController</c>-Exports Endpunkte und hält dabei Delegaten auf Plugin-Methoden.
/// Aufgeräumt hat sie erst beim Lifecycle-Ereignis — das nach der Deaktivierung veröffentlicht
/// wird, also nach der Prüfung, ob der Ladekontext wirklich verschwunden ist. Die Prüfung fand
/// die eigenen Endpunkte als Halter vor und meldete <c>UnloadFailed</c>: Jedes Plugin mit
/// API-Route verlangte beim Deaktivieren einen Host-Neustart, obwohl nichts kaputt war.
/// </para>
/// <para>
/// Getestet wird gegen ein echtes Plugin in einem echten
/// <c>PluginAssemblyLoadContext</c> — die Reihenfolge ist der ganze Punkt, und die zeigt sich
/// nur im tatsächlichen Ablauf.
/// </para>
/// </remarks>
[Collection(PluginLoadContextCollection.Name)]
public sealed class ExportsChangedFiresBeforeUnloadTests
{
    [Fact]
    public async Task Deactivation_ReportsExportsChanged_WhileThePluginIsStillLoaded()
    {
        var assemblyPath = TestPluginAssemblies.Exporting();
        Assert.True(File.Exists(assemblyPath), $"Test plugin was not built at {assemblyPath}.");

        await using var host = new RuntimePluginHost(
            new ServiceCollection().BuildServiceProvider(),
            new CalloraHostingOptions(),
            NullLogger<RuntimePluginHost>.Instance);

        var install = await host.InstallAsync(
            assemblyPath,
            "Callora.TestPlugin.Exporting.ExportingTestPlugin");
        Assert.True(install.IsSuccess, install.Message);
        var pluginId = install.Plugin!.PluginId;
        Assert.True((await host.ActivateAsync(pluginId)).IsSuccess);

        var notifications = 0;
        host.ExportsChanged += () => notifications++;

        await host.DeactivateAsync(pluginId);

        // Geprüft wird die MELDUNG, nicht der Erfolg der Deaktivierung: Ob der Ladekontext
        // im selben Prozess tatsächlich eingesammelt wird, ist im Testhost zeitabhängig und
        // unzuverlässig — dieselbe Einschränkung, die RuntimePluginHostActivationTests
        // ausdrücklich festhält. Was dieser Fix zusichert, ist nicht „der Unload gelingt
        // jetzt immer", sondern „wer aufräumen muss, erfährt es rechtzeitig". Genau einmal,
        // nicht null: ohne die Meldung gibt es die Gelegenheit gar nicht.
        Assert.Equal(1, notifications);
    }

    [Fact]
    public async Task WhenTheNotificationArrives_TheExportsAreAlreadyGone()
    {
        // Die Reihenfolge innerhalb der Deaktivierung ist die eigentliche Zusage: Wer beim
        // Aufruf nachsieht, findet die Exports bereits zurückgezogen vor — und kann seine
        // eigene Ableitung daraufhin gefahrlos neu bauen, statt auf einen halb abgeräumten
        // Zustand zu treffen.
        var assemblyPath = TestPluginAssemblies.Exporting();
        await using var host = new RuntimePluginHost(
            new ServiceCollection().BuildServiceProvider(),
            new CalloraHostingOptions(),
            NullLogger<RuntimePluginHost>.Instance);

        var install = await host.InstallAsync(
            assemblyPath,
            "Callora.TestPlugin.Exporting.ExportingTestPlugin");
        var pluginId = install.Plugin!.PluginId;
        await host.ActivateAsync(pluginId);

        var exportsWhenNotified = -1;
        host.ExportsChanged += () =>
            exportsWhenNotified = host.GetExports<Callora.Core.Application.Persistence.Contracts.IWorkspaceDataPurgeContributor>().Count;

        await host.DeactivateAsync(pluginId);

        Assert.Equal(0, exportsWhenNotified);
    }
}

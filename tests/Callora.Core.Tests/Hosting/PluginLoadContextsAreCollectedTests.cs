using Callora.Core.Application.Options;
using Callora.Core.Application.Persistence.Contracts;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Policies;
using Callora.Core.Infrastructure.Persistence;
using Callora.Core.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Callora.Core.Tests.Hosting;

/// <summary>
/// Ein deaktiviertes Plugin gibt seinen Ladekontext wirklich frei.
/// </summary>
/// <remarks>
/// <para>
/// Das behauptet bisher kein Test. <c>ExportsChangedFiresBeforeUnloadTests</c> sagt ausdrücklich, es
/// prüfe „die MELDUNG, nicht den Erfolg der Deaktivierung" — und die übrigen Hosting-Tests rufen
/// <c>DeactivateAsync</c> ohne Zusicherung. Deshalb konnte in Produktion jede Aktualisierung jedes
/// Plugins mit „still pinned after unload" scheitern, während die Suite grün blieb.
/// </para>
/// <para>
/// <b>Der Unterschied liegt im Container.</b> Die vorhandenen Tests bauen den Host mit einem LEEREN
/// <c>ServiceCollection</c>: kein Logging, kein <see cref="IPluginDbContextProvider"/>. Damit läuft
/// weder der Weg, auf dem ein Plugin-Typ im Wurzel-Container landet, noch der, auf dem EF Core sein
/// Modell prozessweit ablegt. Die beiden Wege, die es in Produktion gibt, hatte kein Test je betreten.
/// </para>
/// <para>
/// Zugesichert wird hier, was <c>DeactivateAsync</c> ohnehin misst: Es sammelt den Kontext ein und
/// meldet Erfolg oder Misserfolg. Der Test liest diese Messung, statt eine zweite danebenzustellen.
/// </para>
/// </remarks>
[Collection(PluginLoadContextCollection.Name)]
public sealed class PluginLoadContextsAreCollectedTests
{
    [Fact]
    public async Task A_plugin_unloads_at_all()
    {
        // Der Grundfall, den nie jemand zugesichert hat: ein Plugin, das nichts Besonderes tut, in
        // einem leeren Container. Auch der scheiterte — die Prüfung stand in derselben async-Methode,
        // die das Plugin gerade noch angefasst hatte.
        await using var host = HostWith(_ => { });

        var install = await host.InstallAsync(
            TestPluginAssemblies.Exporting(), "Callora.TestPlugin.Exporting.ExportingTestPlugin");
        Assert.True(install.IsSuccess, install.Message);
        Assert.True((await host.ActivateAsync(install.Plugin!.PluginId)).IsSuccess);

        var deactivate = await host.DeactivateAsync(install.Plugin.PluginId);

        Assert.True(deactivate.IsSuccess, deactivate.Message);
    }

    [Fact]
    public async Task A_plugin_that_asked_for_its_own_logger_still_unloads()
    {
        // ILogger<EigenerTyp> aus den Diensten ist der Weg, den echte Plugins gehen. Er trägt den
        // geschlossenen generischen Typ in den Wurzel-Container — und dessen Auflösungs-Cache lebt so
        // lange wie der Prozess.
        await using var host = HostWith(services => services.AddLogging());

        var install = await host.InstallAsync(
            TestPluginAssemblies.Exporting(), "Callora.TestPlugin.Exporting.ExportingTestPlugin");
        Assert.True(install.IsSuccess, install.Message);
        Assert.True((await host.ActivateAsync(install.Plugin!.PluginId)).IsSuccess);

        var deactivate = await host.DeactivateAsync(install.Plugin.PluginId);

        Assert.True(deactivate.IsSuccess, deactivate.Message);
    }

    [Fact]
    public async Task A_plugin_that_built_its_ef_model_still_unloads()
    {
        // Ohne Datenbank: Gebaut wird das Modell, nicht verbunden. EF Core legt es in einem
        // prozessweiten Zwischenspeicher ab, dessen Schlüssel der Kontext-TYP ist — und der gehört dem
        // Plugin. Ohne einen eigenen internen Dienstanbieter je Plugin hält dieser Speicher den
        // Ladekontext, solange der Prozess läuft.
        await using var host = HostWith(services => services
            .AddLogging()
            .AddSingleton<IPluginDbContextProvider>(new NpgsqlPluginDbContextProvider(
                new BackendHostOptions
                {
                    DatabaseConnectionString = "Host=127.0.0.1;Port=1;Database=unused;Username=u;Password=p"
                })));

        var install = await host.InstallAsync(
            TestPluginAssemblies.DbContext(), "Callora.TestPlugin.DbContextPlugin.DbContextTestPlugin");
        Assert.True(install.IsSuccess, install.Message);
        Assert.True((await host.ActivateAsync(install.Plugin!.PluginId)).IsSuccess);

        var deactivate = await host.DeactivateAsync(install.Plugin.PluginId);

        Assert.True(deactivate.IsSuccess, deactivate.Message);
    }

    private static RuntimePluginHost HostWith(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);

        return new RuntimePluginHost(
            services.BuildServiceProvider(),
            new CalloraHostingOptions(),
            NullLogger<RuntimePluginHost>.Instance);
    }
}

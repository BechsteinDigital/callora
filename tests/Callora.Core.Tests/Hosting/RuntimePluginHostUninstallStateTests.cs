using Callora.Core.Application.Options;
using Callora.Core.Application.Plugins;
using Callora.Core.Tests.Cli;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Callora.Core.Tests.Hosting;

/// <summary>
/// Was der Host über ein Plugin sagt, das er nicht loswerden konnte. Die Frage ist nicht
/// akademisch: LoadedPlugins ist die Quelle für die Verfügbarkeitsprüfung und für die
/// Installationsliste der Verwaltung — steht dort Active, gilt ein gestopptes Plugin als laufend.
/// </summary>
[Collection(PluginLoadContextCollection.Name)]
public sealed class RuntimePluginHostUninstallStateTests
{
    [Fact]
    public async Task AFailedUninstallReportsTheHalfTornDownPluginAsUnloadFailed()
    {
        await using var host = CreateHost();
        var pluginId = await ActivateAsync(host, "Callora.TestPlugin.Exporting.FailingStopTestPlugin");

        var uninstall = await host.UninstallAsync(pluginId);

        Assert.Equal(RuntimePluginUninstallStatus.Failed, uninstall.Status);

        // Bis hierher ist das Plugin aus dem aktiven Satz heraus, gedraint und gestoppt — es steht
        // nur noch installiert da. Meldete es sich weiter als Active, hinge die Verwaltung an einer
        // Auskunft, die der Host selbst widerlegt hat.
        var descriptor = Assert.Single(host.LoadedPlugins, plugin => plugin.PluginId == pluginId);
        Assert.Equal(RuntimePluginState.UnloadFailed, descriptor.State);
    }

    /// <summary>
    /// Die Gegenprobe: Der Weg über die öffentliche Deaktivierung war schon immer richtig
    /// verbucht. Beide Wege müssen dasselbe sagen, sonst hängt die Auskunft davon ab, über welchen
    /// Knopf der Betreiber gegangen ist.
    /// </summary>
    [Fact]
    public async Task AFailedDeactivationReportsTheSameState()
    {
        await using var host = CreateHost();
        var pluginId = await ActivateAsync(host, "Callora.TestPlugin.Exporting.FailingStopTestPlugin");

        var deactivate = await host.DeactivateAsync(pluginId);

        Assert.False(deactivate.IsSuccess);
        var descriptor = Assert.Single(host.LoadedPlugins, plugin => plugin.PluginId == pluginId);
        Assert.Equal(RuntimePluginState.UnloadFailed, descriptor.State);
    }

    private static RuntimePluginHost CreateHost()
    {
        var services = new ServiceCollection();
        return new RuntimePluginHost(
            services.BuildServiceProvider(),
            new CalloraHostingOptions { PluginDrainTimeout = TimeSpan.Zero },
            NullLogger<RuntimePluginHost>.Instance);
    }

    private static async Task<string> ActivateAsync(RuntimePluginHost host, string entryTypeName)
    {
        var assemblyPath = ResolveFixturePluginAssemblyPath();
        Assert.True(File.Exists(assemblyPath), $"Test plugin was not built at {assemblyPath}.");

        var install = await host.InstallAsync(assemblyPath, entryTypeName);
        Assert.True(install.IsSuccess, install.Message);

        var activate = await host.ActivateAsync(install.Plugin!.PluginId);
        Assert.True(activate.IsSuccess, activate.Message);

        return install.Plugin.PluginId;
    }

    private static string ResolveFixturePluginAssemblyPath()
    {
        var testOutput = new DirectoryInfo(
            AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        return Path.Combine(
            ScaffoldedPluginFixture.ResolveRepositoryRoot(),
            "tests", "TestPlugins", "ExportingPlugin",
            "bin", testOutput.Parent!.Name, testOutput.Name,
            "Callora.TestPlugin.Exporting.dll");
    }
}

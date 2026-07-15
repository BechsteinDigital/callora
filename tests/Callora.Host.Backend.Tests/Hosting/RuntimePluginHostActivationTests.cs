using System.Runtime.CompilerServices;
using Callora.Host.Backend.Tests.Cli;
using Callora.Host.PluginContracts.Application.Persistence;
using Callora.Hosting.Application.Options;
using Callora.Hosting.Application.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Host.Backend.Tests.Hosting;

/// <summary>
/// End-to-end proof of the plugin runtime chain against a real plugin ALC
/// (closes the by-parts caveat behind the WP-3c export bridge and the §14 purge
/// aggregator): activation loads and starts the plugin, its <c>context.Export</c>
/// is resolvable by the host across the ALC boundary (unified <c>Callora.*</c>
/// contract identity), and exports are withdrawn on deactivation (REV2 §9.3).
/// The plugin assembly is built with the solution, then loaded through a real
/// <c>PluginAssemblyLoadContext</c> — no service mocking of the export path.
/// </summary>
public sealed class RuntimePluginHostActivationTests
{
    [Fact]
    public async Task ActivatePlugin_ExportIsResolvableCrossAlc_AndWithdrawnOnDeactivate()
    {
        var assemblyPath = ResolveExportingPluginAssemblyPath();
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

        // The export appears only through activation, not from installation.
        Assert.Empty(host.GetExports<IWorkspaceDataPurgeContributor>());

        var activate = await host.ActivateAsync(pluginId);
        Assert.True(activate.IsSuccess, activate.Message);

        // The whole chain: a plugin exported the contract from its own ALC and
        // the host resolves it — only possible if the Callora.* contract type is
        // unified across the plugin and default load contexts. Asserted in a
        // non-inlined helper so no reference to the plugin-ALC instance escapes.
        AssertExportResolvable(host);

        // Deactivation withdraws the shared export (REV2 §9.3). Whether the ALC
        // is additionally GC-collected in-process is timing-dependent (H2's
        // runtime concern and unreliable inside a test host), so this asserts
        // the withdrawal, not the collection.
        await host.DeactivateAsync(pluginId);

        Assert.Empty(host.GetExports<IWorkspaceDataPurgeContributor>());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AssertExportResolvable(RuntimePluginHost host)
    {
        Assert.Single(host.GetExports<IWorkspaceDataPurgeContributor>());
        Assert.True(host.TryGetExport(typeof(IWorkspaceDataPurgeContributor), out var exported));
        Assert.NotNull(exported);
    }

    private static string ResolveExportingPluginAssemblyPath()
    {
        // The plugin builds to bin/<Config>/<Tfm>/ just like this test assembly,
        // so mirror the current build config/framework from the test's location.
        var testOutput = new DirectoryInfo(
            AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var targetFramework = testOutput.Name;
        var configuration = testOutput.Parent!.Name;

        return Path.Combine(
            ScaffoldedPluginFixture.ResolveRepositoryRoot(),
            "tests", "TestPlugins", "ExportingPlugin",
            "bin", configuration, targetFramework,
            "Callora.TestPlugin.Exporting.dll");
    }
}

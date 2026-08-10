using Xunit;
using Callora.Core.Application.Options;
using Callora.Core.Application.Plugins;
using Callora.Core.Tests.Cli;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Core.Tests.Hosting;

/// <summary>
/// Regression guard for the plugin ALC framework-assembly unification: a bundled
/// plugin that ships its own copy of a host framework assembly (EF Core here) must
/// still resolve that assembly to the host's copy. Otherwise a plugin type deriving
/// from a host framework base type (a <c>DbContext</c>) gets a duplicate type
/// identity and violates the host generic constraint
/// (<c>IPluginDbContextFactory&lt;TContext&gt; where TContext : DbContext</c>) —
/// the production failure seen when deploying the Communication plugin as a signed
/// full-publish bundle. The fixture plugin forces that constraint check in its
/// <c>StartAsync</c>, so activation succeeds only when EF Core is unified.
/// </summary>
[Collection(PluginLoadContextCollection.Name)]
public sealed class RuntimePluginHostDbContextActivationTests
{
    [Fact]
    public async Task ActivatePlugin_WithBundledDuplicateEfCore_UnifiesToHostAndActivates()
    {
        var assemblyPath = ResolveDbContextPluginAssemblyPath();
        Assert.True(File.Exists(assemblyPath), $"Test plugin was not built at {assemblyPath}.");

        // The fixture genuinely ships its own EF Core copy next to the assembly —
        // the exact bundle condition. Without unification the ALC would load this
        // as a duplicate identity; the fix must resolve it to the host's copy.
        var bundledEfCore = Path.Combine(
            Path.GetDirectoryName(assemblyPath)!,
            "Microsoft.EntityFrameworkCore.dll");
        Assert.True(File.Exists(bundledEfCore),
            $"Fixture must bundle its own EF Core copy at {bundledEfCore} for this regression to be meaningful.");

        await using var host = new RuntimePluginHost(
            new ServiceCollection().BuildServiceProvider(),
            new CalloraHostingOptions(),
            NullLogger<RuntimePluginHost>.Instance);

        var install = await host.InstallAsync(
            assemblyPath,
            "Callora.TestPlugin.DbContextPlugin.DbContextTestPlugin");
        Assert.True(install.IsSuccess, install.Message);
        var pluginId = install.Plugin!.PluginId;

        // Before the fix this fails with a TypeLoadException about the
        // IPluginDbContextFactory<TContext> constraint; after the fix it succeeds.
        var activate = await host.ActivateAsync(pluginId);
        Assert.True(activate.IsSuccess, activate.Message);

        await host.DeactivateAsync(pluginId);
    }

    private static string ResolveDbContextPluginAssemblyPath()
    {
        // The plugin builds to bin/<Config>/<Tfm>/ just like this test assembly,
        // so mirror the current build config/framework from the test's location.
        var testOutput = new DirectoryInfo(
            AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var targetFramework = testOutput.Name;
        var configuration = testOutput.Parent!.Name;

        return Path.Combine(
            ScaffoldedPluginFixture.ResolveRepositoryRoot(),
            "tests", "TestPlugins", "DbContextPlugin",
            "bin", configuration, targetFramework,
            "Callora.TestPlugin.DbContext.dll");
    }
}

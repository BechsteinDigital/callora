using Callora.Core.Application.Options;
using Callora.Core.Application.Plugins;
using Callora.Core.Tests.Cli;
using Callora.Core.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Callora.Core.Tests.Hosting;

/// <summary>
/// Draining is what stands between a deactivation and the work it would otherwise cut through
/// (ADR-018 §2.1). These run against a real plugin load context, because the ordering being asserted
/// is the host's, not a mock's.
/// </summary>
[Collection(PluginLoadContextCollection.Name)]
public sealed class RuntimePluginHostDrainTests
{
    [Fact]
    public async Task APluginIsDrainedBeforeItIsStopped()
    {
        var lifecycle = new CapturingLoggerFactory();
        await using var host = CreateHost(lifecycle, TimeSpan.FromSeconds(5));
        var pluginId = await ActivateAsync(host, "Callora.TestPlugin.Exporting.DrainingTestPlugin");

        await host.DeactivateAsync(pluginId);

        // The order is the whole point: work that is still finishing gets its chance before the
        // plugin is taken apart.
        Assert.Equal(["start", "drain", "stop"], lifecycle.Messages);
    }

    [Fact]
    public async Task APluginThatNeverRunsDryIsStoppedWhenTheDeadlineExpires()
    {
        var lifecycle = new CapturingLoggerFactory();
        await using var host = CreateHost(lifecycle, TimeSpan.FromMilliseconds(200));
        var pluginId = await ActivateAsync(host, "Callora.TestPlugin.Exporting.StubbornDrainTestPlugin");

        await host.DeactivateAsync(pluginId);

        // A drain may delay a deactivation, never prevent it: the plugin saw its cancellation, was
        // stopped anyway, and is gone from the active set.
        Assert.Equal(["start", "drain-begin", "drain-cancelled", "stop"], lifecycle.Messages);
        await AssertNoLongerActiveAsync(host, pluginId);
    }

    [Fact]
    public async Task AZeroDeadlineSkipsDrainingEntirely()
    {
        var lifecycle = new CapturingLoggerFactory();
        await using var host = CreateHost(lifecycle, TimeSpan.Zero);
        // The stubborn plugin would block forever if it were asked; a zero deadline means it is not.
        var pluginId = await ActivateAsync(host, "Callora.TestPlugin.Exporting.StubbornDrainTestPlugin");

        await host.DeactivateAsync(pluginId);

        Assert.Equal(["start", "stop"], lifecycle.Messages);
    }

    [Fact]
    public async Task APluginWithoutTheContractIsUnaffected()
    {
        var lifecycle = new CapturingLoggerFactory();
        await using var host = CreateHost(lifecycle, TimeSpan.FromSeconds(5));
        var pluginId = await ActivateAsync(host, "Callora.TestPlugin.Exporting.ExportingTestPlugin");

        await host.DeactivateAsync(pluginId);

        // No drain contract, no drain step — and the stop still happens, with a generous deadline
        // configured that nothing waits on.
        Assert.Empty(lifecycle.Messages);
        await AssertNoLongerActiveAsync(host, pluginId);
    }

    /// <summary>
    /// Asserts the plugin was stopped, without asking whether its load context was also collected.
    /// Collection is timing-dependent inside a test host — the pre-existing activation harness makes
    /// the same distinction — so a deactivation that reports the context as pinned still proves the
    /// stop ran. A second deactivation answering "already inactive" is the GC-independent evidence.
    /// </summary>
    private static async Task AssertNoLongerActiveAsync(RuntimePluginHost host, string pluginId) =>
        Assert.Equal(
            RuntimePluginDeactivateStatus.AlreadyInactive,
            (await host.DeactivateAsync(pluginId)).Status);

    private static RuntimePluginHost CreateHost(CapturingLoggerFactory lifecycle, TimeSpan drainTimeout)
    {
        // The factory reaches the plugin through the curated service provider, which hands out
        // logging — the fixture's only way to report back across the load-context boundary.
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(lifecycle);

        return new RuntimePluginHost(
            services.BuildServiceProvider(),
            new CalloraHostingOptions { PluginDrainTimeout = drainTimeout },
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

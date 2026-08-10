using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Callora.Core.Application.Lifecycle;
using Callora.Core.Application.Options;
using Callora.Core.Application.Plugins;
using Callora.Core.Domain.Plugins;
using Callora.Core.Tests.Cli;
using Callora.Core.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Callora.Core.Tests.Hosting;

/// <summary>
/// End-to-end proof of the runtime-capability chain through to the gate consumers rely on
/// (RuntimePluginHost → RuntimeCapabilityRegistry → PluginCapabilityGuard). A provider plugin declares
/// a capability conditionally and exports an <c>IRuntimeCapabilitySource</c> that grants it; a dependent
/// plugin requires it. Activating the provider registers its source into the registry, and the guard —
/// the same one the activation/availability paths use — then treats the dependent's requirement as
/// satisfied. Deactivation unregisters the source and the requirement is denied again. The existing
/// <see cref="RuntimePluginHostActivationTests"/> stops at <c>registry.IsSatisfied</c>; this closes the
/// last hop to the guard, over a real plugin ALC (no mocking of the export path).
/// </summary>
[Collection(PluginLoadContextCollection.Name)]
public sealed class RuntimeCapabilityGateEndToEndTests
{
    // These match the fixed grant baked into ExportingTestPlugin's runtime-capability source.
    private const string Capability = "exporting-test.capability";
    private const string WorkspaceKey = "ws-test";
    private const string DependentPluginId = "dependent-consumer";

    [Fact]
    public async Task ConditionalCapability_GrantedByActivatedSource_FlipsGuardFromDeniedToAllowed()
    {
        var assemblyPath = ResolveExportingPluginAssemblyPath();
        Assert.True(File.Exists(assemblyPath), $"Test plugin was not built at {assemblyPath}.");

        // Grace zero: a loss should surface immediately in this deterministic wiring test (grace damping
        // itself is covered by RuntimeCapabilityRegistryTests).
        var registry = new RuntimeCapabilityRegistry(TimeSpan.Zero, TimeProvider.System);
        await using var host = new RuntimePluginHost(
            new ServiceCollection().BuildServiceProvider(),
            new CalloraHostingOptions(),
            NullLogger<RuntimePluginHost>.Instance,
            registry);

        var install = await host.InstallAsync(
            assemblyPath,
            "Callora.TestPlugin.Exporting.ExportingTestPlugin");
        Assert.True(install.IsSuccess, install.Message);
        var providerPluginId = install.Plugin!.PluginId;

        // The provider declares the capability conditionally; the dependent requires it. The guard reads
        // both from the installation repository and consults the same registry the host feeds.
        var installations = new InMemoryPluginInstallationRepository();
        await installations.AddAsync(CreateInstallation(providerPluginId, conditional: [Capability]));
        await installations.AddAsync(CreateInstallation(DependentPluginId, requires: [Capability]));
        var activations = new StaticWorkspacePluginActivationReader([providerPluginId]);
        var guard = new PluginCapabilityGuard(installations, activations, registry);

        // Before activation the source is not registered → no grant → the requirement is denied.
        Assert.False(registry.IsSatisfied(providerPluginId, Capability, WorkspaceKey));
        var beforeActivation = await guard.CheckActivationAsync(DependentPluginId, WorkspaceKey, CancellationToken.None);
        Assert.False(beforeActivation.IsAllowed);

        // Activation starts the plugin, collects its export and registers the source → grant is live.
        var activate = await host.ActivateAsync(providerPluginId);
        Assert.True(activate.IsSuccess, activate.Message);

        Assert.True(registry.IsSatisfied(providerPluginId, Capability, WorkspaceKey));
        var whileActive = await guard.CheckActivationAsync(DependentPluginId, WorkspaceKey, CancellationToken.None);
        Assert.True(whileActive.IsAllowed); // the conditional capability now satisfies the dependent

        // Deactivation withdraws the export and unregisters the source → the grant is gone again.
        await host.DeactivateAsync(providerPluginId);

        Assert.False(registry.IsSatisfied(providerPluginId, Capability, WorkspaceKey));
        var afterDeactivation = await guard.CheckActivationAsync(DependentPluginId, WorkspaceKey, CancellationToken.None);
        Assert.False(afterDeactivation.IsAllowed);
    }

    private static PluginInstallation CreateInstallation(
        string pluginId,
        string[]? requires = null,
        string[]? conditional = null)
    {
        var installation = PluginInstallation.CreateInstalled(
            pluginId,
            pluginId,
            $"/tmp/{pluginId}.dll",
            null,
            DateTimeOffset.UtcNow);
        installation.SetCapabilities(providedCapabilities: null, requires, conditional, DateTimeOffset.UtcNow);
        return installation;
    }

    // Mirrors RuntimePluginHostActivationTests: the plugin builds to bin/<Config>/<Tfm>/ like this test.
    private static string ResolveExportingPluginAssemblyPath()
    {
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

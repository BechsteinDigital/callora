using Callora.Core.Application.Lifecycle;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Domain.Plugins;
using Callora.Core.Tests.Application.Plugins;
using Callora.Core.Tests.Support;
using Xunit;

namespace Callora.Core.Tests.Application.Lifecycle;

public sealed class PluginCapabilityGuardTests
{
    private const string VoiceCapability = "communication.voice";

    [Fact]
    public async Task CheckActivation_NoRequirements_IsAllowed()
    {
        var (guard, installations, _) = CreateGuard();
        await installations.AddAsync(CreateInstallation("dialer"));

        var result = await guard.CheckActivationAsync("dialer", "workspace-a", CancellationToken.None);

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task CheckActivation_WorkspaceScope_DeniedWhenNoProviderActive()
    {
        var (guard, installations, _) = CreateGuard();
        await installations.AddAsync(CreateInstallation("voice", provides: [VoiceCapability]));
        await installations.AddAsync(CreateInstallation("dialer", requires: [VoiceCapability]));

        var result = await guard.CheckActivationAsync("dialer", "workspace-a", CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.Contains(VoiceCapability, result.Message);
    }

    [Fact]
    public async Task CheckActivation_WorkspaceScope_AllowedWhenProviderActive()
    {
        var (guard, installations, activations) = CreateGuard();
        await installations.AddAsync(CreateInstallation("voice", provides: [VoiceCapability]));
        await installations.AddAsync(CreateInstallation("dialer", requires: [VoiceCapability]));
        await activations.SetActiveAsync("voice", "workspace-a", "tenant-1", true);

        var result = await guard.CheckActivationAsync("dialer", "workspace-a", CancellationToken.None);

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task CheckActivation_WorkspaceScope_IgnoresProviderInstalledButNotActivated()
    {
        // PLAT-253: a provider merely installed (or entitled) but not activated in the workspace
        // does not satisfy a capability requirement — only actual activation counts.
        var (guard, installations, activations) = CreateGuard();
        await installations.AddAsync(CreateInstallation("voice", provides: [VoiceCapability]));
        await installations.AddAsync(CreateInstallation("dialer", requires: [VoiceCapability]));
        await activations.SetActiveAsync("voice", "workspace-a", "tenant-1", false);

        var result = await guard.CheckActivationAsync("dialer", "workspace-a", CancellationToken.None);

        Assert.False(result.IsAllowed);
    }

    [Fact]
    public async Task CheckActivation_WorkspaceScope_IgnoresProviderActiveInOtherWorkspace()
    {
        // Activation is per-workspace; a provider active only in another workspace must not count.
        var (guard, installations, activations) = CreateGuard();
        await installations.AddAsync(CreateInstallation("voice", provides: [VoiceCapability]));
        await installations.AddAsync(CreateInstallation("dialer", requires: [VoiceCapability]));
        await activations.SetActiveAsync("voice", "workspace-b", "tenant-1", true);

        var result = await guard.CheckActivationAsync("dialer", "workspace-a", CancellationToken.None);

        Assert.False(result.IsAllowed);
    }

    [Fact]
    public async Task CheckActivation_GlobalScope_RequiresGloballyActiveProvider()
    {
        var (guard, installations, _) = CreateGuard();
        await installations.AddAsync(CreateInstallation("voice", provides: [VoiceCapability]));
        await installations.AddAsync(CreateInstallation("dialer", requires: [VoiceCapability]));

        var deniedWhileInactive = await guard.CheckActivationAsync("dialer", null, CancellationToken.None);
        Assert.False(deniedWhileInactive.IsAllowed);

        var voice = await installations.GetByPluginIdAsync("voice");
        voice!.MarkActivated(DateTimeOffset.UtcNow);

        var allowedWhenActive = await guard.CheckActivationAsync("dialer", null, CancellationToken.None);
        Assert.True(allowedWhenActive.IsAllowed);
    }

    [Fact]
    public async Task CheckDeactivation_DeniedWhileActiveDependentNeedsCapability()
    {
        var (guard, installations, activations) = CreateGuard();
        await installations.AddAsync(CreateInstallation("voice", provides: [VoiceCapability]));
        await installations.AddAsync(CreateInstallation("dialer", requires: [VoiceCapability]));
        await activations.SetActiveAsync("voice", "workspace-a", "tenant-1", true);
        await activations.SetActiveAsync("dialer", "workspace-a", "tenant-1", true);

        var result = await guard.CheckDeactivationAsync("voice", "workspace-a", CancellationToken.None);

        Assert.False(result.IsAllowed);
        Assert.Contains("dialer", result.Message);
    }

    [Fact]
    public async Task CheckDeactivation_AllowedWhenAlternativeProviderIsActive()
    {
        var (guard, installations, activations) = CreateGuard();
        await installations.AddAsync(CreateInstallation("voice", provides: [VoiceCapability]));
        await installations.AddAsync(CreateInstallation("voice-backup", provides: [VoiceCapability]));
        await installations.AddAsync(CreateInstallation("dialer", requires: [VoiceCapability]));
        await activations.SetActiveAsync("voice", "workspace-a", "tenant-1", true);
        await activations.SetActiveAsync("voice-backup", "workspace-a", "tenant-1", true);
        await activations.SetActiveAsync("dialer", "workspace-a", "tenant-1", true);

        var result = await guard.CheckDeactivationAsync("voice", "workspace-a", CancellationToken.None);

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task CheckDeactivation_AllowedWhenDependentIsNotActive()
    {
        var (guard, installations, activations) = CreateGuard();
        await installations.AddAsync(CreateInstallation("voice", provides: [VoiceCapability]));
        await installations.AddAsync(CreateInstallation("dialer", requires: [VoiceCapability]));
        await activations.SetActiveAsync("voice", "workspace-a", "tenant-1", true);

        var result = await guard.CheckDeactivationAsync("voice", "workspace-a", CancellationToken.None);

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task CheckActivation_ConditionalProvider_SatisfiedInWorkspace_Allowed()
    {
        var registry = RegistryWith("voice", new RuntimeCapabilityGrant(VoiceCapability, "workspace-a"));
        var (guard, installations, activations) = CreateGuard(registry);
        await installations.AddAsync(CreateInstallation("voice", conditional: [VoiceCapability]));
        await installations.AddAsync(CreateInstallation("dialer", requires: [VoiceCapability]));
        await activations.SetActiveAsync("voice", "workspace-a", "tenant-1", true);

        var result = await guard.CheckActivationAsync("dialer", "workspace-a", CancellationToken.None);

        Assert.True(result.IsAllowed); // conditional capability, satisfied by a runtime grant
    }

    [Fact]
    public async Task CheckActivation_ConditionalProvider_NotSatisfied_Denied()
    {
        // The provider is active and declares the capability conditionally, but no runtime grant exists.
        var (guard, installations, activations) = CreateGuard(RegistryWith("voice"));
        await installations.AddAsync(CreateInstallation("voice", conditional: [VoiceCapability]));
        await installations.AddAsync(CreateInstallation("dialer", requires: [VoiceCapability]));
        await activations.SetActiveAsync("voice", "workspace-a", "tenant-1", true);

        var result = await guard.CheckActivationAsync("dialer", "workspace-a", CancellationToken.None);

        Assert.False(result.IsAllowed);
    }

    [Fact]
    public async Task CheckActivation_ConditionalProvider_GlobalGrantCoversWorkspaceConsumer()
    {
        var registry = RegistryWith("voice", new RuntimeCapabilityGrant(VoiceCapability, WorkspaceKey: null));
        var (guard, installations, activations) = CreateGuard(registry);
        await installations.AddAsync(CreateInstallation("voice", conditional: [VoiceCapability]));
        await installations.AddAsync(CreateInstallation("dialer", requires: [VoiceCapability]));
        await activations.SetActiveAsync("voice", "workspace-a", "tenant-1", true);

        var result = await guard.CheckActivationAsync("dialer", "workspace-a", CancellationToken.None);

        Assert.True(result.IsAllowed); // a global grant covers every workspace (spec §8)
    }

    [Fact]
    public async Task CheckActivation_ConditionalProvider_WorkspaceGrantDoesNotCoverGlobalConsumer()
    {
        var registry = RegistryWith("voice", new RuntimeCapabilityGrant(VoiceCapability, "workspace-a"));
        var (guard, installations, _) = CreateGuard(registry);
        var voice = CreateInstallation("voice", conditional: [VoiceCapability]);
        voice.MarkActivated(DateTimeOffset.UtcNow); // globally active
        await installations.AddAsync(voice);
        await installations.AddAsync(CreateInstallation("dialer", requires: [VoiceCapability]));

        var result = await guard.CheckActivationAsync("dialer", workspaceKey: null, CancellationToken.None);

        Assert.False(result.IsAllowed); // a workspace grant does not satisfy a global consumer
    }

    [Fact]
    public async Task CheckDeactivation_ConditionalProvider_BlockedWhenActiveDependentNeedsIt()
    {
        var registry = RegistryWith("voice", new RuntimeCapabilityGrant(VoiceCapability, "workspace-a"));
        var (guard, installations, activations) = CreateGuard(registry);
        await installations.AddAsync(CreateInstallation("voice", conditional: [VoiceCapability]));
        await installations.AddAsync(CreateInstallation("dialer", requires: [VoiceCapability]));
        await activations.SetActiveAsync("voice", "workspace-a", "tenant-1", true);
        await activations.SetActiveAsync("dialer", "workspace-a", "tenant-1", true);

        var result = await guard.CheckDeactivationAsync("voice", "workspace-a", CancellationToken.None);

        Assert.False(result.IsAllowed); // an active dependent still needs the conditionally-provided capability
    }

    private static (PluginCapabilityGuard Guard, InMemoryPluginInstallationRepository Installations, InMemoryWorkspacePluginActivationStore Activations)
        CreateGuard(RuntimeCapabilityRegistry? runtimeCapabilities = null)
    {
        var installations = new InMemoryPluginInstallationRepository();
        var activations = new InMemoryWorkspacePluginActivationStore();
        var guard = new PluginCapabilityGuard(installations, activations, runtimeCapabilities);
        return (guard, installations, activations);
    }

    private static RuntimeCapabilityRegistry RegistryWith(string pluginId, params RuntimeCapabilityGrant[] grants)
    {
        var registry = new RuntimeCapabilityRegistry(TimeSpan.Zero, TimeProvider.System);
        registry.Register(pluginId, new FakeRuntimeCapabilitySource(grants));
        return registry;
    }

    private static PluginInstallation CreateInstallation(
        string pluginId,
        string[]? provides = null,
        string[]? requires = null,
        string[]? conditional = null)
    {
        var installation = PluginInstallation.CreateInstalled(
            pluginId,
            pluginId,
            $"/tmp/{pluginId}.dll",
            null,
            DateTimeOffset.UtcNow);
        installation.SetCapabilities(provides, requires, conditional, DateTimeOffset.UtcNow);
        return installation;
    }
}

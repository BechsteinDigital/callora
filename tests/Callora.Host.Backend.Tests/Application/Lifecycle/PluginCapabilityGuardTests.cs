using Callora.Host.Backend.Application.Abstractions;
using Callora.Host.Backend.Application.Lifecycle;
using Callora.Host.Backend.Application.Policies;
using Callora.Host.Backend.Domain.Plugins;
using Callora.Host.Backend.Tests.Support;
using Xunit;

namespace Callora.Host.Backend.Tests.Application.Lifecycle;

public sealed class PluginCapabilityGuardTests
{
    private const string VoiceCapability = "communication.voice";

    [Fact]
    public async Task CheckActivation_NoRequirements_IsAllowed()
    {
        var (guard, installations, _) = await CreateGuardAsync();
        await installations.AddAsync(CreateInstallation("dialer"));

        var result = await guard.CheckActivationAsync("dialer", "workspace-a", CancellationToken.None, "tenant-1");

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task CheckActivation_WorkspaceScope_DeniedWhenNoProviderEntitled()
    {
        var (guard, installations, _) = await CreateGuardAsync();
        await installations.AddAsync(CreateInstallation("voice", provides: [VoiceCapability]));
        await installations.AddAsync(CreateInstallation("dialer", requires: [VoiceCapability]));

        var result = await guard.CheckActivationAsync("dialer", "workspace-a", CancellationToken.None, "tenant-1");

        Assert.False(result.IsAllowed);
        Assert.Contains(VoiceCapability, result.Message);
    }

    [Fact]
    public async Task CheckActivation_WorkspaceScope_AllowedWhenProviderEntitled()
    {
        var (guard, installations, entitlements) = await CreateGuardAsync();
        await installations.AddAsync(CreateInstallation("voice", provides: [VoiceCapability]));
        await installations.AddAsync(CreateInstallation("dialer", requires: [VoiceCapability]));
        await entitlements.SetEntitledAsync("voice", true, "workspace-a", "tenant-1");

        var result = await guard.CheckActivationAsync("dialer", "workspace-a", CancellationToken.None, "tenant-1");

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task CheckActivation_GlobalScope_RequiresGloballyActiveProvider()
    {
        var (guard, installations, _) = await CreateGuardAsync();
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
    public async Task CheckDeactivation_DeniedWhileEntitledDependentNeedsCapability()
    {
        var (guard, installations, entitlements) = await CreateGuardAsync();
        await installations.AddAsync(CreateInstallation("voice", provides: [VoiceCapability]));
        await installations.AddAsync(CreateInstallation("dialer", requires: [VoiceCapability]));
        await entitlements.SetEntitledAsync("voice", true, "workspace-a", "tenant-1");
        await entitlements.SetEntitledAsync("dialer", true, "workspace-a", "tenant-1");

        var result = await guard.CheckDeactivationAsync("voice", "workspace-a", CancellationToken.None, "tenant-1");

        Assert.False(result.IsAllowed);
        Assert.Contains("dialer", result.Message);
    }

    [Fact]
    public async Task CheckDeactivation_AllowedWhenAlternativeProviderIsEntitled()
    {
        var (guard, installations, entitlements) = await CreateGuardAsync();
        await installations.AddAsync(CreateInstallation("voice", provides: [VoiceCapability]));
        await installations.AddAsync(CreateInstallation("voice-backup", provides: [VoiceCapability]));
        await installations.AddAsync(CreateInstallation("dialer", requires: [VoiceCapability]));
        await entitlements.SetEntitledAsync("voice", true, "workspace-a", "tenant-1");
        await entitlements.SetEntitledAsync("voice-backup", true, "workspace-a", "tenant-1");
        await entitlements.SetEntitledAsync("dialer", true, "workspace-a", "tenant-1");

        var result = await guard.CheckDeactivationAsync("voice", "workspace-a", CancellationToken.None, "tenant-1");

        Assert.True(result.IsAllowed);
    }

    [Fact]
    public async Task CheckDeactivation_AllowedWhenDependentIsNotEntitled()
    {
        var (guard, installations, entitlements) = await CreateGuardAsync();
        await installations.AddAsync(CreateInstallation("voice", provides: [VoiceCapability]));
        await installations.AddAsync(CreateInstallation("dialer", requires: [VoiceCapability]));
        await entitlements.SetEntitledAsync("voice", true, "workspace-a", "tenant-1");

        var result = await guard.CheckDeactivationAsync("voice", "workspace-a", CancellationToken.None, "tenant-1");

        Assert.True(result.IsAllowed);
    }

    private static Task<(PluginCapabilityGuard Guard, InMemoryPluginInstallationRepository Installations, IPluginEntitlementStore Entitlements)>
        CreateGuardAsync()
    {
        var installations = new InMemoryPluginInstallationRepository();
        var entitlements = new InMemoryPluginEntitlementStore(new BackendHostOptions());
        var guard = new PluginCapabilityGuard(installations, entitlements);
        return Task.FromResult<(PluginCapabilityGuard, InMemoryPluginInstallationRepository, IPluginEntitlementStore)>(
            (guard, installations, entitlements));
    }

    private static PluginInstallation CreateInstallation(
        string pluginId,
        string[]? provides = null,
        string[]? requires = null)
    {
        var installation = PluginInstallation.CreateInstalled(
            pluginId,
            pluginId,
            $"/tmp/{pluginId}.dll",
            null,
            DateTimeOffset.UtcNow);
        installation.SetCapabilities(provides, requires, DateTimeOffset.UtcNow);
        return installation;
    }
}

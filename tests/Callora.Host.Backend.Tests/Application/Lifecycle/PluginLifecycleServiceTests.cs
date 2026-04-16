using Callora.Host.Backend.Application.Abstractions;
using Callora.Host.Backend.Application.Abstractions.Plugins;
using Callora.Host.Backend.Application.Lifecycle;
using Callora.Host.Backend.Domain.Plugins;
using Callora.Host.Backend.Tests.Support;

namespace Callora.Host.Backend.Tests.Application.Lifecycle;

public sealed class PluginLifecycleServiceTests
{
    [Fact]
    public async Task ActivateAsync_DeniedByPolicy_ReturnsForbidden_AndWritesAudit()
    {
        var lifecycle = new FakeHostPluginLifecycle();
        var policy = new StaticPluginActivationPolicy(PluginActivationDecision.Deny("blocked"));
        var audit = new InMemoryHostAuditStore();
        var installations = new InMemoryPluginInstallationRepository();
        var unitOfWork = new NoOpHostUnitOfWork();
        var registryReader = new StaticPluginPackageRegistryReader();
        var events = new RecordingHostApplicationEventPublisher();
        var sut = new PluginLifecycleService(
            lifecycle,
            policy,
            audit,
            installations,
            unitOfWork,
            registryReader,
            new StaticNuGetPluginAssemblyResolver(),
            events);

        var result = await sut.ActivateAsync(new PluginLifecycleCommand("plugin-x", "tester"));

        Assert.Equal(PluginLifecycleServiceStatus.Forbidden, result.Status);
        Assert.False(result.IsSuccess);

        var entries = await audit.GetRecentAsync();
        Assert.Single(entries);
        Assert.Equal("plugin.activate", entries[0].Action);
        Assert.False(entries[0].IsSuccess);
        Assert.Single(events.Events);
    }

    [Fact]
    public async Task InstallAsync_Success_ReturnsOk_AndWritesAudit()
    {
        var lifecycle = new FakeHostPluginLifecycle
        {
            InstallResult = new VoipHost.PluginContracts.Application.Plugins.HostPluginOperationResult(
                VoipHost.PluginContracts.Application.Plugins.HostPluginOperation.Install,
                true,
                "plugin-install",
                null)
        };
        var policy = new StaticPluginActivationPolicy(PluginActivationDecision.Allow());
        var audit = new InMemoryHostAuditStore();
        var installations = new InMemoryPluginInstallationRepository();
        var unitOfWork = new NoOpHostUnitOfWork();
        var registryReader = new StaticPluginPackageRegistryReader();
        var events = new RecordingHostApplicationEventPublisher();
        var sut = new PluginLifecycleService(
            lifecycle,
            policy,
            audit,
            installations,
            unitOfWork,
            registryReader,
            new StaticNuGetPluginAssemblyResolver(),
            events);

        var result = await sut.InstallAsync(new InstallPluginCommand("/tmp/plugin.dll", null, "tester"));

        Assert.Equal(PluginLifecycleServiceStatus.Ok, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal("plugin-install", result.PluginId);

        var entries = await audit.GetRecentAsync();
        Assert.Single(entries);
        Assert.Equal("plugin.install", entries[0].Action);
        Assert.True(entries[0].IsSuccess);

        var installation = await installations.GetByPluginIdAsync("plugin-install");
        Assert.NotNull(installation);
        Assert.Equal(PluginInstallationState.Installed, installation.State);
        Assert.Single(events.Events);
    }

    [Fact]
    public async Task ActivateAsync_Success_PersistsActiveState()
    {
        var lifecycle = new FakeHostPluginLifecycle
        {
            ActivateResult = new VoipHost.PluginContracts.Application.Plugins.HostPluginOperationResult(
                VoipHost.PluginContracts.Application.Plugins.HostPluginOperation.Activate,
                true,
                "plugin-x",
                null)
        };
        var policy = new StaticPluginActivationPolicy(PluginActivationDecision.Allow());
        var audit = new InMemoryHostAuditStore();
        var installations = new InMemoryPluginInstallationRepository();
        await installations.AddAsync(PluginInstallation.CreateInstalled(
            "plugin-x",
            "Plugin X",
            "/tmp/plugin-x.dll",
            null,
            DateTimeOffset.UtcNow));

        var sut = new PluginLifecycleService(
            lifecycle,
            policy,
            audit,
            installations,
            new NoOpHostUnitOfWork(),
            new StaticPluginPackageRegistryReader(),
            new StaticNuGetPluginAssemblyResolver(),
            new RecordingHostApplicationEventPublisher());

        var result = await sut.ActivateAsync(new PluginLifecycleCommand("plugin-x", "tester"));

        Assert.Equal(PluginLifecycleServiceStatus.Ok, result.Status);
        var installation = await installations.GetByPluginIdAsync("plugin-x");
        Assert.NotNull(installation);
        Assert.Equal(PluginInstallationState.Active, installation.State);
    }

    [Fact]
    public async Task InstallAsync_UsesRegistryEntryType_WhenRequestEntryTypeIsNull()
    {
        var lifecycle = new FakeHostPluginLifecycle
        {
            InstallResult = new VoipHost.PluginContracts.Application.Plugins.HostPluginOperationResult(
                VoipHost.PluginContracts.Application.Plugins.HostPluginOperation.Install,
                true,
                "plugin-reg",
                null)
        };
        var policy = new StaticPluginActivationPolicy(PluginActivationDecision.Allow());
        var audit = new InMemoryHostAuditStore();
        var installations = new InMemoryPluginInstallationRepository();
        var registryReader = new StaticPluginPackageRegistryReader
        {
            Result = new PluginPackageRegistryReadResult(
                HasRegistryFile: true,
                IsValid: true,
                RegistryPath: "/tmp/registry.json",
                Registry: new PluginPackageRegistryMetadata(
                    "1.0",
                    "Callora Voip Plugin",
                    "plugin-reg",
                    "1.0.0",
                    "plugin-reg.dll",
                    "Plugins.Entry",
                    [],
                    new Dictionary<string, string>()))
        };

        var sut = new PluginLifecycleService(
            lifecycle,
            policy,
            audit,
            installations,
            new NoOpHostUnitOfWork(),
            registryReader,
            new StaticNuGetPluginAssemblyResolver(),
            new RecordingHostApplicationEventPublisher());

        var result = await sut.InstallAsync(new InstallPluginCommand("/tmp/plugin-reg.dll", null, "tester"));

        Assert.Equal(PluginLifecycleServiceStatus.Ok, result.Status);
        Assert.Equal("Plugins.Entry", lifecycle.LastInstallEntryTypeName);
    }

    [Fact]
    public async Task InstallAsync_InvalidRegistry_ReturnsBadRequest_WithoutInstallCall()
    {
        var lifecycle = new FakeHostPluginLifecycle();
        var policy = new StaticPluginActivationPolicy(PluginActivationDecision.Allow());
        var audit = new InMemoryHostAuditStore();
        var installations = new InMemoryPluginInstallationRepository();
        var registryReader = new StaticPluginPackageRegistryReader
        {
            Result = new PluginPackageRegistryReadResult(
                HasRegistryFile: true,
                IsValid: false,
                RegistryPath: "/tmp/registry.json",
                Registry: null,
                ErrorMessage: "registry.json parse error")
        };

        var sut = new PluginLifecycleService(
            lifecycle,
            policy,
            audit,
            installations,
            new NoOpHostUnitOfWork(),
            registryReader,
            new StaticNuGetPluginAssemblyResolver(),
            new RecordingHostApplicationEventPublisher());

        var result = await sut.InstallAsync(new InstallPluginCommand("/tmp/plugin-reg.dll", null, "tester"));

        Assert.Equal(PluginLifecycleServiceStatus.BadRequest, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Equal(0, lifecycle.InstallCallCount);
    }

    [Fact]
    public async Task InstallFromNuGetAsync_ResolvedAssembly_InvokesInstallFlow()
    {
        var lifecycle = new FakeHostPluginLifecycle
        {
            InstallResult = new VoipHost.PluginContracts.Application.Plugins.HostPluginOperationResult(
                VoipHost.PluginContracts.Application.Plugins.HostPluginOperation.Install,
                true,
                "plugin-nuget",
                null)
        };
        var policy = new StaticPluginActivationPolicy(PluginActivationDecision.Allow());
        var audit = new InMemoryHostAuditStore();
        var installations = new InMemoryPluginInstallationRepository();
        var resolver = new StaticNuGetPluginAssemblyResolver
        {
            Result = NuGetPluginAssemblyResolveResult.Success("/tmp/plugin-nuget.dll")
        };

        var sut = new PluginLifecycleService(
            lifecycle,
            policy,
            audit,
            installations,
            new NoOpHostUnitOfWork(),
            new StaticPluginPackageRegistryReader(),
            resolver,
            new RecordingHostApplicationEventPublisher());

        var result = await sut.InstallFromNuGetAsync(new InstallNuGetPluginCommand("acme.voice", "1.2.3"));

        Assert.Equal(PluginLifecycleServiceStatus.Ok, result.Status);
        Assert.Equal("/tmp/plugin-nuget.dll", lifecycle.LastInstallAssemblyPath);
    }
}

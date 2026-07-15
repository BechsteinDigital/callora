using Callora.Core.Application.Plugins;
using Callora.Core.Application.Entitlements;
using Callora.Core.Application.Extensions;
using Callora.Core.Application.Lifecycle;
using Callora.Core.Application.Policies;
using Callora.Core.Domain.Extensions;
using Callora.Core.Domain.Plugins;
using Callora.Core.Infrastructure.Extensions;
using Callora.Core.Tests.Support;
using System.Reflection;
using Callora.Host.PluginContracts.Application.Plugins;

namespace Callora.Core.Tests.Application.Lifecycle;

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
            new InMemoryPluginEntitlementStore(new BackendHostOptions()),
            audit,
            installations,
            unitOfWork,
            registryReader,
            new StaticPluginPackageSignatureVerifier(),
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
            InstallResult = new Callora.Host.PluginContracts.Application.Plugins.HostPluginOperationResult(
                Callora.Host.PluginContracts.Application.Plugins.HostPluginOperation.Install,
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
            new InMemoryPluginEntitlementStore(new BackendHostOptions()),
            audit,
            installations,
            unitOfWork,
            registryReader,
            new StaticPluginPackageSignatureVerifier(),
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
            ActivateResult = new Callora.Host.PluginContracts.Application.Plugins.HostPluginOperationResult(
                Callora.Host.PluginContracts.Application.Plugins.HostPluginOperation.Activate,
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
            new InMemoryPluginEntitlementStore(new BackendHostOptions()),
            audit,
            installations,
            new NoOpHostUnitOfWork(),
            new StaticPluginPackageRegistryReader(),
            new StaticPluginPackageSignatureVerifier(),
            new StaticNuGetPluginAssemblyResolver(),
            new RecordingHostApplicationEventPublisher());

        var result = await sut.ActivateAsync(new PluginLifecycleCommand("plugin-x", "tester"));

        Assert.Equal(PluginLifecycleServiceStatus.Ok, result.Status);
        var installation = await installations.GetByPluginIdAsync("plugin-x");
        Assert.NotNull(installation);
        Assert.Equal(PluginInstallationState.Active, installation.State);
    }

    [Fact]
    public async Task ActivateAsync_WorkspaceScope_SetsWorkspaceActivationWithoutRuntimeLifecycleCall()
    {
        var lifecycle = new FakeHostPluginLifecycle
        {
            ActivateResult = new Callora.Host.PluginContracts.Application.Plugins.HostPluginOperationResult(
                Callora.Host.PluginContracts.Application.Plugins.HostPluginOperation.Activate,
                true,
                "plugin-x",
                null)
        };
        var options = new BackendHostOptions
        {
            RequireAllowlistForActivation = false
        };
        var entitlementStore = new InMemoryPluginEntitlementStore(options);
        var activationStore = new Callora.Core.Application.Plugins.InMemoryWorkspacePluginActivationStore();
        var policy = new AllowlistPluginActivationPolicy(options);
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
            entitlementStore,
            audit,
            installations,
            new NoOpHostUnitOfWork(),
            new StaticPluginPackageRegistryReader(),
            new StaticPluginPackageSignatureVerifier(),
            new StaticNuGetPluginAssemblyResolver(),
            new RecordingHostApplicationEventPublisher(),
            workspaceActivationStore: activationStore);

        var result = await sut.ActivateAsync(new PluginLifecycleCommand("plugin-x", "tester", "workspace-a"));

        Assert.Equal(PluginLifecycleServiceStatus.Ok, result.Status);
        Assert.Contains("plugin-x", await activationStore.ListActivePluginIdsAsync("workspace-a"));
        Assert.Equal(0, lifecycle.ActivateCallCount);
    }

    [Fact]
    public async Task ActivateAsync_WorkspaceScope_ReleasesWorkspaceLifecycleLockEntryAfterCall()
    {
        var lifecycle = new FakeHostPluginLifecycle();
        var options = new BackendHostOptions
        {
            RequireAllowlistForActivation = false
        };
        var entitlementStore = new InMemoryPluginEntitlementStore(options);
        var policy = new AllowlistPluginActivationPolicy(options);
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
            entitlementStore,
            new InMemoryHostAuditStore(),
            installations,
            new NoOpHostUnitOfWork(),
            new StaticPluginPackageRegistryReader(),
            new StaticPluginPackageSignatureVerifier(),
            new StaticNuGetPluginAssemblyResolver(),
            new RecordingHostApplicationEventPublisher());

        var result = await sut.ActivateAsync(new PluginLifecycleCommand("plugin-x", "tester", "workspace-a"));

        Assert.Equal(PluginLifecycleServiceStatus.Ok, result.Status);
        var lockField = typeof(PluginLifecycleService).GetField("_workspaceLifecycleLocks", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(lockField);
        var lockMap = lockField!.GetValue(sut);
        Assert.NotNull(lockMap);
        var countProperty = lockMap!.GetType().GetProperty("Count");
        Assert.NotNull(countProperty);
        var count = (int)countProperty!.GetValue(lockMap)!;
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task DeactivateAsync_WorkspaceScope_ClearsWorkspaceActivationWithoutRuntimeLifecycleCall()
    {
        var lifecycle = new FakeHostPluginLifecycle
        {
            ActivateResult = new Callora.Host.PluginContracts.Application.Plugins.HostPluginOperationResult(
                Callora.Host.PluginContracts.Application.Plugins.HostPluginOperation.Activate,
                true,
                "plugin-x",
                null)
        };
        var options = new BackendHostOptions
        {
            RequireAllowlistForActivation = false
        };
        var entitlementStore = new InMemoryPluginEntitlementStore(options);
        var activationStore = new Callora.Core.Application.Plugins.InMemoryWorkspacePluginActivationStore();
        await entitlementStore.SetEntitledAsync("plugin-x", true, "workspace-a");
        var policy = new AllowlistPluginActivationPolicy(options);
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
            entitlementStore,
            audit,
            installations,
            new NoOpHostUnitOfWork(),
            new StaticPluginPackageRegistryReader(),
            new StaticPluginPackageSignatureVerifier(),
            new StaticNuGetPluginAssemblyResolver(),
            new RecordingHostApplicationEventPublisher(),
            workspaceActivationStore: activationStore);

        var result = await sut.DeactivateAsync(new PluginLifecycleCommand("plugin-x", "tester", "workspace-a"));

        Assert.Equal(PluginLifecycleServiceStatus.Ok, result.Status);
        Assert.DoesNotContain("plugin-x", await activationStore.ListActivePluginIdsAsync("workspace-a"));
        Assert.Equal(0, lifecycle.DeactivateCallCount);
    }

    [Fact]
    public async Task ActivateAsync_WorkspaceScope_IsIdempotent()
    {
        var lifecycle = new FakeHostPluginLifecycle();
        var options = new BackendHostOptions
        {
            RequireAllowlistForActivation = false
        };
        var entitlementStore = new InMemoryPluginEntitlementStore(options);
        var activationStore = new Callora.Core.Application.Plugins.InMemoryWorkspacePluginActivationStore();
        var policy = new AllowlistPluginActivationPolicy(options);
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
            entitlementStore,
            audit,
            installations,
            new NoOpHostUnitOfWork(),
            new StaticPluginPackageRegistryReader(),
            new StaticPluginPackageSignatureVerifier(),
            new StaticNuGetPluginAssemblyResolver(),
            new RecordingHostApplicationEventPublisher(),
            workspaceActivationStore: activationStore);

        var first = await sut.ActivateAsync(new PluginLifecycleCommand("plugin-x", "tester", "workspace-a"));
        var second = await sut.ActivateAsync(new PluginLifecycleCommand("plugin-x", "tester", "workspace-a"));

        Assert.Equal(PluginLifecycleServiceStatus.Ok, first.Status);
        Assert.Equal(PluginLifecycleServiceStatus.Ok, second.Status);
        Assert.Contains("plugin-x", await activationStore.ListActivePluginIdsAsync("workspace-a"));
        Assert.Equal(0, lifecycle.ActivateCallCount);
    }

    [Fact]
    public async Task DeactivateAsync_WorkspaceScope_IsIdempotent()
    {
        var lifecycle = new FakeHostPluginLifecycle();
        var options = new BackendHostOptions
        {
            RequireAllowlistForActivation = false
        };
        var entitlementStore = new InMemoryPluginEntitlementStore(options);
        var activationStore = new Callora.Core.Application.Plugins.InMemoryWorkspacePluginActivationStore();
        await entitlementStore.SetEntitledAsync("plugin-x", true, "workspace-a");
        var policy = new AllowlistPluginActivationPolicy(options);
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
            entitlementStore,
            audit,
            installations,
            new NoOpHostUnitOfWork(),
            new StaticPluginPackageRegistryReader(),
            new StaticPluginPackageSignatureVerifier(),
            new StaticNuGetPluginAssemblyResolver(),
            new RecordingHostApplicationEventPublisher(),
            workspaceActivationStore: activationStore);

        var first = await sut.DeactivateAsync(new PluginLifecycleCommand("plugin-x", "tester", "workspace-a"));
        var second = await sut.DeactivateAsync(new PluginLifecycleCommand("plugin-x", "tester", "workspace-a"));

        Assert.Equal(PluginLifecycleServiceStatus.Ok, first.Status);
        Assert.Equal(PluginLifecycleServiceStatus.Ok, second.Status);
        Assert.DoesNotContain("plugin-x", await activationStore.ListActivePluginIdsAsync("workspace-a"));
        Assert.Equal(0, lifecycle.DeactivateCallCount);
    }

    [Fact]
    public async Task ActivateAsync_WorkspaceScope_ParallelCalls_LeaveConsistentState()
    {
        var lifecycle = new FakeHostPluginLifecycle();
        var options = new BackendHostOptions
        {
            RequireAllowlistForActivation = false
        };
        var entitlementStore = new InMemoryPluginEntitlementStore(options);
        var activationStore = new Callora.Core.Application.Plugins.InMemoryWorkspacePluginActivationStore();
        var policy = new AllowlistPluginActivationPolicy(options);
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
            entitlementStore,
            audit,
            installations,
            new NoOpHostUnitOfWork(),
            new StaticPluginPackageRegistryReader(),
            new StaticPluginPackageSignatureVerifier(),
            new StaticNuGetPluginAssemblyResolver(),
            new RecordingHostApplicationEventPublisher(),
            workspaceActivationStore: activationStore);

        await Task.WhenAll(
            sut.ActivateAsync(new PluginLifecycleCommand("plugin-x", "tester-a", "workspace-a")),
            sut.ActivateAsync(new PluginLifecycleCommand("plugin-x", "tester-b", "workspace-a")),
            sut.ActivateAsync(new PluginLifecycleCommand("plugin-x", "tester-c", "workspace-a")));

        Assert.Contains("plugin-x", await activationStore.ListActivePluginIdsAsync("workspace-a"));
        Assert.Equal(0, lifecycle.ActivateCallCount);
    }

    [Fact]
    public async Task DeactivateAsync_WorkspaceScope_ParallelCalls_LeaveConsistentState()
    {
        var lifecycle = new FakeHostPluginLifecycle();
        var options = new BackendHostOptions
        {
            RequireAllowlistForActivation = false
        };
        var entitlementStore = new InMemoryPluginEntitlementStore(options);
        var activationStore = new Callora.Core.Application.Plugins.InMemoryWorkspacePluginActivationStore();
        await entitlementStore.SetEntitledAsync("plugin-x", true, "workspace-a");
        var policy = new AllowlistPluginActivationPolicy(options);
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
            entitlementStore,
            audit,
            installations,
            new NoOpHostUnitOfWork(),
            new StaticPluginPackageRegistryReader(),
            new StaticPluginPackageSignatureVerifier(),
            new StaticNuGetPluginAssemblyResolver(),
            new RecordingHostApplicationEventPublisher(),
            workspaceActivationStore: activationStore);

        await Task.WhenAll(
            sut.DeactivateAsync(new PluginLifecycleCommand("plugin-x", "tester-a", "workspace-a")),
            sut.DeactivateAsync(new PluginLifecycleCommand("plugin-x", "tester-b", "workspace-a")),
            sut.DeactivateAsync(new PluginLifecycleCommand("plugin-x", "tester-c", "workspace-a")));

        Assert.DoesNotContain("plugin-x", await activationStore.ListActivePluginIdsAsync("workspace-a"));
        Assert.Equal(0, lifecycle.DeactivateCallCount);
    }

    [Fact]
    public async Task ActivateAsync_WorkspaceScope_MissingInstallation_ReturnsBadRequest()
    {
        var lifecycle = new FakeHostPluginLifecycle();
        var options = new BackendHostOptions
        {
            RequireAllowlistForActivation = false
        };
        var entitlementStore = new InMemoryPluginEntitlementStore(options);
        var policy = new AllowlistPluginActivationPolicy(options);
        var audit = new InMemoryHostAuditStore();
        var installations = new InMemoryPluginInstallationRepository();

        var sut = new PluginLifecycleService(
            lifecycle,
            policy,
            entitlementStore,
            audit,
            installations,
            new NoOpHostUnitOfWork(),
            new StaticPluginPackageRegistryReader(),
            new StaticPluginPackageSignatureVerifier(),
            new StaticNuGetPluginAssemblyResolver(),
            new RecordingHostApplicationEventPublisher());

        var result = await sut.ActivateAsync(new PluginLifecycleCommand("plugin-x", "tester", "workspace-a"));

        Assert.Equal(PluginLifecycleServiceStatus.BadRequest, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Contains("not installed", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, lifecycle.ActivateCallCount);
    }

    [Fact]
    public async Task InstallAsync_UsesRegistryEntryType_WhenRequestEntryTypeIsNull()
    {
        var lifecycle = new FakeHostPluginLifecycle
        {
            InstallResult = new Callora.Host.PluginContracts.Application.Plugins.HostPluginOperationResult(
                Callora.Host.PluginContracts.Application.Plugins.HostPluginOperation.Install,
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
                    "v1",
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
            new InMemoryPluginEntitlementStore(new BackendHostOptions()),
            audit,
            installations,
            new NoOpHostUnitOfWork(),
            registryReader,
            new StaticPluginPackageSignatureVerifier(),
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
                ErrorMessage: "registry.json parse error",
                ErrorCode: PluginRegistryErrorCodes.ContractVersionUnsupported)
        };

        var sut = new PluginLifecycleService(
            lifecycle,
            policy,
            new InMemoryPluginEntitlementStore(new BackendHostOptions()),
            audit,
            installations,
            new NoOpHostUnitOfWork(),
            registryReader,
            new StaticPluginPackageSignatureVerifier(),
            new StaticNuGetPluginAssemblyResolver(),
            new RecordingHostApplicationEventPublisher());

        var result = await sut.InstallAsync(new InstallPluginCommand("/tmp/plugin-reg.dll", null, "tester"));

        Assert.Equal(PluginLifecycleServiceStatus.BadRequest, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Equal(PluginLifecycleErrorCodes.PluginContractVersionUnsupported, result.ErrorCode);
        Assert.Equal(0, lifecycle.InstallCallCount);

        var entries = await audit.GetRecentAsync();
        Assert.Single(entries);
        Assert.NotNull(entries[0].Metadata);
        Assert.Equal("registry.validation", entries[0].Metadata!["gateType"]);
        Assert.Equal(PluginLifecycleErrorCodes.PluginContractVersionUnsupported, entries[0].Metadata!["reasonCode"]);
    }

    [Fact]
    public async Task InstallAsync_RemovedContractRegistry_ReturnsBadRequest_WithRemovedErrorCode()
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
                ErrorMessage: "removed",
                ErrorCode: PluginRegistryErrorCodes.ContractVersionRemoved)
        };

        var sut = new PluginLifecycleService(
            lifecycle,
            policy,
            new InMemoryPluginEntitlementStore(new BackendHostOptions()),
            audit,
            installations,
            new NoOpHostUnitOfWork(),
            registryReader,
            new StaticPluginPackageSignatureVerifier(),
            new StaticNuGetPluginAssemblyResolver(),
            new RecordingHostApplicationEventPublisher());

        var result = await sut.InstallAsync(new InstallPluginCommand("/tmp/plugin-reg.dll", null, "tester"));

        Assert.Equal(PluginLifecycleServiceStatus.BadRequest, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Equal(PluginLifecycleErrorCodes.PluginContractVersionRemoved, result.ErrorCode);
        Assert.Equal(0, lifecycle.InstallCallCount);
    }

    [Fact]
    public async Task InstallAsync_DeprecatedContractRegistry_ReturnsOk_WithWarningCode()
    {
        var lifecycle = new FakeHostPluginLifecycle
        {
            InstallResult = new Callora.Host.PluginContracts.Application.Plugins.HostPluginOperationResult(
                Callora.Host.PluginContracts.Application.Plugins.HostPluginOperation.Install,
                true,
                "plugin-deprecated",
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
                    "v1",
                    "1.0",
                    "Deprecated Plugin",
                    "plugin-deprecated",
                    "1.0.0",
                    "plugin-deprecated.dll",
                    "Plugins.Entry",
                    [],
                    new Dictionary<string, string>()),
                WarningMessage: "deprecated",
                WarningCode: PluginRegistryErrorCodes.ContractVersionDeprecated)
        };

        var sut = new PluginLifecycleService(
            lifecycle,
            policy,
            new InMemoryPluginEntitlementStore(new BackendHostOptions()),
            audit,
            installations,
            new NoOpHostUnitOfWork(),
            registryReader,
            new StaticPluginPackageSignatureVerifier(),
            new StaticNuGetPluginAssemblyResolver(),
            new RecordingHostApplicationEventPublisher());

        var result = await sut.InstallAsync(new InstallPluginCommand("/tmp/plugin-deprecated.dll", null, "tester"));

        Assert.Equal(PluginLifecycleServiceStatus.Ok, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal(PluginLifecycleWarningCodes.PluginContractVersionDeprecated, result.WarningCode);
        Assert.Equal("deprecated", result.WarningMessage);
    }

    [Fact]
    public async Task InstallAsync_RegistryAssemblyMismatch_ReturnsBadRequest_AndWritesStructuredGateMetadata()
    {
        var lifecycle = new FakeHostPluginLifecycle();
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
                    "v2",
                    "1.0",
                    "Plugin",
                    "plugin-mismatch",
                    "1.0.0",
                    "plugin-expected.dll",
                    "Plugins.Entry",
                    [],
                    new Dictionary<string, string>()))
        };

        var sut = new PluginLifecycleService(
            lifecycle,
            policy,
            new InMemoryPluginEntitlementStore(new BackendHostOptions()),
            audit,
            installations,
            new NoOpHostUnitOfWork(),
            registryReader,
            new StaticPluginPackageSignatureVerifier(),
            new StaticNuGetPluginAssemblyResolver(),
            new RecordingHostApplicationEventPublisher());

        var result = await sut.InstallAsync(new InstallPluginCommand("/tmp/plugin-actual.dll", null, "tester"));

        Assert.Equal(PluginLifecycleServiceStatus.BadRequest, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Equal(PluginLifecycleErrorCodes.PluginAssemblyFileNameMismatch, result.ErrorCode);
        Assert.Equal(0, lifecycle.InstallCallCount);

        var entries = await audit.GetRecentAsync();
        Assert.Single(entries);
        Assert.NotNull(entries[0].Metadata);
        Assert.Equal("registry.assembly_match", entries[0].Metadata!["gateType"]);
        Assert.Equal(PluginLifecycleErrorCodes.PluginAssemblyFileNameMismatch, entries[0].Metadata!["reasonCode"]);
    }

    [Fact]
    public async Task InstallAsync_RegistryExtensions_DoNotBlockInstallInCodeFirstMode()
    {
        var lifecycle = new FakeHostPluginLifecycle
        {
            InstallResult = new Callora.Host.PluginContracts.Application.Plugins.HostPluginOperationResult(
                Callora.Host.PluginContracts.Application.Plugins.HostPluginOperation.Install,
                true,
                "plugin-with-extension",
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
                    "v2",
                    "1.0",
                    "Plugin",
                    "plugin-with-extension",
                    "1.0.0",
                    "plugin-with-extension.dll",
                    "Plugins.Entry",
                    [],
                    new Dictionary<string, string>(),
                    [
                        new PluginPackageExtensionRegistration(
                            "workspace.unknown.point",
                            ExtensionSurface.Workspace)
                    ]))
        };
        var extensionRegistry = new InMemoryExtensionPointRegistryStore();

        var sut = new PluginLifecycleService(
            lifecycle,
            policy,
            new InMemoryPluginEntitlementStore(new BackendHostOptions()),
            audit,
            installations,
            new NoOpHostUnitOfWork(),
            registryReader,
            new StaticPluginPackageSignatureVerifier(),
            new StaticNuGetPluginAssemblyResolver(),
            extensionRegistry,
            new RecordingHostApplicationEventPublisher());

        var result = await sut.InstallAsync(new InstallPluginCommand("/tmp/plugin-with-extension.dll", null, "tester"));

        Assert.Equal(PluginLifecycleServiceStatus.Ok, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal("plugin-with-extension", result.PluginId);
        Assert.Equal(1, lifecycle.InstallCallCount);

        var entries = await audit.GetRecentAsync();
        Assert.Single(entries);
        Assert.True(entries[0].IsSuccess);
    }

    [Fact]
    public async Task InstallAsync_RegistryScopeMismatch_DoesNotBlockInstallInCodeFirstMode()
    {
        var lifecycle = new FakeHostPluginLifecycle
        {
            InstallResult = new Callora.Host.PluginContracts.Application.Plugins.HostPluginOperationResult(
                Callora.Host.PluginContracts.Application.Plugins.HostPluginOperation.Install,
                true,
                "plugin-scope",
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
                    "v2",
                    "1.0",
                    "Plugin",
                    "plugin-scope",
                    "1.0.0",
                    "plugin-scope.dll",
                    "Plugins.Entry",
                    [],
                    new Dictionary<string, string>(),
                    [
                        new PluginPackageExtensionRegistration(
                            "admin.dashboard.header",
                            ExtensionSurface.Admin)
                    ]))
        };
        var extensionRegistry = new InMemoryExtensionPointRegistryStore();
        await extensionRegistry.ReplaceAsync(
            "1.0",
            [new ExtensionPointDefinition("admin.dashboard.header", ExtensionSurface.Admin, "extensions.admin.write")]);

        var sut = new PluginLifecycleService(
            lifecycle,
            policy,
            new InMemoryPluginEntitlementStore(new BackendHostOptions()),
            audit,
            installations,
            new NoOpHostUnitOfWork(),
            registryReader,
            new StaticPluginPackageSignatureVerifier(),
            new StaticNuGetPluginAssemblyResolver(),
            extensionRegistry,
            new RecordingHostApplicationEventPublisher());

        var result = await sut.InstallAsync(new InstallPluginCommand("/tmp/plugin-scope.dll", null, "tester"));

        Assert.Equal(PluginLifecycleServiceStatus.Ok, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal("plugin-scope", result.PluginId);
        Assert.Equal(1, lifecycle.InstallCallCount);

        var entries = await audit.GetRecentAsync();
        Assert.Single(entries);
        Assert.True(entries[0].IsSuccess);
    }

    [Fact]
    public async Task ActivateAsync_RuntimeExtensionPointUnknown_ReturnsBadRequest_AndRollsBackActivation()
    {
        var lifecycle = new FakeHostPluginLifecycle
        {
            ActivateResult = new Callora.Host.PluginContracts.Application.Plugins.HostPluginOperationResult(
                Callora.Host.PluginContracts.Application.Plugins.HostPluginOperation.Activate,
                true,
                "plugin-x",
                null)
        };
        var policy = new StaticPluginActivationPolicy(PluginActivationDecision.Allow());
        var installations = new InMemoryPluginInstallationRepository();
        await installations.AddAsync(PluginInstallation.CreateInstalled(
            "plugin-x",
            "Plugin X",
            "/tmp/plugin-x.dll",
            null,
            DateTimeOffset.UtcNow));
        var extensionRegistry = new InMemoryExtensionPointRegistryStore();
        var extensionStore = new InMemoryPluginExtensionRegistrationStore();
        var pluginCatalog = new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>
        {
            [typeof(IHostPluginExtensionContributor)] =
            [
                new StaticHostPluginExtensionContributor(
                    "plugin-x",
                    ["workspace.navigation"],
                    [new HostPluginExtensionRegistration("workspace.navigation.unknown", "workspace")])
            ]
        });
        var sut = new PluginLifecycleService(
            lifecycle,
            policy,
            new InMemoryPluginEntitlementStore(new BackendHostOptions()),
            new InMemoryHostAuditStore(),
            installations,
            new NoOpHostUnitOfWork(),
            new StaticPluginPackageRegistryReader(),
            new StaticPluginPackageSignatureVerifier(),
            new StaticNuGetPluginAssemblyResolver(),
            extensionRegistry,
            extensionStore,
            new RecordingHostApplicationEventPublisher(),
            new InMemoryWorkspaceManagementStore(),
            pluginCatalog);

        var result = await sut.ActivateAsync(new PluginLifecycleCommand("plugin-x", "tester"));

        Assert.Equal(PluginLifecycleServiceStatus.BadRequest, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Equal(PluginLifecycleErrorCodes.PluginExtensionPointUnknown, result.ErrorCode);
        Assert.Equal(1, lifecycle.ActivateCallCount);
        Assert.Equal(1, lifecycle.DeactivateCallCount);
    }

    [Fact]
    public async Task ActivateAsync_RuntimeExtensionRegistrations_Valid_AreStored()
    {
        var lifecycle = new FakeHostPluginLifecycle
        {
            ActivateResult = new Callora.Host.PluginContracts.Application.Plugins.HostPluginOperationResult(
                Callora.Host.PluginContracts.Application.Plugins.HostPluginOperation.Activate,
                true,
                "plugin-x",
                null)
        };
        var policy = new StaticPluginActivationPolicy(PluginActivationDecision.Allow());
        var installations = new InMemoryPluginInstallationRepository();
        await installations.AddAsync(PluginInstallation.CreateInstalled(
            "plugin-x",
            "Plugin X",
            "/tmp/plugin-x.dll",
            null,
            DateTimeOffset.UtcNow));
        var extensionRegistry = new InMemoryExtensionPointRegistryStore();
        var extensionStore = new InMemoryPluginExtensionRegistrationStore();
        var pluginCatalog = new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>
        {
            [typeof(IHostPluginExtensionContributor)] =
            [
                new StaticHostPluginExtensionContributor(
                    "plugin-x",
                    ["workspace.navigation"],
                    [new HostPluginExtensionRegistration("workspace.navigation.main", "workspace")])
            ]
        });
        var sut = new PluginLifecycleService(
            lifecycle,
            policy,
            new InMemoryPluginEntitlementStore(new BackendHostOptions()),
            new InMemoryHostAuditStore(),
            installations,
            new NoOpHostUnitOfWork(),
            new StaticPluginPackageRegistryReader(),
            new StaticPluginPackageSignatureVerifier(),
            new StaticNuGetPluginAssemblyResolver(),
            extensionRegistry,
            extensionStore,
            new RecordingHostApplicationEventPublisher(),
            new InMemoryWorkspaceManagementStore(),
            pluginCatalog);

        var result = await sut.ActivateAsync(new PluginLifecycleCommand("plugin-x", "tester"));

        Assert.Equal(PluginLifecycleServiceStatus.Ok, result.Status);
        Assert.True(result.IsSuccess);

        var registrations = await extensionStore.ListAsync();
        Assert.Single(registrations);
        Assert.Equal("plugin-x", registrations[0].PluginId);
        Assert.Single(registrations[0].ExtensionRegistrations);
        Assert.Equal("workspace.navigation.main", registrations[0].ExtensionRegistrations[0].ExtensionPointId);
    }

    [Fact]
    public async Task InstallAsync_UnsignedPackage_ReturnsBadRequest_AndWritesSignatureReasonCode()
    {
        var lifecycle = new FakeHostPluginLifecycle();
        var policy = new StaticPluginActivationPolicy(PluginActivationDecision.Allow());
        var audit = new InMemoryHostAuditStore();
        var installations = new InMemoryPluginInstallationRepository();
        var signatureVerifier = new StaticPluginPackageSignatureVerifier
        {
            Result = new PluginPackageSignatureVerificationResult(
                IsValid: false,
                ErrorMessage: "unsigned",
                ErrorCode: PluginPackageSignatureErrorCodes.UnsignedPackage)
        };

        var sut = new PluginLifecycleService(
            lifecycle,
            policy,
            new InMemoryPluginEntitlementStore(new BackendHostOptions()),
            audit,
            installations,
            new NoOpHostUnitOfWork(),
            new StaticPluginPackageRegistryReader(),
            signatureVerifier,
            new StaticNuGetPluginAssemblyResolver(),
            new RecordingHostApplicationEventPublisher());

        var result = await sut.InstallAsync(new InstallPluginCommand("/tmp/plugin-unsigned.dll", null, "tester"));

        Assert.Equal(PluginLifecycleServiceStatus.BadRequest, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Equal(PluginLifecycleErrorCodes.PluginPackageUnsigned, result.ErrorCode);
        Assert.Equal(0, lifecycle.InstallCallCount);

        var entries = await audit.GetRecentAsync();
        Assert.Single(entries);
        Assert.Equal("plugin.install", entries[0].Action);
        Assert.False(entries[0].IsSuccess);
        Assert.NotNull(entries[0].Metadata);
        Assert.Equal("signature.validation", entries[0].Metadata!["gateType"]);
        Assert.Equal(PluginLifecycleErrorCodes.PluginPackageUnsigned, entries[0].Metadata!["reasonCode"]);
        Assert.Equal(PluginLifecycleErrorCodes.PluginPackageUnsigned, entries[0].Metadata!["signatureErrorCode"]);
    }

    [Fact]
    public async Task InstallAsync_InvalidSignature_ReturnsBadRequest_AndWritesSignatureReasonCode()
    {
        var lifecycle = new FakeHostPluginLifecycle();
        var policy = new StaticPluginActivationPolicy(PluginActivationDecision.Allow());
        var audit = new InMemoryHostAuditStore();
        var installations = new InMemoryPluginInstallationRepository();
        var signatureVerifier = new StaticPluginPackageSignatureVerifier
        {
            Result = new PluginPackageSignatureVerificationResult(
                IsValid: false,
                ErrorMessage: "invalid signature",
                ErrorCode: PluginPackageSignatureErrorCodes.InvalidSignature)
        };

        var sut = new PluginLifecycleService(
            lifecycle,
            policy,
            new InMemoryPluginEntitlementStore(new BackendHostOptions()),
            audit,
            installations,
            new NoOpHostUnitOfWork(),
            new StaticPluginPackageRegistryReader(),
            signatureVerifier,
            new StaticNuGetPluginAssemblyResolver(),
            new RecordingHostApplicationEventPublisher());

        var result = await sut.InstallAsync(new InstallPluginCommand("/tmp/plugin-invalid.dll", null, "tester"));

        Assert.Equal(PluginLifecycleServiceStatus.BadRequest, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Equal(PluginLifecycleErrorCodes.PluginPackageSignatureInvalid, result.ErrorCode);
        Assert.Equal(0, lifecycle.InstallCallCount);

        var entries = await audit.GetRecentAsync();
        Assert.Single(entries);
        Assert.Equal("plugin.install", entries[0].Action);
        Assert.False(entries[0].IsSuccess);
        Assert.NotNull(entries[0].Metadata);
        Assert.Equal("signature.validation", entries[0].Metadata!["gateType"]);
        Assert.Equal(PluginLifecycleErrorCodes.PluginPackageSignatureInvalid, entries[0].Metadata!["reasonCode"]);
        Assert.Equal(PluginLifecycleErrorCodes.PluginPackageSignatureInvalid, entries[0].Metadata!["signatureErrorCode"]);
    }

    [Fact]
    public async Task InstallAsync_UntrustedSigner_ReturnsBadRequest_AndWritesSignatureReasonCode()
    {
        var lifecycle = new FakeHostPluginLifecycle();
        var policy = new StaticPluginActivationPolicy(PluginActivationDecision.Allow());
        var audit = new InMemoryHostAuditStore();
        var installations = new InMemoryPluginInstallationRepository();
        var signatureVerifier = new StaticPluginPackageSignatureVerifier
        {
            Result = new PluginPackageSignatureVerificationResult(
                IsValid: false,
                ErrorMessage: "untrusted signer",
                ErrorCode: PluginPackageSignatureErrorCodes.UntrustedSigner)
        };

        var sut = new PluginLifecycleService(
            lifecycle,
            policy,
            new InMemoryPluginEntitlementStore(new BackendHostOptions()),
            audit,
            installations,
            new NoOpHostUnitOfWork(),
            new StaticPluginPackageRegistryReader(),
            signatureVerifier,
            new StaticNuGetPluginAssemblyResolver(),
            new RecordingHostApplicationEventPublisher());

        var result = await sut.InstallAsync(new InstallPluginCommand("/tmp/plugin-untrusted.dll", null, "tester"));

        Assert.Equal(PluginLifecycleServiceStatus.BadRequest, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Equal(PluginLifecycleErrorCodes.PluginPackageSignerUntrusted, result.ErrorCode);
        Assert.Equal(0, lifecycle.InstallCallCount);

        var entries = await audit.GetRecentAsync();
        Assert.Single(entries);
        Assert.Equal("plugin.install", entries[0].Action);
        Assert.False(entries[0].IsSuccess);
        Assert.NotNull(entries[0].Metadata);
        Assert.Equal("signature.validation", entries[0].Metadata!["gateType"]);
        Assert.Equal(PluginLifecycleErrorCodes.PluginPackageSignerUntrusted, entries[0].Metadata!["reasonCode"]);
        Assert.Equal(PluginLifecycleErrorCodes.PluginPackageSignerUntrusted, entries[0].Metadata!["signatureErrorCode"]);
    }

    [Fact]
    public async Task InstallFromNuGetAsync_ResolvedAssembly_InvokesInstallFlow()
    {
        var lifecycle = new FakeHostPluginLifecycle
        {
            InstallResult = new Callora.Host.PluginContracts.Application.Plugins.HostPluginOperationResult(
                Callora.Host.PluginContracts.Application.Plugins.HostPluginOperation.Install,
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
            new InMemoryPluginEntitlementStore(new BackendHostOptions()),
            audit,
            installations,
            new NoOpHostUnitOfWork(),
            new StaticPluginPackageRegistryReader(),
            new StaticPluginPackageSignatureVerifier(),
            resolver,
            new RecordingHostApplicationEventPublisher());

        var result = await sut.InstallFromNuGetAsync(new InstallNuGetPluginCommand("acme.voice", "1.2.3"));

        Assert.Equal(PluginLifecycleServiceStatus.Ok, result.Status);
        Assert.Equal("/tmp/plugin-nuget.dll", lifecycle.LastInstallAssemblyPath);
    }

    [Fact]
    public async Task UpdateFromNuGetAsync_FailedUpdate_TriggersAutomaticRollback()
    {
        var lifecycle = new FakeHostPluginLifecycle
        {
            InstallHandler = (assemblyPath, _) =>
                assemblyPath == "/tmp/plugin-new.dll"
                    ? new Callora.Host.PluginContracts.Application.Plugins.HostPluginOperationResult(
                        Callora.Host.PluginContracts.Application.Plugins.HostPluginOperation.Install,
                        false,
                        "plugin-x",
                        "new install failed")
                    : new Callora.Host.PluginContracts.Application.Plugins.HostPluginOperationResult(
                        Callora.Host.PluginContracts.Application.Plugins.HostPluginOperation.Install,
                        true,
                        "plugin-x",
                        null)
        };
        var installations = new InMemoryPluginInstallationRepository();
        await installations.AddAsync(PluginInstallation.CreateInstalled(
            "plugin-x",
            "Plugin X",
            "/tmp/plugin-old.dll",
            "Plugin.Entry",
            DateTimeOffset.UtcNow));
        var existing = await installations.GetByPluginIdAsync("plugin-x");
        Assert.NotNull(existing);
        existing!.MarkActivated(DateTimeOffset.UtcNow);

        var resolver = new StaticNuGetPluginAssemblyResolver
        {
            Result = NuGetPluginAssemblyResolveResult.Success("/tmp/plugin-new.dll")
        };
        var audit = new InMemoryHostAuditStore();
        var sut = new PluginLifecycleService(
            lifecycle,
            new StaticPluginActivationPolicy(PluginActivationDecision.Allow()),
            new InMemoryPluginEntitlementStore(new BackendHostOptions()),
            audit,
            installations,
            new NoOpHostUnitOfWork(),
            new StaticPluginPackageRegistryReader(),
            new StaticPluginPackageSignatureVerifier(),
            resolver,
            new RecordingHostApplicationEventPublisher());

        var result = await sut.UpdateFromNuGetAsync(
            new UpdateNuGetPluginCommand(
                "plugin-x",
                "acme.voice",
                "2.0.0",
                RequestedBy: "tester"));

        Assert.Equal(PluginLifecycleServiceStatus.BadRequest, result.Status);
        Assert.False(result.IsSuccess);
        Assert.Contains("Rollback restored previous version", result.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(2, lifecycle.InstallCallCount);
        Assert.Equal("/tmp/plugin-new.dll", lifecycle.InstalledAssemblyPaths[0]);
        Assert.Equal("/tmp/plugin-old.dll", lifecycle.InstalledAssemblyPaths[1]);
        Assert.Equal(1, lifecycle.DeactivateCallCount);
        Assert.Equal(1, lifecycle.UninstallCallCount);
        Assert.Equal(1, lifecycle.ActivateCallCount);

        var restored = await installations.GetByPluginIdAsync("plugin-x");
        Assert.NotNull(restored);
        Assert.Equal("/tmp/plugin-old.dll", restored!.AssemblyPath);
        Assert.Equal(PluginInstallationState.Active, restored.State);

        var entries = await audit.GetRecentAsync(20);
        Assert.Contains(entries, x => x.Action == "plugin.rollback" && x.IsSuccess);
        Assert.Contains(entries, x => x.Action == "plugin.update" && !x.IsSuccess);
    }

    [Fact]
    public async Task UpdateFromLocalAsync_ResolvedAssembly_UpdatesAndReactivatesPlugin()
    {
        var lifecycle = new FakeHostPluginLifecycle
        {
            InstallResult = new Callora.Host.PluginContracts.Application.Plugins.HostPluginOperationResult(
                Callora.Host.PluginContracts.Application.Plugins.HostPluginOperation.Install,
                true,
                "plugin-local",
                null)
        };
        var installations = new InMemoryPluginInstallationRepository();
        await installations.AddAsync(PluginInstallation.CreateInstalled(
            "plugin-local",
            "Plugin Local",
            "/tmp/plugin-old.dll",
            "Plugin.Entry",
            DateTimeOffset.UtcNow));

        var existing = await installations.GetByPluginIdAsync("plugin-local");
        Assert.NotNull(existing);
        existing!.MarkActivated(DateTimeOffset.UtcNow);

        var localResolver = new StaticLocalPluginInstallSourceResolver
        {
            Result = new LocalPluginInstallSourceResolveResult(
                IsSuccess: true,
                PluginId: "plugin-local",
                AssemblyPath: "/tmp/plugin-local-new.dll",
                EntryTypeName: "Plugin.Entry.New",
                UsedBuild: true,
                Message: "local source resolved")
        };

        var sut = new PluginLifecycleService(
            lifecycle,
            new StaticPluginActivationPolicy(PluginActivationDecision.Allow()),
            new InMemoryPluginEntitlementStore(new BackendHostOptions()),
            new InMemoryHostAuditStore(),
            installations,
            new NoOpHostUnitOfWork(),
            new StaticPluginPackageRegistryReader(),
            new StaticPluginPackageSignatureVerifier(),
            new StaticNuGetPluginAssemblyResolver(),
            new RecordingHostApplicationEventPublisher(),
            localPluginInstallSourceResolver: localResolver);

        var result = await sut.UpdateFromLocalAsync(
            new UpdateLocalPluginCommand(
                PluginId: "plugin-local",
                BuildIfNeeded: true,
                ForceBuild: true,
                RequestedBy: "tester"));

        Assert.Equal(PluginLifecycleServiceStatus.Ok, result.Status);
        Assert.True(result.IsSuccess);
        Assert.Equal("plugin-local", result.PluginId);
        Assert.Equal("/tmp/plugin-local-new.dll", lifecycle.LastInstallAssemblyPath);
        Assert.Equal(1, lifecycle.DeactivateCallCount);
        Assert.Equal(1, lifecycle.UninstallCallCount);
        Assert.Equal(1, lifecycle.ActivateCallCount);

        var updated = await installations.GetByPluginIdAsync("plugin-local");
        Assert.NotNull(updated);
        Assert.Equal("/tmp/plugin-local-new.dll", updated!.AssemblyPath);
        Assert.Equal(PluginInstallationState.Active, updated.State);
    }

}

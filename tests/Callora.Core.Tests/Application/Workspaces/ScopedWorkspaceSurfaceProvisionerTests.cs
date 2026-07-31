using Callora.Core.Application.Workspaces;
using Callora.Core.Application.Workspaces.Contracts;
using Callora.Core.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Callora.Core.Tests.Application.Workspaces;

public sealed class ScopedWorkspaceSurfaceProvisionerTests
{
    [Fact]
    public async Task Ensure_RootResolvedFacade_CreatesAnOwnedScope()
    {
        var workspaceStore = new InMemoryWorkspaceManagementStore();
        workspaceStore.AddTenant("tenant-a");
        _ = await workspaceStore.UpsertAsync(
            "tenant-a",
            "acme",
            "Acme",
            "standard",
            isActive: true,
            "https://example.test/acme");
        var surfaceStore = new InMemoryWorkspaceSurfaceStore();
        var services = new ServiceCollection()
            .AddScoped(_ => new WorkspaceSurfaceProvisioner(workspaceStore, surfaceStore))
            .AddSingleton<IWorkspaceSurfaceProvisioner, ScopedWorkspaceSurfaceProvisioner>()
            .BuildServiceProvider(validateScopes: true);

        var provisioner = services.GetRequiredService<IWorkspaceSurfaceProvisioner>();
        var location = await provisioner.EnsureAsync(
            "acme",
            new PluginSurfaceDefinition(
                "videoconference",
                "Video Conference",
                "videoconference",
                "/meet",
                PluginSurfaceAccessMode.Mixed,
                "videoconference",
                "0.1.0"));

        Assert.NotNull(location);
        Assert.Equal("/acme/meet", location!.PublicPath);
    }
}

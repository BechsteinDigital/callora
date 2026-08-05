using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication;
using Callora.Plugin.Communication.Application.Admin;
using Callora.Plugin.Communication.Infrastructure.Channels;
using Xunit;

namespace Callora.Core.Tests.Communication;

/// <summary>
/// Walking-Skeleton-Tests (B2): das Plugin-Hauptprojekt lädt, die Composition Root hat eine
/// stabile Identität und der Operator-Admin-Contributor deklariert eine erreichbare
/// Status-Route inkl. Permission-Fläche.
/// </summary>
public sealed class CommunicationPluginSkeletonTests
{
    [Fact]
    public void Plugin_HasStableIdentity()
    {
        var plugin = new CommunicationPlugin();

        Assert.Equal("communication", plugin.PluginId);
        Assert.Equal(CommunicationPlugin.Id, plugin.PluginId);
        Assert.Equal("Communication", plugin.DisplayName);
    }

    [Fact]
    public void AdminContributor_DeclaresStatusRouteAndPermissions()
    {
        var contributor = new CommunicationAdminApiExtensionContributor([], NewProbe());

        Assert.Equal(CommunicationPlugin.Id, contributor.PluginId);
        Assert.Contains(CommunicationPermissionKeys.AccountsRead, contributor.PermissionKeys);

        var route = Assert.Single(contributor.Routes);
        Assert.Equal("GET", route.HttpMethod);
        Assert.Equal("status", route.RouteTemplate);
        Assert.Equal(CommunicationPermissionKeys.AccountsRead, route.RequiredPermission);

        var nav = Assert.Single(contributor.NavigationItems);
        Assert.Equal("communication", nav.Id);
        Assert.Equal(CommunicationPermissionKeys.AccountsRead, nav.RequiredPermission);
    }

    [Fact]
    public async Task StatusRoute_WithoutAnyChannel_ReportsUnavailable()
    {
        // No registered channel means nothing can be dialled, so readiness must say so
        // instead of the constant "ok" this route used to answer (#112).
        var handler = new CommunicationStatusRouteHandler(NewProbe());
        var request = new HostAdminApiRequest(
            CommunicationPlugin.Id,
            "GET",
            "status",
            new Dictionary<string, string>(),
            new Dictionary<string, string[]>(),
            Body: null,
            UserId: null);

        var response = await handler.HandleAsync(request);

        Assert.Equal(503, response.StatusCode);
        var status = Assert.IsType<CommunicationStatus>(response.Payload);
        Assert.Equal(CommunicationPlugin.Id, status.PluginId);
        Assert.Equal(CommunicationReadiness.Unavailable, status.Status);
        Assert.Contains(status.Dependencies, x => x.Name == "channels" && x.State == "down");
    }

    private static CommunicationReadinessProbe NewProbe() =>
        new(new CommunicationChannelRegistry());
}

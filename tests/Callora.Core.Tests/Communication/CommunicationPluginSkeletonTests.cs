using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication;
using Callora.Plugin.Communication.Application.Admin;
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
        var contributor = new CommunicationAdminApiExtensionContributor();

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
    public async Task StatusRoute_ReturnsOkPayload()
    {
        var handler = new CommunicationStatusRouteHandler();
        var request = new HostAdminApiRequest(
            CommunicationPlugin.Id,
            "GET",
            "status",
            new Dictionary<string, string>(),
            new Dictionary<string, string[]>(),
            Body: null,
            UserId: null);

        var response = await handler.HandleAsync(request);

        Assert.Equal(200, response.StatusCode);
        var status = Assert.IsType<CommunicationStatus>(response.Payload);
        Assert.Equal(CommunicationPlugin.Id, status.PluginId);
        Assert.Equal("ok", status.Status);
    }
}

using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Surfaces;
using Callora.Core.Tests.Support;

namespace Callora.Core.Tests.Application.Surfaces;

/// <summary>
/// What the surface API prefix will and will not serve (#125 block B). A refused
/// route is recorded with its reason rather than dropped, because a route that
/// silently never matches is the hardest misconfiguration to see from outside.
/// </summary>
public sealed class SurfaceApiRouteInventoryTests
{
    [Fact]
    public void ValidRoutesAreMountedUnderTheirPlugin()
    {
        var inventory = SurfaceApiRouteInventory.Build([
            Contributor("crm", Route("GET", "leads/{leadId}")),
        ]);

        var mounted = Assert.Single(inventory.Routes);
        Assert.Equal("crm", mounted.PluginId);
        Assert.Equal("leads/{leadId}", mounted.Route.RouteTemplate);
        Assert.Empty(inventory.Rejections);
    }

    [Theory]
    [InlineData("/leads")]
    [InlineData("../admin")]
    [InlineData("leads/../../admin")]
    [InlineData("leads\\admin")]
    public void ATemplateEscapingItsRootIsRefused(string template)
    {
        var inventory = SurfaceApiRouteInventory.Build([Contributor("crm", Route("GET", template))]);

        Assert.Empty(inventory.Routes);
        Assert.Equal(
            SurfaceApiRouteRejectionReason.InvalidTemplate,
            Assert.Single(inventory.Rejections).Reason);
    }

    [Theory]
    [InlineData("api")]
    [InlineData("admin")]
    [InlineData("surface")]
    [InlineData("ws")]
    [InlineData("crm/nested")]
    public void APluginIdShadowingThePlatformIsRefused(string pluginId)
    {
        var inventory = SurfaceApiRouteInventory.Build([Contributor(pluginId, Route("GET", "x"))]);

        Assert.Empty(inventory.Routes);
        Assert.Equal(
            SurfaceApiRouteRejectionReason.ReservedPluginId,
            Assert.Single(inventory.Rejections).Reason);
    }

    [Fact]
    public void TheFirstDeclarationWinsAndTheDuplicateIsRecorded()
    {
        var first = Route("GET", "leads");
        var second = Route("GET", "/leads/".Trim('/'));

        var inventory = SurfaceApiRouteInventory.Build([Contributor("crm", first, second)]);

        // Letting the later declaration shadow the earlier one would make the served
        // behaviour depend on export order.
        Assert.Same(first.Handler, Assert.Single(inventory.Routes).Route.Handler);
        Assert.Equal(
            SurfaceApiRouteRejectionReason.DuplicateRoute,
            Assert.Single(inventory.Rejections).Reason);
    }

    [Fact]
    public void TheSameTemplateUnderADifferentMethodIsNotADuplicate()
    {
        var inventory = SurfaceApiRouteInventory.Build([
            Contributor("crm", Route("GET", "leads"), Route("POST", "leads")),
        ]);

        Assert.Equal(2, inventory.Routes.Count);
        Assert.Empty(inventory.Rejections);
    }

    [Fact]
    public void TwoPluginsMayDeclareTheSameTemplate()
    {
        var inventory = SurfaceApiRouteInventory.Build([
            Contributor("crm", Route("GET", "items")),
            Contributor("shop", Route("GET", "items")),
        ]);

        // The plugin id in the path is what keeps them apart, so this is not a collision.
        Assert.Equal(2, inventory.Routes.Count);
        Assert.Empty(inventory.Rejections);
    }

    private static StaticSurfaceApiContributor Contributor(
        string pluginId,
        params HostSurfaceApiRouteRegistration[] routes) =>
        new(pluginId, routes);

    private static HostSurfaceApiRouteRegistration Route(string method, string template) =>
        new(method, template, new StaticSurfaceApiRouteHandler(200));
}

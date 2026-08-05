using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Surfaces;
using Callora.Core.Tests.Support;

namespace Callora.Core.Tests.Application.Surfaces;

/// <summary>
/// What a slot holds is decided on the server, before any markup exists (#125 block C):
/// a view the visitor may not see is never emitted, and the order a theme renders is
/// the host's, not the plugin load order's.
/// </summary>
public sealed class SurfaceSlotResolverTests
{
    private const string WorkspaceKey = "workspace-a";
    private const string SurfaceKey = "portal";

    [Fact]
    public async Task ViewsAreGroupedByTheirSlotAndOrderedByWeight()
    {
        var resolver = Build(
            ("crm", View("crm.lead-list", "workspace.main", weight: 20)),
            ("comm", View("comm.phone", "workspace.main", weight: 10)));

        var slots = await resolver.ResolveAsync(WorkspaceKey, SurfaceKey, Guest());

        var main = slots["workspace.main"];
        Assert.Equal(["comm.phone", "crm.lead-list"], main.Select(view => view.ViewId));
    }

    [Fact]
    public async Task TwoPluginsFillTheSameSlotWithoutKnowingEachOther()
    {
        var resolver = Build(
            ("crm", View("crm.lead-list", "workspace.main")),
            ("videoconference", View("vc.room", "workspace.main")));

        var slots = await resolver.ResolveAsync(WorkspaceKey, SurfaceKey, Guest());

        Assert.Equal(2, slots["workspace.main"].Count);
    }

    [Fact]
    public async Task AViewPinnedToAnotherSurfaceIsNotEmitted()
    {
        var resolver = Build(
            ("crm", View("crm.lead-list", "workspace.main", surfaceKeys: ["shop"])));

        Assert.Empty(await resolver.ResolveAsync(WorkspaceKey, SurfaceKey, Guest()));
    }

    [Fact]
    public async Task AViewFromAnUnavailablePluginIsNotEmitted()
    {
        var resolver = Build(
            unavailablePluginIds: ["crm"],
            contributions: ("crm", View("crm.lead-list", "workspace.main")));

        Assert.Empty(await resolver.ResolveAsync(WorkspaceKey, SurfaceKey, Guest()));
    }

    [Fact]
    public async Task AClaimGatedViewIsWithheldFromAGuest()
    {
        var resolver = Build(
            ("crm", View("crm.lead-list", "workspace.main", requiredClaims: ["crm.roles"])));

        Assert.Empty(await resolver.ResolveAsync(WorkspaceKey, SurfaceKey, Guest()));
    }

    [Fact]
    public async Task AClaimGatedViewReachesACallerCarryingTheClaim()
    {
        var resolver = Build(
            ("crm", View("crm.lead-list", "workspace.main", requiredClaims: ["crm.roles"])));

        var slots = await resolver.ResolveAsync(WorkspaceKey, SurfaceKey, Authenticated("crm.roles"));

        Assert.Single(slots["workspace.main"]);
    }

    [Fact]
    public async Task ClaimGatingMatchesOnPresenceNotOnValue()
    {
        var resolver = Build(
            ("crm", View("crm.lead-list", "workspace.main", requiredClaims: ["crm.roles"])));

        // The host never compares a claim's value: what "agent" means belongs to the
        // plugin that issued it.
        var slots = await resolver.ResolveAsync(
            WorkspaceKey, SurfaceKey, Authenticated("crm.roles", value: "anything-at-all"));

        Assert.Single(slots["workspace.main"]);
    }

    [Fact]
    public async Task TheSameViewIdIsEmittedOnceEvenIfDeclaredTwice()
    {
        var resolver = Build(
            ("crm", View("crm.lead-list", "workspace.main", weight: 10)),
            ("crm", View("crm.lead-list", "workspace.main", weight: 20)));

        Assert.Single((await resolver.ResolveAsync(WorkspaceKey, SurfaceKey, Guest()))["workspace.main"]);
    }

    private static SurfaceSlotResolver Build(
        params (string PluginId, HostSurfaceViewRegistration View)[] contributions) =>
        Build(unavailablePluginIds: null, contributions);

    private static SurfaceSlotResolver Build(
        string[]? unavailablePluginIds,
        params (string PluginId, HostSurfaceViewRegistration View)[] contributions)
    {
        var catalog = new StaticPluginExportCatalog();
        foreach (var group in contributions.GroupBy(x => x.PluginId, StringComparer.Ordinal))
        {
            catalog.Add(
                group.Key,
                new StaticSurfaceViewContributor(group.Key, group.Select(x => x.View).ToArray()));
        }

        return new SurfaceSlotResolver(
            catalog, new StaticPluginAvailabilityEvaluator(unavailablePluginIds ?? []));
    }

    private static HostSurfaceViewRegistration View(
        string viewId,
        string slot,
        int weight = 0,
        IReadOnlyList<string>? surfaceKeys = null,
        IReadOnlyList<string>? requiredClaims = null) =>
        new(viewId, slot, viewId, weight, SurfaceViewCardinality.Multiple,
            SurfaceKeys: surfaceKeys, RequiredClaims: requiredClaims);

    private static SurfaceCaller Guest() =>
        new GuestSurfaceCaller(new SurfaceSubject(SurfaceIdentityIssuers.Guest, "g-1"));

    private static SurfaceCaller Authenticated(string claimKey, string value = "agent") =>
        new AuthenticatedSurfaceCaller(
            new SurfaceSubject("crm.example", "lead-42"),
            new SurfaceIdentity(
                "Erika Muster",
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
                {
                    [claimKey] = [value],
                },
                "password",
                DateTimeOffset.UtcNow.AddMinutes(-1),
                DateTimeOffset.UtcNow.AddHours(1)));
}

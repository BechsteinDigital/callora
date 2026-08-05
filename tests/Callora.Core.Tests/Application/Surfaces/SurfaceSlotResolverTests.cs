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

        var slots = (await resolver.ResolveAsync(WorkspaceKey, SurfaceKey, Guest())).Slots;

        var main = slots["workspace.main"];
        Assert.Equal(["comm.phone", "crm.lead-list"], main.Select(view => view.ViewId));
    }

    [Fact]
    public async Task TwoPluginsFillTheSameSlotWithoutKnowingEachOther()
    {
        var resolver = Build(
            ("crm", View("crm.lead-list", "workspace.main")),
            ("videoconference", View("vc.room", "workspace.main")));

        var slots = (await resolver.ResolveAsync(WorkspaceKey, SurfaceKey, Guest())).Slots;

        Assert.Equal(2, slots["workspace.main"].Count);
    }

    [Fact]
    public async Task AViewPinnedToAnotherSurfaceIsNotEmitted()
    {
        var resolver = Build(
            ("crm", View("crm.lead-list", "workspace.main", surfaceKeys: ["shop"])));

        Assert.Empty((await resolver.ResolveAsync(WorkspaceKey, SurfaceKey, Guest())).Slots);
    }

    [Fact]
    public async Task AViewFromAnUnavailablePluginIsNotEmitted()
    {
        var resolver = Build(
            unavailablePluginIds: ["crm"],
            contributions: ("crm", View("crm.lead-list", "workspace.main")));

        Assert.Empty((await resolver.ResolveAsync(WorkspaceKey, SurfaceKey, Guest())).Slots);
    }

    [Fact]
    public async Task AClaimGatedViewIsWithheldFromAGuest()
    {
        var resolver = Build(
            ("crm", View("crm.lead-list", "workspace.main", requiredClaims: ["crm.roles"])));

        Assert.Empty((await resolver.ResolveAsync(WorkspaceKey, SurfaceKey, Guest())).Slots);
    }

    [Fact]
    public async Task AClaimGatedViewReachesACallerCarryingTheClaim()
    {
        var resolver = Build(
            ("crm", View("crm.lead-list", "workspace.main", requiredClaims: ["crm.roles"])));

        var slots = (await resolver.ResolveAsync(WorkspaceKey, SurfaceKey, Authenticated("crm.roles"))).Slots;

        Assert.Single(slots["workspace.main"]);
    }

    [Fact]
    public async Task ClaimGatingMatchesOnPresenceNotOnValue()
    {
        var resolver = Build(
            ("crm", View("crm.lead-list", "workspace.main", requiredClaims: ["crm.roles"])));

        // The host never compares a claim's value: what "agent" means belongs to the
        // plugin that issued it.
        var slots = (await resolver.ResolveAsync(
            WorkspaceKey, SurfaceKey, Authenticated("crm.roles", value: "anything-at-all"))).Slots;

        Assert.Single(slots["workspace.main"]);
    }

    [Fact]
    public async Task TheSameViewIdIsEmittedOnceEvenIfDeclaredTwice()
    {
        var resolver = Build(
            ("crm", View("crm.lead-list", "workspace.main", weight: 10)),
            ("crm", View("crm.lead-list", "workspace.main", weight: 20)));

        Assert.Single((await resolver.ResolveAsync(WorkspaceKey, SurfaceKey, Guest())).Slots["workspace.main"]);
    }

    [Fact]
    public async Task NavigationIsContributedByPluginsAndOrderedByTheHost()
    {
        var resolver = BuildWithNavigation(
            ("crm", Nav("crm.leads", "Leads", order: 20)),
            ("comm", Nav("comm.phone", "Phone", order: 10)));

        var navigation = (await resolver.ResolveAsync(WorkspaceKey, SurfaceKey, Guest())).Navigation;

        Assert.Equal(["comm.phone", "crm.leads"], navigation.Select(entry => entry.Id));
        Assert.Equal("comm", navigation[0].PluginId);
    }

    [Fact]
    public async Task AClaimGatedNavigationEntryIsWithheldFromAGuest()
    {
        var resolver = BuildWithNavigation(
            ("crm", Nav("crm.leads", "Leads", requiredClaims: ["crm.roles"])));

        // Withheld rather than greyed out: the entry never reaches the markup.
        Assert.Empty((await resolver.ResolveAsync(WorkspaceKey, SurfaceKey, Guest())).Navigation);
        Assert.Single(
            (await resolver.ResolveAsync(WorkspaceKey, SurfaceKey, Authenticated("crm.roles"))).Navigation);
    }

    [Fact]
    public async Task NavigationFromAnUnavailablePluginIsNotContributed()
    {
        var resolver = BuildWithNavigation(
            unavailablePluginIds: ["crm"],
            contributions: ("crm", Nav("crm.leads", "Leads")));

        Assert.Empty((await resolver.ResolveAsync(WorkspaceKey, SurfaceKey, Guest())).Navigation);
    }

    private static SurfaceSlotResolver BuildWithNavigation(
        params (string PluginId, HostSurfaceNavigationItem Item)[] contributions) =>
        BuildWithNavigation(unavailablePluginIds: null, contributions);

    private static SurfaceSlotResolver BuildWithNavigation(
        string[]? unavailablePluginIds,
        params (string PluginId, HostSurfaceNavigationItem Item)[] contributions)
    {
        var catalog = new StaticPluginExportCatalog();
        foreach (var group in contributions.GroupBy(x => x.PluginId, StringComparer.Ordinal))
        {
            catalog.Add(
                group.Key,
                new StaticSurfaceViewContributor(group.Key, [], group.Select(x => x.Item).ToArray()));
        }

        return new SurfaceSlotResolver(
            catalog, new StaticPluginAvailabilityEvaluator(unavailablePluginIds ?? []));
    }

    private static HostSurfaceNavigationItem Nav(
        string id,
        string label,
        int order = 0,
        IReadOnlyList<string>? requiredClaims = null) =>
        new(id, label, $"/{id}", Order: order, RequiredClaims: requiredClaims);

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

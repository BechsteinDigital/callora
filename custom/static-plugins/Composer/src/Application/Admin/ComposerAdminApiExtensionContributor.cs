using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Plugin.Composer.Application.Admin;

/// <summary>
/// The Composer's Admin-API surface: what the editor talks to.
/// <para>
/// Every route names the permission it needs, and reading a draft needs one. That is where the
/// draft guarantee from the core contract actually holds: the public render path calls
/// <c>GetPublishedAsync</c> on a different contract and can never reach a draft, and the only
/// route that can is behind an operator permission. Two methods rather than one with a flag is
/// what makes this checkable at all.
/// </para>
/// <para>
/// Workspace-scoped, like almost everything: a layout belongs to a workspace, and the host
/// resolves which one before dispatching (#109). Nothing here re-reads a workspace from the
/// query, which would step around that gate.
/// </para>
/// </summary>
public sealed class ComposerAdminApiExtensionContributor : IHostAdminApiExtensionContributor
{
    private readonly IReadOnlyList<HostAdminApiRouteRegistration> _routes;

    public ComposerAdminApiExtensionContributor(SurfaceLayoutStore store)
    {
        ArgumentNullException.ThrowIfNull(store);

        _routes =
        [
            new HostAdminApiRouteRegistration(
                "GET",
                "layouts/{layoutKey}/draft",
                ComposerPermissionKeys.LayoutRead,
                new LayoutDraftRouteHandler(store)),

            new HostAdminApiRouteRegistration(
                "PUT",
                "layouts/{layoutKey}/draft",
                ComposerPermissionKeys.LayoutWrite,
                new LayoutSaveRouteHandler(store)),

            // Veröffentlichen und Verwerfen teilen eine Berechtigung: Beide entscheiden über den
            // Unterschied zwischen dem, was jemand gebaut hat, und dem, was Besucher sehen.
            new HostAdminApiRouteRegistration(
                "POST",
                "layouts/{layoutKey}/publish",
                ComposerPermissionKeys.LayoutPublish,
                new LayoutPublishRouteHandler(store)),

            new HostAdminApiRouteRegistration(
                "POST",
                "layouts/{layoutKey}/discard",
                ComposerPermissionKeys.LayoutPublish,
                new LayoutDiscardRouteHandler(store)),
        ];
    }

    /// <inheritdoc />
    public string PluginId => "composer";

    /// <inheritdoc />
    public IReadOnlyList<string> PermissionKeys => ComposerPermissionKeys.All;

    /// <inheritdoc />
    public IReadOnlyList<HostAdminApiRouteRegistration> Routes => _routes;

    /// <inheritdoc />
    public IReadOnlyList<HostAdminNavigationItem> NavigationItems { get; } =
    [
        new HostAdminNavigationItem(
            Id: "composer",
            Label: "Flächen gestalten",
            To: "/extensions/composer",
            Icon: "i-lucide-layout-template",
            Order: 40,
            RequiredPermission: ComposerPermissionKeys.LayoutRead),
    ];
}

using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Workspaces;

namespace Callora.Plugin.Composer.Application.Admin;

/// <summary>
/// Der Seitenbaum des Editors: die Surfaces des Workspaces, jede mit ihrem Layout.
/// <para>
/// Eine eigene Route und nicht der Umweg über <c>/api/workspaces/{key}/surfaces</c>: Jene
/// verlangt <c>workspace.read</c>, und wer Flächen gestaltet, ist nicht zwingend jemand, der
/// Workspaces verwalten darf. Das Plugin liefert, was seine Oberfläche braucht, unter seiner
/// eigenen Berechtigung — genau die Trennung, für die es Plugin-Routen gibt.
/// </para>
/// <para>
/// Ausgeliefert werden nur Name, Elternteil, Reihenfolge und das Layout. Host, Zugangsmodus und
/// Identitätsbindung bleiben draußen: Ein Editor braucht sie nicht, und was nicht ausgeliefert
/// wird, kann auch nicht versehentlich angezeigt werden.
/// </para>
/// </summary>
public sealed class PageTreeRouteHandler(
    SurfaceLayoutStore store,
    IWorkspaceSurfaceStore surfaces) : IHostAdminApiRouteHandler
{
    /// <inheritdoc />
    public async ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.WorkspaceKey))
        {
            return new HostAdminApiResponse(400, new { error = "A workspace is required." });
        }

        var nodes = await surfaces.ListAsync(request.WorkspaceKey, cancellationToken).ConfigureAwait(false);
        var layouts = await store.ListAsync(request.WorkspaceKey, cancellationToken).ConfigureAwait(false);
        var layoutBySurface = layouts
            .Where(layout => !string.IsNullOrWhiteSpace(layout.SurfaceKey))
            .ToDictionary(layout => layout.SurfaceKey!, StringComparer.Ordinal);

        var pages = nodes
            .Select(node =>
            {
                layoutBySurface.TryGetValue(node.SurfaceKey, out var layout);
                return new PageTreeResponse(
                    node.SurfaceKey,
                    string.IsNullOrWhiteSpace(node.DisplayName) ? node.SurfaceKey : node.DisplayName,
                    node.ParentSurfaceKey,
                    node.Position,
                    layout?.LayoutKey,
                    layout?.HasPublishedVersion ?? false);
            })
            .ToArray();

        return new HostAdminApiResponse(200, pages);
    }
}

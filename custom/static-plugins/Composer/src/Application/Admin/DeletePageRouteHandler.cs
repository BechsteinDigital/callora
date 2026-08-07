using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Workspaces;

namespace Callora.Plugin.Composer.Application.Admin;

/// <summary>
/// Entfernt eine Seite: den Surface-Knoten und die Erlebniswelt dazu.
/// <para>
/// <b>Eine Seite mit Unterseiten wird nicht gelöscht.</b> Was mit ihnen geschehen soll, ist
/// eine Entscheidung (ADR-019 §7): sie an den Großelternknoten zu hängen ändert stillschweigend
/// URLs, sie mitzulöschen verliert Erlebniswelten. Bis jemand sie trifft, passiert nichts —
/// <c>409</c>, weil die Seite ja da ist.
/// </para>
/// <para>
/// Die Erlebniswelt geht ZUERST: Bliebe sie stehen, während die Fläche verschwindet, hätte
/// niemand mehr einen Weg zu ihr — sie stünde in keinem Baum und in keiner Auswahl mit Fläche.
/// Andersherum ist der Zwischenzustand eine Gliederungsebene, die man sieht.
/// </para>
/// </summary>
public sealed class DeletePageRouteHandler(
    SurfaceLayoutStore store,
    IWorkspaceSurfaceStore surfaces) : IHostAdminApiRouteHandler
{
    /// <inheritdoc />
    public async ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.WorkspaceKey) ||
            !request.RouteValues.TryGetValue("surfaceKey", out var surfaceKey) ||
            string.IsNullOrWhiteSpace(surfaceKey))
        {
            return new HostAdminApiResponse(400, new { error = "A workspace and a page are required." });
        }

        var page = await surfaces
            .GetAsync(request.WorkspaceKey, surfaceKey, cancellationToken)
            .ConfigureAwait(false);
        if (page is null)
        {
            return new HostAdminApiResponse(404);
        }

        // Nur Seiten, keine Anwendungswurzeln: Eine Wurzel trägt Host, Zugangsmodus und
        // Identitätsanbieter — sie zu entfernen ist Zugangsverwaltung, nicht Gestaltung.
        if (string.IsNullOrWhiteSpace(page.ParentSurfaceKey))
        {
            return new HostAdminApiResponse(409, new
            {
                error = "An application root is removed in the workspace administration.",
            });
        }

        var all = await surfaces.ListAsync(request.WorkspaceKey, cancellationToken).ConfigureAwait(false);
        if (all.Any(other => string.Equals(other.ParentSurfaceKey, surfaceKey, StringComparison.Ordinal)))
        {
            return new HostAdminApiResponse(409, new
            {
                error = "This page has sub-pages. Move or delete them first.",
            });
        }

        await store.DeleteAsync(surfaceKey, cancellationToken).ConfigureAwait(false);
        await surfaces.DeleteAsync(request.WorkspaceKey, surfaceKey, cancellationToken).ConfigureAwait(false);

        return new HostAdminApiResponse(204);
    }
}

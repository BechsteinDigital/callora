using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Workspaces;
using System.Text.Json;

namespace Callora.Plugin.Composer.Application.Admin;

/// <summary>
/// Verschiebt eine Seite — unter ein anderes Übergeordnetes oder an eine andere Stelle.
/// <para>
/// Die Erlebniswelt bleibt, wo sie ist: Verschoben wird der Knoten, nicht sein Inhalt. Was
/// sich ändert, ist die URL — und zwar für den ganzen Teilbaum, weil jedes Kind sein Segment
/// trägt und den Rest aus der Kette bekommt (ADR-019 §6). Genau dafür ist der Pfad relativ.
/// </para>
/// </summary>
public sealed class MovePageRouteHandler(IWorkspaceSurfaceStore surfaces) : IHostAdminApiRouteHandler
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.WorkspaceKey) ||
            !request.RouteValues.TryGetValue("surfaceKey", out var surfaceKey) ||
            string.IsNullOrWhiteSpace(surfaceKey) ||
            request.Body is not { } body)
        {
            return new HostAdminApiResponse(400, new { error = "A workspace, a page and a body are required." });
        }

        MovePageRequest? move;
        try
        {
            move = body.Deserialize<MovePageRequest>(Options);
        }
        catch (JsonException)
        {
            return new HostAdminApiResponse(400, new { error = "The body could not be read." });
        }

        if (move is null || string.IsNullOrWhiteSpace(move.ParentSurfaceKey))
        {
            return new HostAdminApiResponse(400, new
            {
                error = "A page needs a parent. Application roots are managed in the workspace " +
                        "administration.",
            });
        }

        var page = await surfaces
            .GetAsync(request.WorkspaceKey, surfaceKey, cancellationToken)
            .ConfigureAwait(false);
        if (page is null)
        {
            return new HostAdminApiResponse(404);
        }

        // Der Upsert ist ein vollständiges Ersetzen: Alles, was hier nicht mitgeschickt wird,
        // fiele weg. Deshalb wird der bestehende Stand übernommen und nur bewegt, was sich
        // bewegen soll — sonst verlöre eine Verschiebung nebenbei Locale, Zugangsmodus und
        // Sichtbarkeits-Anforderung.
        var moved = await surfaces
            .UpsertAsync(
                request.WorkspaceKey,
                new WorkspaceSurfaceInput(
                    page.SurfaceKey,
                    page.DisplayName,
                    page.SurfaceType,
                    page.PublicBaseUrl,
                    page.PublicHost,
                    page.PublicPathPrefix,
                    page.AccessMode,
                    page.Locale,
                    page.TemplatePluginId,
                    page.TemplateVersion,
                    page.ThemePluginId,
                    page.ThemeVersion,
                    page.IsActive)
                {
                    ParentSurfaceKey = move.ParentSurfaceKey.Trim(),
                    Position = move.Position,
                    RequiredClaims = page.RequiredClaims,
                },
                cancellationToken)
            .ConfigureAwait(false);

        // Null heißt hier: das Ziel gibt es nicht, oder es läge unter dieser Seite — ein Zyklus.
        return moved is null
            ? new HostAdminApiResponse(400, new
            {
                error = "The target page does not exist or lies below this one.",
            })
            : new HostAdminApiResponse(200, new { moved.SurfaceKey, moved.ParentSurfaceKey, moved.Position });
    }
}

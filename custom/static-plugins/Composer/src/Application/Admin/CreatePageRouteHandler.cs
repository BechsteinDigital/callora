using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using System.Text.Json;

namespace Callora.Plugin.Composer.Application.Admin;

/// <summary>
/// Legt eine Seite an: den Surface-Knoten und die Erlebniswelt dazu, in einem Vorgang.
/// <para>
/// Bisher waren das zwei getrennte Schritte in zwei Oberflächen — Fläche in der
/// Workspace-Verwaltung, Layout im Composer —, und wer den zweiten vergaß, hatte einen Knoten,
/// der auf nichts zeigt.
/// </para>
/// <para>
/// <b>Nur Kind-Knoten.</b> Eine Anwendungswurzel trägt Host, Zugangsmodus und
/// Identitätsanbieter (ADR-019 §2); sie anzulegen ist Zugangsverwaltung und gehört nicht in
/// einen Editor, dessen Berechtigung „Layouts schreiben" heißt. Ein Kind erbt all das und
/// trägt nur Name, Segment und Elternteil — deshalb ist genau hier die Grenze.
/// </para>
/// </summary>
public sealed class CreatePageRouteHandler(
    SurfaceLayoutStore store,
    IWorkspaceSurfaceStore surfaces) : IHostAdminApiRouteHandler
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

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

        if (request.Body is not { } body)
        {
            return new HostAdminApiResponse(400, new { error = "A body is required." });
        }

        CreatePageRequest? create;
        try
        {
            create = body.Deserialize<CreatePageRequest>(Options);
        }
        catch (JsonException)
        {
            return new HostAdminApiResponse(400, new { error = "The body could not be read." });
        }

        if (create is null ||
            string.IsNullOrWhiteSpace(create.SurfaceKey) ||
            string.IsNullOrWhiteSpace(create.Label))
        {
            return new HostAdminApiResponse(400, new { error = "A key and a label are required." });
        }

        if (string.IsNullOrWhiteSpace(create.ParentSurfaceKey))
        {
            return new HostAdminApiResponse(400, new
            {
                error = "A page needs a parent. Application roots carry host, access mode and " +
                        "identity provider — they are created in the workspace administration.",
            });
        }

        var parent = await surfaces
            .GetAsync(request.WorkspaceKey, create.ParentSurfaceKey, cancellationToken)
            .ConfigureAwait(false);
        if (parent is null)
        {
            return new HostAdminApiResponse(404, new { error = "The parent page does not exist." });
        }

        var surfaceKey = create.SurfaceKey.Trim();
        var existing = await surfaces
            .GetAsync(request.WorkspaceKey, surfaceKey, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            // 409 und nicht 400: Der Schlüssel ist nicht falsch, er ist vergeben.
            return new HostAdminApiResponse(409, new { error = $"Surface '{surfaceKey}' already exists." });
        }

        // Die Fläche zuerst. Scheitert das Layout danach, steht ein Knoten ohne Erlebniswelt da
        // — sichtbar im Baum, als Gliederungsebene benannt, jederzeit nachrüstbar. Andersherum
        // stünde ein Layout ohne Fläche irgendwo, das niemand mehr findet.
        var created = await surfaces
            .UpsertAsync(
                request.WorkspaceKey,
                new WorkspaceSurfaceInput(
                    surfaceKey,
                    create.Label.Trim(),
                    parent.SurfaceType,
                    PublicBaseUrl: null,
                    PublicHost: null,
                    // Das eigene Segment. Leer heißt: der Schlüssel — was jemand meint, der nur
                    // einen Namen eingibt.
                    string.IsNullOrWhiteSpace(create.PathSegment) ? surfaceKey : create.PathSegment.Trim(),
                    // Der Zugangsmodus wird geerbt, indem er dem des Elternteils entspricht: Er
                    // ist nicht nullbar, es gibt also kein „nicht gesetzt" (ADR-019 §3.1).
                    parent.AccessMode,
                    Locale: null,
                    TemplatePluginId: null,
                    TemplateVersion: null,
                    ThemePluginId: null,
                    ThemeVersion: null,
                    IsActive: true)
                {
                    ParentSurfaceKey = parent.SurfaceKey,
                },
                cancellationToken)
            .ConfigureAwait(false);
        if (created is null)
        {
            return new HostAdminApiResponse(400, new { error = "The page could not be created." });
        }

        var layout = await store
            .CreateAsync(
                surfaceKey,
                request.WorkspaceKey,
                surfaceKey,
                create.Label.Trim(),
                request.UserId ?? "operator",
                cancellationToken)
            .ConfigureAwait(false);

        return new HostAdminApiResponse(201, new PageTreeResponse(
            created.SurfaceKey,
            created.DisplayName,
            created.ParentSurfaceKey,
            created.Position,
            layout.Key,
            HasPublishedVersion: false));
    }
}

using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Plugin.Composer.Application.Admin;

/// <summary>
/// Listet die Layouts eines Workspaces — damit der Editor eine Auswahl zeigen kann statt eines
/// Textfelds, in das man den Schlüssel tippt.
/// <para>
/// Der Workspace kommt aus der Anfrage und nicht aus dem Aufrufer-Parameter: Die Route ist
/// <c>Workspace</c>-scoped, der Host hat ihn also schon aufgelöst und geprüft. Ihn hier noch
/// einmal aus einem Query-Parameter zu nehmen wäre ein zweiter Weg, auf dem jemand einen
/// fremden Workspace benennen könnte.
/// </para>
/// </summary>
public sealed class LayoutListRouteHandler(SurfaceLayoutStore store) : IHostAdminApiRouteHandler
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

        var layouts = await store
            .ListAsync(request.WorkspaceKey, cancellationToken)
            .ConfigureAwait(false);

        return new HostAdminApiResponse(200, layouts);
    }
}

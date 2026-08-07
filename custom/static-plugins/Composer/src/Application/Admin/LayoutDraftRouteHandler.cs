using System.Text.Json;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Composer.Domain;

namespace Callora.Plugin.Composer.Application.Admin;

/// <summary>
/// Reads the working draft for the editor.
/// <para>
/// This is the one place <c>GetDraftAsync</c> may be reached from, and the route that carries it
/// declares <see cref="ComposerPermissionKeys.LayoutRead"/>. The public render path has no way
/// here: it calls a different method on a different contract, and there is no parameter anywhere
/// that turns it into this one. That separation is the whole point of two methods rather than one
/// with a flag — on a Public surface a draft leak would sit behind no authentication at all.
/// </para>
/// <para>
/// The response carries the change stamp. The editor sends it back when saving, which is how a
/// second writer with a stale view gets a conflict instead of overwriting the first.
/// </para>
/// </summary>
public sealed class LayoutDraftRouteHandler(SurfaceLayoutStore store) : IHostAdminApiRouteHandler
{
    /// <inheritdoc />
    public async ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.RouteValues.TryGetValue("layoutKey", out var layoutKey) ||
            string.IsNullOrWhiteSpace(layoutKey))
        {
            return new HostAdminApiResponse(400, new { error = "layoutKey is required." });
        }

        var draft = await store.GetDraftAsync(layoutKey, cancellationToken).ConfigureAwait(false);
        if (draft is null)
        {
            return new HostAdminApiResponse(404);
        }

        return new HostAdminApiResponse(200, new LayoutDraftResponse(
            draft.LayoutKey,
            draft.VersionNumber,
            JsonSerializer.Deserialize<JsonElement>(draft.Document),
            draft.ChangedAtUtc));
    }
}

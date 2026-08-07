using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Plugin.Composer.Application.Admin;

/// <summary>
/// Throws the draft away and rebuilds it from what is live.
/// <para>
/// Unter derselben Berechtigung wie das Veröffentlichen: Beide entscheiden über den Unterschied
/// zwischen dem, was jemand gebaut hat, und dem, was Besucher sehen — nur in verschiedene
/// Richtungen.
/// </para>
/// </summary>
public sealed class LayoutDiscardRouteHandler(SurfaceLayoutStore store) : IHostAdminApiRouteHandler
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

        try
        {
            await store
                .DiscardAsync(layoutKey, request.UserId ?? "unknown", cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return new HostAdminApiResponse(404);
        }

        return new HostAdminApiResponse(204);
    }
}

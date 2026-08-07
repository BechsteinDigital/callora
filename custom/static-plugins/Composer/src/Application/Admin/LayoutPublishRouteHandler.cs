using System.Text.Json;
using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Plugin.Composer.Application.Admin;

/// <summary>
/// Makes the draft live.
/// <para>
/// Its own permission, separate from writing: a draft is not yet anybody's decision, publishing
/// puts it in front of visitors. Who did it is recorded — the user id comes from the host's
/// request, never from the body, so it cannot be claimed.
/// </para>
/// </summary>
public sealed class LayoutPublishRouteHandler(SurfaceLayoutStore store) : IHostAdminApiRouteHandler
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

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

        string? label = null;
        if (request.Body is { } body)
        {
            try
            {
                label = body.Deserialize<LayoutPublishRequest>(Options)?.Label;
            }
            catch (JsonException)
            {
                return new HostAdminApiResponse(400, new { error = "The body could not be read." });
            }
        }

        try
        {
            await store
                .PublishAsync(layoutKey, request.UserId ?? "unknown", label, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // Kein Entwurf zu diesem Layout — für den Aufrufer nicht von "gibt es nicht" zu
            // unterscheiden, und das genügt ihm auch.
            return new HostAdminApiResponse(404);
        }

        return new HostAdminApiResponse(204);
    }
}

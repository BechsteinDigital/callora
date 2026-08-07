using System.Text.Json;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Api.Surface;

/// <summary>
/// Handles <c>POST calls</c> from a surface dialer.
/// </summary>
public sealed class SurfacePlaceCallRouteHandler(ICallControlService calls) : IHostSurfaceApiRouteHandler
{
    /// <summary>Prefix of the quota origin a surface dials under.</summary>
    /// <remarks>
    /// Named after the surface, so an operator can cap one workplace without capping the others —
    /// and set here rather than taken from the request, because that is the whole point.
    /// </remarks>
    public const string OriginPrefix = "surface:";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async ValueTask<HostSurfaceApiResponse> HandleAsync(
        HostSurfaceApiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!SurfaceCallAccess.TryResolve(request, SurfaceCallAccess.Manage, out var workspaceKey, out var error))
        {
            return error!;
        }

        SurfacePlaceCallRequest? body;
        try
        {
            body = request.Body?.Deserialize<SurfacePlaceCallRequest>(SerializerOptions);
        }
        catch (JsonException)
        {
            body = null;
        }

        if (body is null || string.IsNullOrWhiteSpace(body.To))
        {
            return new HostSurfaceApiResponse(400, new { error = "to is required" });
        }

        try
        {
            var placed = await calls
                .PlaceCallAsync(
                    new PlaceCallCommand(
                        workspaceKey,
                        body.To!.Trim(),
                        DisplayName: string.IsNullOrWhiteSpace(body.DisplayName) ? null : body.DisplayName!.Trim(),
                        Origin: OriginPrefix + request.SurfaceKey),
                    cancellationToken)
                .ConfigureAwait(false);

            return new HostSurfaceApiResponse(201, placed);
        }
        catch (InvalidOperationException ex)
        {
            // No line, no channel, quota exhausted: all of them are "not right now" rather than a
            // defect, and the panel needs a sentence rather than a stack trace.
            return new HostSurfaceApiResponse(409, new { error = ex.Message });
        }
    }
}

using System.Text.Json;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Api.Surface;

/// <summary>
/// Handles <c>POST calls/{callId}/dtmf</c> — the keypad of a panel that is on a call.
/// </summary>
public sealed class SurfaceSendDtmfRouteHandler(ICallControlService calls) : IHostSurfaceApiRouteHandler
{
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

        if (!request.RouteValues.TryGetValue(SurfaceCallCommandRouteHandler.CallIdRouteKey, out var callId) ||
            string.IsNullOrWhiteSpace(callId))
        {
            return new HostSurfaceApiResponse(400, new { error = "callId required" });
        }

        SurfaceSendDtmfRequest? body;
        try
        {
            body = request.Body?.Deserialize<SurfaceSendDtmfRequest>(SerializerOptions);
        }
        catch (JsonException)
        {
            body = null;
        }

        if (body is null || string.IsNullOrWhiteSpace(body.Tones))
        {
            return new HostSurfaceApiResponse(400, new { error = "tones is required" });
        }

        var sent = await calls
            .SendDtmfAsync(workspaceKey, callId, body.Tones!, cancellationToken)
            .ConfigureAwait(false);

        return sent
            ? new HostSurfaceApiResponse(200, new { sent = true })
            : new HostSurfaceApiResponse(404, new { error = "call not found" });
    }
}

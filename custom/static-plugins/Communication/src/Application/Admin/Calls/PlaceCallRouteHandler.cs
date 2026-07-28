using System.Text.Json;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Application.Admin.Calls;

/// <summary>
/// Handles <c>POST calls</c> — places one outbound call in the caller's workspace via the call-control
/// primitive. The out-of-process face of <see cref="ICallControlService.PlaceCallAsync"/>.
/// </summary>
public sealed class PlaceCallRouteHandler(ICallControlService callControl) : IHostAdminApiRouteHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!CallAdminScope.TryResolve(request, out var workspaceKey, out var scopeError))
        {
            return scopeError!;
        }

        PlaceCallApiRequest? body;
        try
        {
            body = request.Body?.Deserialize<PlaceCallApiRequest>(SerializerOptions);
        }
        catch (JsonException)
        {
            body = null;
        }

        if (body is null || string.IsNullOrWhiteSpace(body.To))
        {
            return new HostAdminApiResponse(400, new { error = "to is required." });
        }

        try
        {
            var snapshot = await callControl
                .PlaceCallAsync(
                    new PlaceCallCommand(workspaceKey, body.To.Trim(), body.ChannelId, body.DisplayName),
                    cancellationToken)
                .ConfigureAwait(false);
            return new HostAdminApiResponse(201, CallView.From(snapshot));
        }
        catch (InvalidOperationException ex)
        {
            // No voice-capable channel (or the named channel is not registered) for this workspace.
            return new HostAdminApiResponse(409, new { error = ex.Message });
        }
    }
}

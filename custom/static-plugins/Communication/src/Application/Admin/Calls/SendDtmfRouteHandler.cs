using System.Text.Json;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Application.Admin.Calls;

/// <summary>
/// Handles <c>POST calls/{callId}/dtmf</c> — sends keypad tones to the remote party of a live call.
/// This is how an IVR menu is navigated from a dialer or an automation, so it accepts a sequence
/// rather than one tone per request.
/// </summary>
public sealed class SendDtmfRouteHandler(ICallControlService callControl) : IHostAdminApiRouteHandler
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        SendDtmfApiRequest? body;
        try
        {
            body = request.Body?.Deserialize<SendDtmfApiRequest>(SerializerOptions);
        }
        catch (JsonException)
        {
            body = null;
        }

        if (string.IsNullOrWhiteSpace(body?.Tones))
        {
            return ValueTask.FromResult(new HostAdminApiResponse(400, new { error = "tones is required." }));
        }

        return CallControlRouteExecution.RunAsync(
            request,
            (workspaceKey, callId) => callControl.SendDtmfAsync(workspaceKey, callId, body.Tones, cancellationToken));
    }
}

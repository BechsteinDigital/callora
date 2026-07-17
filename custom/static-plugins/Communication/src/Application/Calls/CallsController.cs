using Callora.Core.Application.Http.Contracts;
using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// The /api/calls surface, shipped by the voice plugin (PLAT-257): live
/// list, channels, placement, call actions, DTMF and the SSE event stream.
/// Route shapes stay identical to the former host endpoints so the shells
/// keep working unchanged.
/// </summary>
public sealed class CallsController(VoipCallHub callHub, ICommunicationChannelRegistry channelRegistry)
    : WorkspaceApiController
{
    [CalloraRoute("GET", "/api/calls", Permission = "call.read")]
    public Task<ApiResult> ListAsync(ApiRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(string.IsNullOrWhiteSpace(request.WorkspaceKey)
            ? BadRequest("workspaceKey is required.")
            : Ok(callHub.List(request.WorkspaceKey)));

    [CalloraRoute("GET", "/api/calls/channels", Permission = "call.read")]
    public Task<ApiResult> ListChannelsAsync(ApiRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.WorkspaceKey))
        {
            return Task.FromResult(BadRequest("workspaceKey is required."));
        }

        var channels = channelRegistry
            .GetChannelsByCapability(request.WorkspaceKey, CommunicationCapabilities.Voice)
            .Select(static channel => new
            {
                channel.ChannelId,
                channel.DisplayName,
                channel.PluginId
            })
            .ToArray();
        return Task.FromResult(Ok(channels));
    }

    [CalloraRoute("POST", "/api/calls", Permission = "call.execute")]
    public async Task<ApiResult> PlaceAsync(ApiRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.WorkspaceKey))
        {
            return BadRequest("workspaceKey is required.");
        }

        var body = await request.ReadJsonAsync<PlaceCallRequestBody>(cancellationToken).ConfigureAwait(false);
        if (body is null || string.IsNullOrWhiteSpace(body.Target))
        {
            return BadRequest("target is required.");
        }

        try
        {
            var summary = await callHub.PlaceCallAsync(
                    request.WorkspaceKey,
                    body.ChannelId,
                    new CallTarget(body.Target, body.TargetDisplayName),
                    cancellationToken)
                .ConfigureAwait(false);
            return Ok(summary);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
    }

    [CalloraRoute("POST", "/api/calls/{callId}/accept", Permission = "call.execute")]
    public Task<ApiResult> AcceptAsync(ApiRequest request, CancellationToken cancellationToken) =>
        ExecuteCallActionAsync(request, static (call, ct) => call.AcceptAsync(ct), cancellationToken);

    [CalloraRoute("POST", "/api/calls/{callId}/reject", Permission = "call.execute")]
    public Task<ApiResult> RejectAsync(ApiRequest request, CancellationToken cancellationToken) =>
        ExecuteCallActionAsync(request, static (call, ct) => call.RejectAsync(ct), cancellationToken);

    [CalloraRoute("POST", "/api/calls/{callId}/hangup", Permission = "call.execute")]
    public Task<ApiResult> HangupAsync(ApiRequest request, CancellationToken cancellationToken) =>
        ExecuteCallActionAsync(request, static (call, ct) => call.HangupAsync(ct), cancellationToken);

    [CalloraRoute("POST", "/api/calls/{callId}/dtmf", Permission = "call.execute")]
    public async Task<ApiResult> SendDtmfAsync(ApiRequest request, CancellationToken cancellationToken)
    {
        var body = await request.ReadJsonAsync<SendDtmfRequestBody>(cancellationToken).ConfigureAwait(false);
        if (body is null || body.Tone is not { Length: 1 })
        {
            return BadRequest("tone must be a single DTMF character.");
        }

        var tone = body.Tone[0];
        return await ExecuteCallActionAsync(
                request,
                (call, ct) => call.SendDtmfAsync(tone, ct),
                cancellationToken)
            .ConfigureAwait(false);
    }

    [CalloraRoute("GET", "/api/calls/events", Permission = "call.read")]
    public async Task StreamEventsAsync(ApiRequest request, ApiEventStream stream, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.WorkspaceKey))
        {
            return;
        }

        using var subscription = callHub.Subscribe(request.WorkspaceKey);
        await foreach (var callEvent in subscription.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            await stream.WriteEventAsync(callEvent, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<ApiResult> ExecuteCallActionAsync(
        ApiRequest request,
        Func<ICall, CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.WorkspaceKey))
        {
            return BadRequest("workspaceKey is required.");
        }

        var callId = request.RouteValues.GetValueOrDefault("callId");
        if (string.IsNullOrWhiteSpace(callId) ||
            !callHub.TryGetTracked(request.WorkspaceKey, callId, out var tracked) ||
            tracked is null)
        {
            return NotFound($"Call '{callId}' was not found in workspace '{request.WorkspaceKey}'.");
        }

        try
        {
            await action(tracked.Call, cancellationToken).ConfigureAwait(false);
            // Shape-treu zum früheren Host-Endpoint: der volle Snapshot.
            return Ok(tracked.ToSummary());
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(exception.Message);
        }
    }
}

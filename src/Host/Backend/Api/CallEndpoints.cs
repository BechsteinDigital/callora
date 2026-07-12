using System.Text.Json;
using Callora.Contracts.Communication;
using Callora.Host.Backend.Application.Communication.Calls;
using Callora.Host.Backend.Infrastructure.Security;

namespace Callora.Host.Backend.Api;

/// <summary>
/// Live call control endpoints: list and place calls, answer or end them and
/// follow their lifecycle over a server-sent event stream.
/// </summary>
public static class CallEndpoints
{
    public static IEndpointRouteBuilder MapCallEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/calls")
            .RequireAuthorization();

        group.MapGet("/", (ActiveCallRegistry registry, string workspaceKey) =>
                Results.Ok(registry.List(workspaceKey)))
            .RequirePermission(BackendPermissionKeys.CallRead)
            .RequireWorkspaceScope();

        group.MapGet("/channels", (ICommunicationChannelRegistry channels, string workspaceKey) =>
                Results.Ok(channels
                    .GetChannelsByCapability(workspaceKey, CommunicationCapabilities.Voice)
                    .Select(channel => new
                    {
                        channel.ChannelId,
                        channel.DisplayName,
                        channel.PluginId
                    })))
            .RequirePermission(BackendPermissionKeys.CallRead)
            .RequireWorkspaceScope();

        group.MapPost("/", async (
            CallPlacementService placement,
            string workspaceKey,
            PlaceCallRequest request,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Target))
                return Results.BadRequest(new { error = "target is required." });

            try
            {
                var snapshot = await placement.PlaceCallAsync(
                    workspaceKey,
                    request.ChannelId,
                    new CallTarget(request.Target, request.TargetDisplayName),
                    cancellationToken);
                return Results.Ok(snapshot);
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new { error = exception.Message });
            }
        }).RequirePermission(BackendPermissionKeys.CallExecute)
            .RequireWorkspaceScope();

        MapCallAction(group, "accept", static (call, ct) => call.AcceptAsync(ct));
        MapCallAction(group, "reject", static (call, ct) => call.RejectAsync(ct));
        MapCallAction(group, "hangup", static (call, ct) => call.HangupAsync(ct));

        group.MapPost("/{callId}/dtmf", async (
            ActiveCallRegistry registry,
            string callId,
            string workspaceKey,
            SendCallDtmfRequest request,
            CancellationToken cancellationToken) =>
        {
            if (request.Tone is not { Length: 1 })
                return Results.BadRequest(new { error = "tone must be exactly one character." });

            if (!registry.TryGet(workspaceKey, callId, out var tracked) || tracked is null)
                return Results.NotFound();

            await tracked.Call.SendDtmfAsync(request.Tone[0], cancellationToken);
            return Results.Ok(tracked.ToSnapshot());
        }).RequirePermission(BackendPermissionKeys.CallExecute)
            .RequireWorkspaceScope();

        group.MapGet("/events", StreamCallEventsAsync)
            .RequirePermission(BackendPermissionKeys.CallRead)
            .RequireWorkspaceScope();

        return app;
    }

    private static void MapCallAction(
        IEndpointRouteBuilder group,
        string action,
        Func<ICall, CancellationToken, Task> execute)
    {
        group.MapPost($"/{{callId}}/{action}", async (
            ActiveCallRegistry registry,
            string callId,
            string workspaceKey,
            CancellationToken cancellationToken) =>
        {
            if (!registry.TryGet(workspaceKey, callId, out var tracked) || tracked is null)
                return Results.NotFound();

            try
            {
                await execute(tracked.Call, cancellationToken);
                return Results.Ok(tracked.ToSnapshot());
            }
            catch (InvalidOperationException exception)
            {
                return Results.Conflict(new { error = exception.Message });
            }
        }).RequirePermission(BackendPermissionKeys.CallExecute)
            .RequireWorkspaceScope();
    }

    private static async Task StreamCallEventsAsync(
        HttpContext context,
        CallEventBroadcaster broadcaster,
        string workspaceKey,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Append("X-Accel-Buffering", "no");

        using var subscription = broadcaster.Subscribe(workspaceKey);
        await context.Response.Body.FlushAsync(cancellationToken);

        try
        {
            await foreach (var callEvent in subscription.Reader.ReadAllAsync(cancellationToken))
            {
                var payload = JsonSerializer.Serialize(callEvent, JsonSerializerOptions.Web);
                await context.Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
                await context.Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The consumer disconnected; ending the stream is the expected outcome.
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Surfaces;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Api.Surface;
using Xunit;

namespace Callora.Core.Tests.Communication.Surface;

/// <summary>
/// What a phone panel on a surface may do. The live state arrives as context; this is the other
/// direction — the commands, which a browser cannot publish and must therefore ask for.
/// </summary>
public sealed class SurfaceCallApiTests
{
    private const string Workspace = "ws-a";
    private const string SurfaceKey = "agent-desk";

    // ── Who may act at all ──────────────────────────────────────────────────

    [Fact]
    public async Task AGuest_MayNotEvenLook()
    {
        // Der Kern des Unterschieds zu den meisten Surface-Routen: Die Anrufe des Workspaces sind
        // nicht die Daten des Aufrufers.
        var handler = new SurfaceListCallsRouteHandler(new FakeHistory());

        var response = await handler.HandleAsync(Request("GET", "calls", caller: Guest()));

        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task AnAuthenticatedVisitorWithoutTheClaim_IsRefusedWithTheReason()
    {
        // Ein Kundenportal authentifiziert genauso wahrhaftig wie ein Arbeitsplatz. Ohne den Anspruch
        // bekäme ein Kunde die Telefonliste des Unternehmens, dessen Kunde er ist.
        var handler = new SurfaceListCallsRouteHandler(new FakeHistory());

        var response = await handler.HandleAsync(Request("GET", "calls", caller: Authenticated()));

        Assert.Equal(403, response.StatusCode);
        Assert.Contains(SurfaceCallAccess.ClaimKey, JsonSerializer.Serialize(response.Payload), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadingIsEnoughToRead_ButNotToAct()
    {
        var history = new FakeHistory(Call("c1"));
        var calls = new RecordingCallControl();

        Assert.Equal(200, (await new SurfaceListCallsRouteHandler(history)
            .HandleAsync(Request("GET", "calls", caller: Authenticated(SurfaceCallAccess.Read)))).StatusCode);

        Assert.Equal(403, (await new SurfaceCallCommandRouteHandler(calls, SurfaceCallCommand.Hangup)
            .HandleAsync(Request("POST", "calls/c1/hangup", caller: Authenticated(SurfaceCallAccess.Read),
                routeValues: new() { ["callId"] = "c1" }))).StatusCode);
    }

    [Fact]
    public async Task ManagingImpliesReading()
    {
        // Sonst wäre es eine Unterscheidung ohne Bedeutung: Wer auflegen darf, darf sehen, was er auflegt.
        var handler = new SurfaceListCallsRouteHandler(new FakeHistory());

        var response = await handler.HandleAsync(
            Request("GET", "calls", caller: Authenticated(SurfaceCallAccess.Manage)));

        Assert.Equal(200, response.StatusCode);
    }

    // ── Lesen ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task TheHistoryComesBackNewestFirst()
    {
        var handler = new SurfaceListCallsRouteHandler(new FakeHistory(Call("c1"), Call("c2")));

        var response = await handler.HandleAsync(
            Request("GET", "calls", caller: Authenticated(SurfaceCallAccess.Read)));

        Assert.Equal(2, Assert.IsAssignableFrom<IReadOnlyList<CallHistoryEntry>>(response.Payload).Count);
    }

    [Fact]
    public async Task TheActiveListIsWhatTheBlockStartsFrom()
    {
        // Kontext meldet Änderungen; wer die Seite neu lädt, während ein Gespräch läuft, braucht
        // einen Anfangszustand.
        var calls = new RecordingCallControl { Active = [new CallSnapshot("c1", CallDirection.Inbound, CallState.Connected, "+4917012345678")] };
        var handler = new SurfaceListActiveCallsRouteHandler(calls);

        var response = await handler.HandleAsync(
            Request("GET", "calls/active", caller: Authenticated(SurfaceCallAccess.Read)));

        Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<CallSnapshot>>(response.Payload));
    }

    // ── Handeln ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(SurfaceCallCommand.Accept, "accept")]
    [InlineData(SurfaceCallCommand.Reject, "reject")]
    [InlineData(SurfaceCallCommand.Hangup, "hangup")]
    public async Task ACommandReachesTheCall(SurfaceCallCommand command, string expected)
    {
        var calls = new RecordingCallControl();
        var handler = new SurfaceCallCommandRouteHandler(calls, command);

        var response = await handler.HandleAsync(Request("POST", $"calls/c1/{expected}",
            caller: Authenticated(SurfaceCallAccess.Manage), routeValues: new() { ["callId"] = "c1" }));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal([$"{expected}:c1"], calls.Actions);
    }

    [Fact]
    public async Task ACommandForACallThatIsGone_IsNotFound()
    {
        var calls = new RecordingCallControl { Succeeds = false };
        var handler = new SurfaceCallCommandRouteHandler(calls, SurfaceCallCommand.Hangup);

        var response = await handler.HandleAsync(Request("POST", "calls/c1/hangup",
            caller: Authenticated(SurfaceCallAccess.Manage), routeValues: new() { ["callId"] = "c1" }));

        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task DigitsReachTheCall()
    {
        var calls = new RecordingCallControl();
        var handler = new SurfaceSendDtmfRouteHandler(calls);

        var response = await handler.HandleAsync(Request("POST", "calls/c1/dtmf",
            caller: Authenticated(SurfaceCallAccess.Manage),
            routeValues: new() { ["callId"] = "c1" },
            body: new { tones = "12#" }));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(["dtmf:c1:12#"], calls.Actions);
    }

    [Fact]
    public async Task DiallingUsesTheSurfaceAsItsOrigin()
    {
        // Die Herkunft kommt NICHT aus dem Body: Ein Browser, der sie benennen dürfte, könnte jedes
        // Kontingent umgehen, indem er sich umbenennt. Ein Plugin ist vertrauenswürdig (ADR-013),
        // eine Seite im Browser ist es nicht.
        var calls = new RecordingCallControl();
        var handler = new SurfacePlaceCallRouteHandler(calls);

        await handler.HandleAsync(Request("POST", "calls",
            caller: Authenticated(SurfaceCallAccess.Manage),
            body: new { to = "+4930123", origin = "crm" }));

        Assert.Equal($"surface:{SurfaceKey}", calls.LastOrigin);
    }

    [Fact]
    public async Task DiallingNowhere_IsRefused()
    {
        var handler = new SurfacePlaceCallRouteHandler(new RecordingCallControl());

        var response = await handler.HandleAsync(Request("POST", "calls",
            caller: Authenticated(SurfaceCallAccess.Manage), body: new { to = "  " }));

        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task AnExhaustedQuota_IsAnAnswerRatherThanACrash()
    {
        // Der Wählplan lehnt mit InvalidOperationException ab; dem Panel muss ein Satz übrig bleiben.
        var handler = new SurfacePlaceCallRouteHandler(new RecordingCallControl { PlaceThrows = true });

        var response = await handler.HandleAsync(Request("POST", "calls",
            caller: Authenticated(SurfaceCallAccess.Manage), body: new { to = "+4930123" }));

        Assert.Equal(409, response.StatusCode);
    }

    // ── Helper ──────────────────────────────────────────────────────────────

    private static SurfaceCaller Guest() =>
        new GuestSurfaceCaller(new SurfaceSubject("callora.surface-guest", "g1"));

    private static SurfaceCaller Authenticated(params string[] callClaims) =>
        new AuthenticatedSurfaceCaller(
            new SurfaceSubject("crm", "u1"),
            new SurfaceIdentity(
                "Alice",
                callClaims.Length == 0
                    ? new Dictionary<string, IReadOnlyList<string>>()
                    : new Dictionary<string, IReadOnlyList<string>> { [SurfaceCallAccess.ClaimKey] = callClaims },
                "password",
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch.AddHours(8)));

    private static CallHistoryEntry Call(string id) =>
        new(id, "Inbound", "+4917012345678", "+493012345678", DateTimeOffset.UnixEpoch, null, null, 0, "Completed", null, []);

    private static HostSurfaceApiRequest Request(
        string method,
        string path,
        SurfaceCaller caller,
        Dictionary<string, string>? routeValues = null,
        object? body = null) =>
        new(
            "communication",
            method,
            path,
            routeValues ?? [],
            new Dictionary<string, string[]>(),
            body is null ? null : JsonSerializer.SerializeToElement(body),
            RequestId: "req-1",
            TenantKey: "tenant",
            WorkspaceKey: Workspace,
            SurfaceKey: SurfaceKey,
            Caller: caller);

    private sealed class FakeHistory(params CallHistoryEntry[] calls) : ICallHistory
    {
        public Task<IReadOnlyList<CallHistoryEntry>> ListRecentAsync(
            string workspaceKey, int limit, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CallHistoryEntry>>([.. calls]);
    }

    private sealed class RecordingCallControl : ICallControlService
    {
        public List<string> Actions { get; } = [];

        public IReadOnlyList<CallSnapshot> Active { get; init; } = [];

        public bool Succeeds { get; init; } = true;

        public bool PlaceThrows { get; init; }

        public string? LastOrigin { get; private set; }

        public Task<bool> AcceptAsync(string workspaceKey, string callId, CancellationToken ct = default) =>
            Record("accept", callId);

        public Task<bool> RejectAsync(string workspaceKey, string callId, CancellationToken ct = default) =>
            Record("reject", callId);

        public Task<bool> HangupAsync(string workspaceKey, string callId, CancellationToken ct = default) =>
            Record("hangup", callId);

        public Task<bool> SendDtmfAsync(string workspaceKey, string callId, string tones, CancellationToken ct = default)
        {
            Actions.Add($"dtmf:{callId}:{tones}");
            return Task.FromResult(Succeeds);
        }

        public Task<CallSnapshot> PlaceCallAsync(PlaceCallCommand command, CancellationToken ct = default)
        {
            LastOrigin = command.Origin;
            if (PlaceThrows)
            {
                throw new InvalidOperationException("no lines left");
            }

            return Task.FromResult(new CallSnapshot("new", CallDirection.Outbound, CallState.Connecting, command.To));
        }

        public CallSnapshot? Get(string workspaceKey, string callId) => null;

        public IReadOnlyList<CallSnapshot> ListActive(string workspaceKey) => Active;

        public Task<IReadOnlyList<CallHistoryEntry>> ListRecentAsync(
            string workspaceKey, int limit, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CallHistoryEntry>>([]);

        private Task<bool> Record(string action, string callId)
        {
            Actions.Add($"{action}:{callId}");
            return Task.FromResult(Succeeds);
        }
    }
}

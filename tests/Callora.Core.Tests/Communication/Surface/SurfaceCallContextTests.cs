using System;
using System.Collections.Generic;
using System.Linq;
using Callora.Core.Application.Surfaces.Contracts;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Api.Surface;
using Callora.Plugin.Communication.Application.Calls;
using Xunit;

namespace Callora.Core.Tests.Communication.Surface;

/// <summary>
/// What a surface block learns about the telephone without asking. A block declares that it needs
/// <c>communication.active-call/v1</c> and updates when a call arrives; nobody writes a socket, a
/// reconnect or a message format.
/// </summary>
public sealed class SurfaceCallContextTests
{
    private const string Workspace = "ws-a";

    [Fact]
    public void ARingingInboundCall_BecomesTheIncomingCallContext()
    {
        var surface = new RecordingBroadcaster();
        var publisher = new SurfaceCallContextPublisher(surface);

        publisher.Publish(Ringing("call-1", "+4917012345678"));

        var published = Assert.Single(surface.Published, p => p.Key == SurfaceCallContextKeys.IncomingCall);
        Assert.Equal(Workspace, published.Address.WorkspaceKey);
        var value = Assert.IsType<SurfaceCallView>(published.Value);
        Assert.Equal(("call-1", "+4917012345678", "Inbound"), (value.CallId, value.RemoteParty, value.Direction));
    }

    [Fact]
    public void ARingingOutboundCall_IsNotAnIncomingOne()
    {
        // Ein selbst gewählter Anruf klingelt auch — beim anderen. Ihn als eingehend zu melden würde
        // das Panel bei jedem Wählversuch aufblinken lassen.
        var surface = new RecordingBroadcaster();
        var publisher = new SurfaceCallContextPublisher(surface);

        publisher.Publish(new CallEventNotification(
            CallEventTypes.Placed, Workspace, "call-1", "Outbound", "Connecting", "+49301", At(0)));

        Assert.DoesNotContain(surface.Published, p => p.Key == SurfaceCallContextKeys.IncomingCall);
    }

    [Fact]
    public void AnAnsweredCall_StopsBeingIncomingAndBecomesActive()
    {
        // Beides in einem Schritt: Ein Panel, das den eingehenden Anruf stehen lässt, während das
        // Gespräch schon läuft, zeigt zwei Wahrheiten gleichzeitig.
        var surface = new RecordingBroadcaster();
        var publisher = new SurfaceCallContextPublisher(surface);
        publisher.Publish(Ringing("call-1", "+4917012345678"));

        publisher.Publish(Connected("call-1", "+4917012345678"));

        Assert.Null(surface.Last(SurfaceCallContextKeys.IncomingCall));
        Assert.NotNull(surface.Last(SurfaceCallContextKeys.ActiveCall));
    }

    [Fact]
    public void AnEndedCall_ClearsWhatItLeftBehind()
    {
        var surface = new RecordingBroadcaster();
        var publisher = new SurfaceCallContextPublisher(surface);
        publisher.Publish(Ringing("call-1", "+4917012345678"));
        publisher.Publish(Connected("call-1", "+4917012345678"));

        publisher.Publish(Ended("call-1", "+4917012345678"));

        Assert.Null(surface.Last(SurfaceCallContextKeys.IncomingCall));
        Assert.Null(surface.Last(SurfaceCallContextKeys.ActiveCall));
    }

    [Fact]
    public void AnotherCallEnding_DoesNotClearTheOneInProgress()
    {
        // Zwei Anrufe, einer legt auf: Ohne diese Prüfung räumt der Abschied des einen das Panel des
        // anderen leer — und der Agent sitzt in einem Gespräch, das laut Oberfläche nicht existiert.
        var surface = new RecordingBroadcaster();
        var publisher = new SurfaceCallContextPublisher(surface);
        publisher.Publish(Connected("call-1", "+4917012345678"));

        publisher.Publish(Ended("call-2", "+4930999"));

        Assert.NotNull(surface.Last(SurfaceCallContextKeys.ActiveCall));
    }

    [Fact]
    public void TheContextGoesToTheWorkspace_AndNoFurther()
    {
        // Noch nicht an ein Subject: Solange ein Anruf niemandem zugeordnet ist, wäre jede engere
        // Adresse geraten. Die Verengung kommt mit der Zuordnung, ohne dass ein Block sich ändert.
        var surface = new RecordingBroadcaster();
        var publisher = new SurfaceCallContextPublisher(surface);

        publisher.Publish(Ringing("call-1", "+4917012345678"));

        var address = surface.Published[0].Address;
        Assert.Equal(Workspace, address.WorkspaceKey);
        Assert.Null(address.SurfaceKey);
        Assert.Null(address.SubjectId);
    }

    [Fact]
    public void TheInnerPublisherStillSeesEverything()
    {
        // Der Kontext tritt neben den Live-Ereignisstrom, er ersetzt ihn nicht: Der Wählplan-Client
        // und das Ereignis-WebSocket hängen weiter daran.
        var inner = new CountingPublisher();
        var publisher = new SurfaceCallContextPublisher(new RecordingBroadcaster(), inner);

        publisher.Publish(Ringing("call-1", "+4917012345678"));

        Assert.Equal(1, inner.Count);
    }

    [Fact]
    public void ABrokenSurfaceDoesNotBreakTheCall()
    {
        // Veröffentlicht wird auf dem Pfad, der gerade einen Anruf bedient. Eine Ausnahme von dort
        // wäre ein verlorener Anruf wegen eines Panels.
        var publisher = new SurfaceCallContextPublisher(new ThrowingBroadcaster());

        publisher.Publish(Ringing("call-1", "+4917012345678"));
    }

    private static DateTimeOffset At(int seconds) => DateTimeOffset.UnixEpoch.AddSeconds(seconds);

    [Fact]
    public void WhatTheNetworkSaidAboutTheCaller_ReachesThePanel()
    {
        // Ohne das zeigt ein Panel eine Ziffernfolge, obwohl das Netz „Praxis Dr. Meier,
        // weitergeleitet von der Zentrale" gesagt hat — und der Anrufer wartet, während jemand
        // Ziffern liest.
        var surface = new RecordingBroadcaster();
        var publisher = new SurfaceCallContextPublisher(surface);

        publisher.Publish(new CallEventNotification(
            CallEventTypes.Ringing, Workspace, "call-1", "Inbound", "Ringing", "+4917012345678", At(0),
            new InboundCallIdentity(
                CalledNumber: "+493012345678",
                CallerNumber: "+4917012345678",
                CallerDisplayName: "Praxis Dr. Meier",
                AssertedIdentity: "+4917012345678",
                DivertedFrom: "+493099999")));

        var view = Assert.IsType<SurfaceCallView>(surface.Last(SurfaceCallContextKeys.IncomingCall));
        Assert.Equal("Praxis Dr. Meier", view.CallerName);
        Assert.Equal("+493012345678", view.CalledNumber);
        Assert.Equal("+493099999", view.DivertedFrom);
        Assert.True(view.Verified);
    }

    [Fact]
    public void WithoutAVouchedIdentity_NothingIsClaimedAsVerified()
    {
        // Der Haken ist eine Aussage über Vertrauen. Ihn zu setzen, weil eine Nummer da ist, wäre
        // genau die Art Bequemlichkeit, die eine Anzeige wertlos macht.
        var surface = new RecordingBroadcaster();
        var publisher = new SurfaceCallContextPublisher(surface);

        publisher.Publish(new CallEventNotification(
            CallEventTypes.Ringing, Workspace, "call-1", "Inbound", "Ringing", "+4917012345678", At(0),
            new InboundCallIdentity(CallerNumber: "+4917012345678")));

        var view = Assert.IsType<SurfaceCallView>(surface.Last(SurfaceCallContextKeys.IncomingCall));
        Assert.False(view.Verified);
        Assert.Null(view.CallerName);
    }

    private static CallEventNotification Ringing(string callId, string remoteParty) =>
        new(CallEventTypes.Ringing, Workspace, callId, "Inbound", "Ringing", remoteParty, At(0));

    private static CallEventNotification Connected(string callId, string remoteParty) =>
        new(CallEventTypes.StateChanged, Workspace, callId, "Inbound", "Connected", remoteParty, At(5));

    private static CallEventNotification Ended(string callId, string remoteParty) =>
        new(CallEventTypes.Ended, Workspace, callId, "Inbound", "Terminated", remoteParty, At(60));

    private sealed class RecordingBroadcaster : ISurfaceContextBroadcaster
    {
        public List<(SurfaceContextAddress Address, string Key, object? Value)> Published { get; } = [];

        public void Publish(SurfaceContextAddress address, string key, object? value) =>
            Published.Add((address, key, value));

        /// <summary>The value a subscriber would currently hold for the key.</summary>
        public object? Last(string key) =>
            Published.LastOrDefault(p => p.Key == key).Value;
    }

    private sealed class ThrowingBroadcaster : ISurfaceContextBroadcaster
    {
        public void Publish(SurfaceContextAddress address, string key, object? value) =>
            throw new InvalidOperationException("the surface is gone");
    }

    private sealed class CountingPublisher : ICallEventPublisher
    {
        public int Count { get; private set; }

        public void Publish(CallEventNotification notification) => Count++;
    }
}

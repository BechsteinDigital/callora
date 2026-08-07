using Callora.Core.Application.Surfaces;
using Xunit;
using Callora.Core.Application.Surfaces.Contracts;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// Who receives a published context value. The address decides it on the server, because
/// everything that reaches a tab is readable there — a filter in the browser would be
/// decoration, not a boundary (design §5.5 P1).
/// </summary>
public sealed class SurfaceContextBroadcasterTests
{
    private const string Key = "communication.active-call/v1";

    [Fact]
    public void AWorkspaceAddressReachesEverySurfaceInIt()
    {
        var broadcaster = new SurfaceContextBroadcaster();
        using var portal = broadcaster.Subscribe("acme", "portal", null, null);
        using var desk = broadcaster.Subscribe("acme", "agent-desk", null, null);

        broadcaster.Publish(new SurfaceContextAddress("acme"), Key, "ringing");

        Assert.Equal("ringing", Received(portal));
        Assert.Equal("ringing", Received(desk));
    }

    [Fact]
    public void AnotherWorkspaceNeverReceivesIt()
    {
        var broadcaster = new SurfaceContextBroadcaster();
        using var ours = broadcaster.Subscribe("acme", "portal", null, null);
        using var theirs = broadcaster.Subscribe("globex", "portal", null, null);

        broadcaster.Publish(new SurfaceContextAddress("acme"), Key, "ringing");

        Assert.Equal("ringing", Received(ours));
        Assert.Null(Received(theirs));
    }

    [Fact]
    public void NamingASurfaceExcludesTheOthers()
    {
        var broadcaster = new SurfaceContextBroadcaster();
        using var desk = broadcaster.Subscribe("acme", "agent-desk", null, null);
        using var portal = broadcaster.Subscribe("acme", "portal", null, null);

        broadcaster.Publish(new SurfaceContextAddress("acme", "agent-desk"), Key, "ringing");

        Assert.Equal("ringing", Received(desk));
        Assert.Null(Received(portal));
    }

    [Fact]
    public void ASubjectAddressReachesOnlyThatVisitor()
    {
        // Ein aktiver Anruf gehört der Agentin, die ihn führt — nicht jedem, der dieselbe
        // Fläche offen hat.
        var broadcaster = new SurfaceContextBroadcaster();
        using var anna = broadcaster.Subscribe("acme", "agent-desk", "idp", "anna");
        using var bert = broadcaster.Subscribe("acme", "agent-desk", "idp", "bert");

        broadcaster.Publish(
            new SurfaceContextAddress("acme", "agent-desk", "idp", "anna"), Key, "ringing");

        Assert.Equal("ringing", Received(anna));
        Assert.Null(Received(bert));
    }

    [Fact]
    public void TheSameSubjectIdFromAnotherIssuerIsAnotherPerson()
    {
        // Eine Subject-Id allein ist keine Identität (ADR-017). Ohne Issuer-Vergleich läse
        // "anna" beim Kunden-Identitätsanbieter mit, was "anna" beim Mitarbeiter-Anbieter sieht.
        var broadcaster = new SurfaceContextBroadcaster();
        using var employee = broadcaster.Subscribe("acme", "agent-desk", "employees", "anna");
        using var customer = broadcaster.Subscribe("acme", "agent-desk", "customers", "anna");

        broadcaster.Publish(
            new SurfaceContextAddress("acme", "agent-desk", "employees", "anna"), Key, "ringing");

        Assert.Equal("ringing", Received(employee));
        Assert.Null(Received(customer));
    }

    [Fact]
    public void AnAnonymousConnectionReceivesNoSubjectScopedValue()
    {
        var broadcaster = new SurfaceContextBroadcaster();
        using var guest = broadcaster.Subscribe("acme", "portal", null, null);

        broadcaster.Publish(
            new SurfaceContextAddress("acme", "portal", "idp", "anna"), Key, "ringing");

        Assert.Null(Received(guest));
    }

    [Fact]
    public void AnAnonymousConnectionStillReceivesASurfaceWideValue()
    {
        // Eine Warteschlangenlänge gehört niemandem persönlich.
        var broadcaster = new SurfaceContextBroadcaster();
        using var guest = broadcaster.Subscribe("acme", "portal", null, null);

        broadcaster.Publish(new SurfaceContextAddress("acme", "portal"), "queue.length/v1", 3);

        Assert.Equal(3, Received(guest, "queue.length/v1"));
    }

    [Fact]
    public void ANullValueIsDeliveredSoASubscriberCanClearItsKey()
    {
        var broadcaster = new SurfaceContextBroadcaster();
        using var desk = broadcaster.Subscribe("acme", "agent-desk", null, null);

        broadcaster.Publish(new SurfaceContextAddress("acme"), Key, null);

        Assert.True(desk.Messages.TryRead(out var message));
        Assert.Equal(Key, message!.Key);
        Assert.Null(message.Value);
    }

    [Fact]
    public void ADisposedSubscriptionStopsReceivingAndReleasesItsSlot()
    {
        var broadcaster = new SurfaceContextBroadcaster();
        var desk = broadcaster.Subscribe("acme", "agent-desk", null, null);
        Assert.Equal(1, broadcaster.SubscriptionCount);

        desk.Dispose();
        broadcaster.Publish(new SurfaceContextAddress("acme"), Key, "ringing");

        Assert.Equal(0, broadcaster.SubscriptionCount);
        Assert.False(desk.Messages.TryRead(out _));
    }

    [Fact]
    public void ASlowConnectionLosesItsOldestValuesRatherThanSlowingThePublisher()
    {
        // Der Anruf darf nicht auf einen hängenden Tab warten. Kontext ist UI-Zustand: eine
        // Lücke kostet ein veraltetes Panel bis zum nächsten Wert, keine Korrektheit.
        var broadcaster = new SurfaceContextBroadcaster();
        using var slow = broadcaster.Subscribe("acme", "portal", null, null);

        for (var i = 0; i < SurfaceContextBroadcaster.ConnectionQueueCapacity + 10; i++)
        {
            broadcaster.Publish(new SurfaceContextAddress("acme"), Key, i);
        }

        var received = new List<object?>();
        while (slow.Messages.TryRead(out var message))
        {
            received.Add(message.Value);
        }

        Assert.Equal(SurfaceContextBroadcaster.ConnectionQueueCapacity, received.Count);
        // Die NEUESTEN bleiben — ein Abonnent will den aktuellen Zustand, nicht den ältesten.
        Assert.Equal(SurfaceContextBroadcaster.ConnectionQueueCapacity + 9, received[^1]);
    }

    private static object? Received(SurfaceContextSubscription subscription, string key = Key)
    {
        while (subscription.Messages.TryRead(out var message))
        {
            if (string.Equals(message.Key, key, StringComparison.Ordinal))
            {
                return message.Value;
            }
        }

        return null;
    }
}

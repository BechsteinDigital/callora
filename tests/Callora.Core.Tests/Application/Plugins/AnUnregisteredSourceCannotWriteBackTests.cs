using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Tests.Support;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Callora.Core.Tests.Application.Plugins;

/// <summary>
/// Was passiert, wenn eine Capability-Meldung die Registry erreicht, nachdem ihr Plugin abgemeldet ist.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IRuntimeCapabilitySource.CapabilitiesChanged"/> darf laut Vertrag aus jedem Thread kommen.
/// <c>Unregister</c> hängt den Abonnenten ab, aber ein Aufruf, der dabei schon unterwegs war, wartet auf
/// das Lock der Registry und läuft danach weiter. Ohne Prüfung legte er einen Eintrag für ein Plugin an,
/// das es aus Sicht der Registry nicht mehr gibt — und <c>IsSatisfied</c> meldete es dauerhaft als
/// erfüllt, weil nichts mehr existiert, das den Eintrag je wieder aufräumen würde.
/// </para>
/// <para>
/// Der teure Teil daran ist nicht der falsche Eintrag, sondern seine Wirkung: Der Capability-Guard
/// liest ihn, ein deaktiviertes Plugin gilt weiter als verfügbar, und die Ursache liegt eine Ebene
/// tiefer als der Ort, an dem es auffällt. Dasselbe Muster wie der stehengebliebene Export in #253.
/// </para>
/// </remarks>
public sealed class AnUnregisteredSourceCannotWriteBackTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);

    [Fact]
    public void AChangeArrivingAfterUnregisterIsDropped()
    {
        var (registry, flips, _) = NewRegistry();
        var source = new DetachIgnoringRuntimeCapabilitySource();
        registry.Register("comm", source);

        registry.Unregister("comm");
        flips.Clear();

        // Der Nachzügler: für die Registry ist das Plugin weg, für die Quelle nicht.
        source.Raise("comm.voice", "ws-1", satisfied: true);

        Assert.False(registry.IsSatisfied("comm", "comm.voice", "ws-1"));
        Assert.Empty(flips);
    }

    /// <summary>
    /// Der Fall, der die Prüfung auf die Instanz statt nur auf die Id nötig macht: Zwischen Abmelden
    /// und Nachzügler kann dasselbe Plugin längst mit einer neuen Quelle registriert sein — etwa nach
    /// einem Neuladen. Der Nachzügler der alten Quelle darf deren Zustand nicht überschreiben.
    /// </summary>
    [Fact]
    public void AChangeFromAReplacedSourceDoesNotOverwriteTheNewOne()
    {
        var (registry, flips, time) = NewRegistry();
        var stale = new DetachIgnoringRuntimeCapabilitySource();
        registry.Register("comm", stale);
        registry.Unregister("comm");

        var current = new DetachIgnoringRuntimeCapabilitySource(new RuntimeCapabilityGrant("comm.voice", "ws-1"));
        registry.Register("comm", current);
        flips.Clear();

        stale.Raise("comm.voice", "ws-1", satisfied: false);

        // Über die Gnadenfrist hinaus: Ein „nicht mehr erfüllt" kippt nicht sofort, sondern startet
        // einen Timer. Ohne die Vorlaufzeit bestünde dieser Test auch mit dem Fehler — er sähe nur
        // den noch nicht abgelaufenen Timer und hielte ihn für die Zurückweisung.
        time.Advance(Grace);

        Assert.True(registry.IsSatisfied("comm", "comm.voice", "ws-1"));
        Assert.Empty(flips);
    }

    /// <summary>
    /// Die Gegenprobe zur Prüfung: Die registrierte Quelle wird weiterhin gehört. Ohne diesen Test
    /// bestünde der obige auch dann, wenn die Registry gar keine Meldung mehr annähme.
    /// </summary>
    [Fact]
    public void TheRegisteredSourceIsStillHeard()
    {
        var (registry, flips, _) = NewRegistry();
        var source = new DetachIgnoringRuntimeCapabilitySource();
        registry.Register("comm", source);
        flips.Clear();

        source.Raise("comm.voice", "ws-1", satisfied: true);

        Assert.True(registry.IsSatisfied("comm", "comm.voice", "ws-1"));
        Assert.Equal(new RuntimeCapabilityFlip("comm", "comm.voice", "ws-1", true), Assert.Single(flips));
    }

    private static (RuntimeCapabilityRegistry Registry, List<RuntimeCapabilityFlip> Flips, FakeTimeProvider Time) NewRegistry()
    {
        var time = new FakeTimeProvider();
        var registry = new RuntimeCapabilityRegistry(Grace, time);
        var flips = new List<RuntimeCapabilityFlip>();
        registry.EffectiveChanged += flips.Add;
        return (registry, flips, time);
    }
}

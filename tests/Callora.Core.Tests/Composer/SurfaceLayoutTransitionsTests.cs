using Callora.Plugin.Composer.Domain;
using Xunit;

namespace Callora.Core.Tests.Composer;

/// <summary>
/// Die Zustandsübergänge eines Layouts. Ohne Datenbank geprüft — die Regeln sind der Teil, der
/// zählt, und sie sollten dafür kein Postgres brauchen.
/// </summary>
public sealed class SurfaceLayoutTransitionsTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 7, 10, 0, 0, TimeSpan.Zero);

    private const string Empty = """{"sections":[]}""";

    private static SurfaceLayoutVersion Draft(string document = "{\"v\":1}", int number = 1) =>
        SurfaceLayoutVersion.NewDraft("portal", number, document, "anna", T0);

    // ── Veröffentlichen ──────────────────────────────────────────────────────

    [Fact]
    public void PublishingMakesTheDraftLiveAndOpensTheNextOne()
    {
        // Ohne den neuen Entwurf hätte die nächste Bearbeitung nichts, worin sie schreiben könnte.
        var draft = Draft();

        var next = SurfaceLayoutTransitions.Publish(draft, null, "anna", "Herbstseite", T0.AddHours(1));

        Assert.Equal(SurfaceLayoutState.Published, draft.State);
        Assert.Equal("Herbstseite", draft.Label);
        Assert.Equal(SurfaceLayoutState.Draft, next.State);
        Assert.Equal(2, next.VersionNumber);
        Assert.Equal(draft.Document, next.Document);
    }

    [Fact]
    public void PublishingArchivesWhatWasLive()
    {
        var live = Draft("{\"alt\":true}");
        SurfaceLayoutTransitions.Publish(live, null, "anna", null, T0);

        var draft = Draft("{\"neu\":true}", number: 2);
        SurfaceLayoutTransitions.Publish(draft, live, "anna", null, T0.AddHours(1));

        Assert.Equal(SurfaceLayoutState.Archived, live.State);
        Assert.Equal(SurfaceLayoutState.Published, draft.State);
    }

    [Fact]
    public void OnlyADraftCanBePublished()
    {
        var live = Draft();
        SurfaceLayoutTransitions.Publish(live, null, "anna", null, T0);

        Assert.Throws<ArgumentException>(
            () => SurfaceLayoutTransitions.Publish(live, null, "anna", null, T0.AddHours(1)));
    }

    // ── Verwerfen ────────────────────────────────────────────────────────────

    [Fact]
    public void DiscardingRebuildsTheDraftFromWhatIsLive()
    {
        var live = Draft("{\"live\":true}");
        SurfaceLayoutTransitions.Publish(live, null, "anna", null, T0);

        var draft = Draft("{\"verworfen\":true}", number: 2);
        SurfaceLayoutTransitions.Discard(draft, live, Empty, "anna", T0.AddHours(1));

        Assert.Equal("{\"live\":true}", draft.Document);
        Assert.Equal(SurfaceLayoutState.Draft, draft.State);
    }

    [Fact]
    public void DiscardingWithNothingPublishedLeavesAnEmptyDraft()
    {
        // Verwerfen darf ein Layout nicht ohne Entwurf zurücklassen — dann wäre es unbearbeitbar.
        var draft = Draft("{\"verworfen\":true}");

        SurfaceLayoutTransitions.Discard(draft, null, Empty, "anna", T0.AddHours(1));

        Assert.Equal(Empty, draft.Document);
    }

    // ── Rückrollen ───────────────────────────────────────────────────────────

    [Fact]
    public void RollingBackGoesIntoTheDraftAndNotStraightToLive()
    {
        // Ein Rückrollen ist ein Vorschlag wie jede andere Bearbeitung. Direkt live zu gehen wäre
        // der eine Weg, auf dem niemand das Ergebnis ansieht, bevor ein Besucher es tut.
        var alt = Draft("{\"alt\":true}");
        SurfaceLayoutTransitions.Publish(alt, null, "anna", null, T0);
        alt.Archive(T0.AddMinutes(5));

        var draft = Draft("{\"aktuell\":true}", number: 3);
        SurfaceLayoutTransitions.RollBack(draft, alt, "anna", T0.AddHours(1));

        Assert.Equal("{\"alt\":true}", draft.Document);
        Assert.Equal(SurfaceLayoutState.Draft, draft.State);
        Assert.Equal(SurfaceLayoutState.Archived, alt.State);
    }

    [Fact]
    public void RollingBackToADraftIsRefused()
    {
        var draft = Draft();
        var anderer = Draft(number: 2);

        Assert.Throws<InvalidOperationException>(
            () => SurfaceLayoutTransitions.RollBack(draft, anderer, "anna", T0));
    }

    // ── Autosave ─────────────────────────────────────────────────────────────

    [Fact]
    public void AutosaveCreatesNoVersion()
    {
        // Sonst wäre die Historie ein Protokoll von Tastenanschlägen, und Rückrollen hieße
        // raten, welcher von vierhundert Einträgen gemeint war.
        var draft = Draft();
        var before = draft.VersionNumber;

        SurfaceLayoutTransitions.TryAutosave(draft, "{\"neu\":1}", draft.ChangedAtUtc, T0.AddMinutes(1));

        Assert.Equal(before, draft.VersionNumber);
        Assert.Equal(SurfaceLayoutState.Draft, draft.State);
        Assert.Equal("{\"neu\":1}", draft.Document);
    }

    [Fact]
    public void ASecondWriterWithAStaleStampIsRefused()
    {
        var draft = Draft();
        var stamp = draft.ChangedAtUtc;

        Assert.True(SurfaceLayoutTransitions.TryAutosave(draft, "{\"a\":1}", stamp, T0.AddMinutes(1)));

        // Der zweite Editor kennt den Stand von vorhin.
        Assert.False(SurfaceLayoutTransitions.TryAutosave(draft, "{\"b\":2}", stamp, T0.AddMinutes(2)));
        Assert.Equal("{\"a\":1}", draft.Document);
    }

    [Fact]
    public void AutosaveMovesTheStampSoTheNextWriteCanBeChecked()
    {
        var draft = Draft();

        SurfaceLayoutTransitions.TryAutosave(draft, "{\"a\":1}", draft.ChangedAtUtc, T0.AddMinutes(1));

        Assert.Equal(T0.AddMinutes(1), draft.ChangedAtUtc);
        Assert.True(SurfaceLayoutTransitions.TryAutosave(
            draft, "{\"b\":2}", draft.ChangedAtUtc, T0.AddMinutes(2)));
    }

    [Fact]
    public void APublishedVersionCannotBeAutosavedInto()
    {
        // Der öffentliche Renderpfad liest sie; eine Bearbeitung ginge sofort live.
        var live = Draft();
        SurfaceLayoutTransitions.Publish(live, null, "anna", null, T0);

        Assert.Throws<ArgumentException>(
            () => SurfaceLayoutTransitions.TryAutosave(live, "{\"x\":1}", live.ChangedAtUtc, T0));
    }
}

using Callora.Core.Application.Surfaces.SharedContext;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// The seven principles of design §5.5, as tests rather than as intentions. Shared context
/// carries personal data across surface boundaries; everything that reaches a browser is readable
/// there, so every one of these rules has to hold on this side or not at all.
/// </summary>
public sealed class SharedContextStoreTests
{
    private const string CallKey = "communication.active-call/v1";

    private static SharedContextKeyDeclaration CallDeclaration() => new(
        CallKey,
        SharedContextAnchorType.Conversation,
        Purpose: "Beide Seiten eines laufenden Gesprächs zeigen dessen Zustand an.",
        Fields:
        [
            new("state", SharedContextVisibility.Participant, "Klingelt, verbunden, beendet."),
            new("durationSeconds", SharedContextVisibility.Participant, "Gesprächsdauer."),
            new("customerRecord", SharedContextVisibility.Owner, "Die Kundenakte zum Anruf."),
        ],
        TimeToLive: TimeSpan.FromMinutes(30),
        PublisherPluginId: "communication");

    private static SharedContextStore Store(
        FakeTimeProvider? time = null,
        params SharedContextKeyDeclaration[] declarations) =>
        new(declarations.Length > 0 ? declarations : [CallDeclaration()], time);

    private static Dictionary<string, object?> Call() => new(StringComparer.Ordinal)
    {
        ["state"] = "connected",
        ["durationSeconds"] = 42,
        ["customerRecord"] = new { Name = "Meier", Debt = 1200 },
    };

    // ── P1: der Server projiziert, der Client filtert nie ────────────────────

    [Fact]
    public void P1_TheOwnerSeesEveryDeclaredField()
    {
        var store = Store();
        var anchor = SharedContextAnchor.ForConversation("call-1");
        store.SetParticipants(anchor, [new("idp", "agent", SharedContextVisibility.Owner)]);
        store.Publish(anchor, CallKey, Call());

        var value = store.Read([anchor], CallKey, "idp", "agent");

        Assert.NotNull(value);
        Assert.Equal(["state", "durationSeconds", "customerRecord"], value!.Keys);
    }

    [Fact]
    public void P1_AParticipantNeverReceivesTheOwnersFields()
    {
        // Der Kunde am selben Anruf sieht Zustand und Dauer — die Kundenakte verlässt den
        // Server nicht. Nicht ausgeblendet: nicht ausgeliefert.
        var store = Store();
        var anchor = SharedContextAnchor.ForConversation("call-1");
        store.SetParticipants(anchor,
        [
            new("idp", "agent", SharedContextVisibility.Owner),
            new("portal", "kunde", SharedContextVisibility.Participant),
        ]);
        store.Publish(anchor, CallKey, Call());

        var value = store.Read([anchor], CallKey, "portal", "kunde");

        Assert.NotNull(value);
        Assert.Equal(["state", "durationSeconds"], value!.Keys);
        Assert.DoesNotContain("customerRecord", value.Keys);
    }

    [Fact]
    public void P1_AFieldNobodyDeclaredIsNotPublishedAtAll()
    {
        // Die sichere Richtung: eine vergessene Deklaration kostet ein Feld, das niemand sieht.
        // Andersherum — veröffentlichen, was am Objekt hängt — lecken Datensätze.
        var store = Store();
        var anchor = SharedContextAnchor.ForConversation("call-1");
        store.SetParticipants(anchor, [new("idp", "agent", SharedContextVisibility.Owner)]);

        var withExtra = Call();
        withExtra["internalNotes"] = "Kunde ist schwierig";
        store.Publish(anchor, CallKey, withExtra);

        var value = store.Read([anchor], CallKey, "idp", "agent");

        Assert.DoesNotContain("internalNotes", value!.Keys);
    }

    // ── P2: Anker kommen aus der Session ─────────────────────────────────────

    [Fact]
    public void P2_ASubjectAnchorNeedsBothIssuerAndSubject()
    {
        // Eine Subject-Id allein ist keine Identität (ADR-017): derselbe Wert bei einem anderen
        // Anbieter ist eine andere Person.
        var a = SharedContextAnchor.ForSubject("employees", "anna");
        var b = SharedContextAnchor.ForSubject("customers", "anna");

        Assert.NotEqual(a, b);
    }

    // ── Anker-Durchsetzung ───────────────────────────────────────────────────

    [Fact]
    public void SomebodyWithoutTheAnchorReceivesNothing()
    {
        var store = Store();
        var anchor = SharedContextAnchor.ForConversation("call-1");
        store.SetParticipants(anchor, [new("idp", "agent", SharedContextVisibility.Owner)]);
        store.Publish(anchor, CallKey, Call());

        var other = SharedContextAnchor.ForConversation("call-2");

        Assert.Null(store.Read([other], CallKey, "idp", "agent"));
    }

    [Fact]
    public void HoldingTheAnchorWithoutBeingAParticipantReceivesNothing()
    {
        // Den Anker zu kennen genügt nicht — es zählt, dass jemand ihn zugewiesen hat.
        var store = Store();
        var anchor = SharedContextAnchor.ForConversation("call-1");
        store.SetParticipants(anchor, [new("idp", "agent", SharedContextVisibility.Owner)]);
        store.Publish(anchor, CallKey, Call());

        Assert.Null(store.Read([anchor], CallKey, "idp", "fremder"));
    }

    [Fact]
    public void AConversationWithoutAssignedParticipantsSharesNothing()
    {
        // Ein unkonfigurierter Anker darf nicht der weiteste sein.
        var store = Store();
        var anchor = SharedContextAnchor.ForConversation("call-1");
        store.Publish(anchor, CallKey, Call());

        Assert.Null(store.Read([anchor], CallKey, "idp", "agent"));
    }

    [Fact]
    public void ASubjectAnchorIsItsOwnOwner()
    {
        var declaration = new SharedContextKeyDeclaration(
            "crm.lead-selection/v1",
            SharedContextAnchorType.Subject,
            "Die zuletzt gewählte Person über die Flächen einer Person hinweg.",
            [new("leadId", SharedContextVisibility.Participant), new("note", SharedContextVisibility.Owner)],
            TimeSpan.FromHours(1),
            "crm");
        var store = Store(null, declaration);
        var anchor = SharedContextAnchor.ForSubject("employees", "anna");
        store.Publish(anchor, "crm.lead-selection/v1",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["leadId"] = 7, ["note"] = "privat" });

        var value = store.Read([anchor], "crm.lead-selection/v1", "employees", "anna");

        // Es sind ihre eigenen Daten über ihre eigenen Flächen — sie sieht alles.
        Assert.Equal(["leadId", "note"], value!.Keys);
    }

    // ── P7: Nicht-Existenz ist ununterscheidbar von Nicht-Berechtigung ───────

    [Fact]
    public void P7_AnUndeclaredKeyAndAForbiddenOneAnswerTheSame()
    {
        var store = Store();
        var anchor = SharedContextAnchor.ForConversation("call-1");
        store.SetParticipants(anchor, [new("idp", "agent", SharedContextVisibility.Owner)]);
        store.Publish(anchor, CallKey, Call());

        var forbidden = store.Read([anchor], CallKey, "idp", "fremder");
        var nonexistent = store.Read([anchor], "gibt.es-nicht/v1", "idp", "agent");

        Assert.Null(forbidden);
        Assert.Null(nonexistent);
    }

    // ── Vertragstreue der Veröffentlichung ───────────────────────────────────

    [Fact]
    public void AnUndeclaredKeyCannotBePublished()
    {
        var store = Store();
        var anchor = SharedContextAnchor.ForConversation("call-1");

        Assert.False(store.Publish(anchor, "nicht.deklariert/v1", Call()));
    }

    [Fact]
    public void AKeyCannotBePublishedUnderTheWrongAnchorType()
    {
        // Der Vertrag sagt, woran der Key hängt. Ein Anruf an einem Subject-Anker aufzuhängen
        // wäre eine andere Zugriffsregel unter demselben Namen.
        var store = Store();

        Assert.False(store.Publish(SharedContextAnchor.ForSubject("idp", "anna"), CallKey, Call()));
    }

    // ── §5.4: Speicherbegrenzung ─────────────────────────────────────────────

    [Fact]
    public void AValueStopsBeingReadableAfterItsTimeToLive()
    {
        // Ein "aktiver Anruf" darf nicht ewig hängen, weil ein Tab abgestürzt ist.
        var time = new FakeTimeProvider();
        var store = Store(time);
        var anchor = SharedContextAnchor.ForConversation("call-1");
        store.SetParticipants(anchor, [new("idp", "agent", SharedContextVisibility.Owner)]);
        store.Publish(anchor, CallKey, Call());

        Assert.NotNull(store.Read([anchor], CallKey, "idp", "agent"));

        time.Advance(TimeSpan.FromMinutes(31));

        Assert.Null(store.Read([anchor], CallKey, "idp", "agent"));
    }

    [Fact]
    public void ReleasingAConversationDropsItsValues()
    {
        var store = Store();
        var anchor = SharedContextAnchor.ForConversation("call-1");
        store.SetParticipants(anchor, [new("idp", "agent", SharedContextVisibility.Owner)]);
        store.Publish(anchor, CallKey, Call());

        store.ReleaseConversation(anchor);

        Assert.Null(store.Read([anchor], CallKey, "idp", "agent"));
    }

    [Fact]
    public void PublishingNullClearsTheValue()
    {
        var store = Store();
        var anchor = SharedContextAnchor.ForConversation("call-1");
        store.SetParticipants(anchor, [new("idp", "agent", SharedContextVisibility.Owner)]);
        store.Publish(anchor, CallKey, Call());

        Assert.True(store.Publish(anchor, CallKey, null));
        Assert.Null(store.Read([anchor], CallKey, "idp", "agent"));
    }

    [Fact]
    public void ASubjectAnchorRejectsParticipantAssignment()
    {
        var store = Store();

        Assert.Throws<ArgumentException>(() =>
            store.SetParticipants(
                SharedContextAnchor.ForSubject("idp", "anna"),
                [new("idp", "anna", SharedContextVisibility.Owner)]));
    }
}

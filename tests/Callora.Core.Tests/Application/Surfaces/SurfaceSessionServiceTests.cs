using Callora.Core.Application.Surfaces;
using Callora.Core.Application.Surfaces.Events;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Callora.Core.Tests.Application.Surfaces;

/// <summary>
/// The session mechanics behind ADR-017 §8: a context exists for every visitor, only
/// an authenticated one is stored, and the token rotates when a guest becomes an
/// identity — otherwise an attacker who planted a known token would inherit the
/// session the victim logs into.
/// </summary>
public sealed class SurfaceSessionServiceTests
{
    private const string Audience = "portal.example.de";
    private static readonly DateTimeOffset Now = new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AFirstAnonymousVisit_MintsAGuestContextWithoutTouchingTheDatabase()
    {
        var fixture = new SurfaceSessionFixture();

        var result = await fixture.Service.EstablishAsync(
            Surface(), Audience, null, SurfaceIdentityResolution.Anonymous);

        var guest = Assert.IsType<GuestSurfaceCaller>(result.Caller);
        Assert.Equal(SurfaceIdentityIssuers.Guest, guest.Subject.Issuer);
        Assert.True(result.WritesCookie);
        Assert.Empty(fixture.Sessions.Sessions);
    }

    [Fact]
    public async Task AReturningGuest_KeepsItsSubjectAndItsCookie()
    {
        var fixture = new SurfaceSessionFixture();
        var first = await fixture.Service.EstablishAsync(
            Surface(), Audience, null, SurfaceIdentityResolution.Anonymous);

        var second = await fixture.Service.EstablishAsync(
            Surface(), Audience, first.CookieValue, SurfaceIdentityResolution.Anonymous);

        Assert.Equal(first.Caller.Subject, second.Caller.Subject);
        Assert.False(second.WritesCookie);
    }

    [Fact]
    public async Task ACookieFromAnotherSurface_IsDiscarded()
    {
        var fixture = new SurfaceSessionFixture();
        var minted = await fixture.Service.EstablishAsync(
            Surface(), Audience, null, SurfaceIdentityResolution.Anonymous);

        var elsewhere = await fixture.Service.EstablishAsync(
            Surface(surfaceKey: "shop"), Audience, minted.CookieValue, SurfaceIdentityResolution.Anonymous);

        Assert.NotEqual(minted.Caller.Subject, elsewhere.Caller.Subject);
        Assert.True(elsewhere.WritesCookie);
    }

    [Fact]
    public async Task ACookieFromAnotherHost_IsDiscarded()
    {
        var fixture = new SurfaceSessionFixture();
        var minted = await fixture.Service.EstablishAsync(
            Surface(), Audience, null, SurfaceIdentityResolution.Anonymous);

        var elsewhere = await fixture.Service.EstablishAsync(
            Surface(), "other.example.de", minted.CookieValue, SurfaceIdentityResolution.Anonymous);

        Assert.NotEqual(minted.Caller.Subject, elsewhere.Caller.Subject);
    }

    [Fact]
    public async Task AnExpiredGuestContext_IsReplaced()
    {
        var fixture = new SurfaceSessionFixture();
        var minted = await fixture.Service.EstablishAsync(
            Surface(), Audience, null, SurfaceIdentityResolution.Anonymous);

        fixture.Clock.Advance(TimeSpan.FromDays(31));
        var later = await fixture.Service.EstablishAsync(
            Surface(), Audience, minted.CookieValue, SurfaceIdentityResolution.Anonymous);

        Assert.NotEqual(minted.Caller.Subject, later.Caller.Subject);
    }

    [Fact]
    public async Task LoggingIn_RotatesTheTokenAndAnnouncesBothSubjects()
    {
        var fixture = new SurfaceSessionFixture();
        var guest = await fixture.Service.EstablishAsync(
            Surface(), Audience, null, SurfaceIdentityResolution.Anonymous);

        var loggedIn = await fixture.Service.EstablishAsync(
            Surface(), Audience, guest.CookieValue, Authenticated("lead-42"));

        Assert.NotEqual(guest.CookieValue, loggedIn.CookieValue);
        Assert.Single(fixture.Sessions.Sessions);

        var promotion = Assert.IsType<SurfaceCallerBusinessEvent>(Assert.Single(fixture.Events.Published));
        Assert.Equal(SurfaceCallerEventTypes.Promoted, promotion.EventName);
        var data = promotion.ToEventData();
        Assert.Equal(guest.Caller.Subject.SubjectId, data["previousSubjectId"]);
        Assert.Equal("lead-42", data["subjectId"]);
    }

    [Fact]
    public async Task AnAuthenticatedRequestWithoutAPriorGuest_AnnouncesNoPromotion()
    {
        var fixture = new SurfaceSessionFixture();

        await fixture.Service.EstablishAsync(Surface(), Audience, null, Authenticated("lead-42"));

        Assert.Empty(fixture.Events.Published);
    }

    [Fact]
    public async Task AFollowUpRequest_ReusesTheSessionWithoutRewritingTheCookie()
    {
        var fixture = new SurfaceSessionFixture();
        var loggedIn = await fixture.Service.EstablishAsync(
            Surface(), Audience, null, Authenticated("lead-42"));

        fixture.Clock.Advance(TimeSpan.FromMinutes(5));
        var followUp = await fixture.Service.EstablishAsync(
            Surface(), Audience, loggedIn.CookieValue, Authenticated("lead-42"));

        Assert.False(followUp.WritesCookie);
        Assert.Single(fixture.Sessions.Sessions);
        Assert.Equal(Now.AddMinutes(5), fixture.Sessions.LastSeen.Values.Single());
    }

    [Fact]
    public async Task ADifferentSubjectOnTheSameCookie_ReplacesTheSession()
    {
        var fixture = new SurfaceSessionFixture();
        var first = await fixture.Service.EstablishAsync(
            Surface(), Audience, null, Authenticated("lead-42"));

        var second = await fixture.Service.EstablishAsync(
            Surface(), Audience, first.CookieValue, Authenticated("lead-99"));

        Assert.NotEqual(first.CookieValue, second.CookieValue);
        var session = Assert.Single(fixture.Sessions.Sessions);
        Assert.Equal("lead-99", session.Subject.SubjectId);
    }

    [Fact]
    public async Task WhenTheProviderStopsRecognisingTheVisitor_TheSessionEnds()
    {
        var fixture = new SurfaceSessionFixture();
        var loggedIn = await fixture.Service.EstablishAsync(
            Surface(), Audience, null, Authenticated("lead-42"));

        var afterwards = await fixture.Service.EstablishAsync(
            Surface(), Audience, loggedIn.CookieValue, SurfaceIdentityResolution.Anonymous);

        Assert.Empty(fixture.Sessions.Sessions);
        Assert.IsType<GuestSurfaceCaller>(afterwards.Caller);
        Assert.True(afterwards.WritesCookie);
    }

    [Fact]
    public async Task ASessionIssuedBeforeAProviderChange_IsNoLongerTrusted()
    {
        var fixture = new SurfaceSessionFixture();
        var loggedIn = await fixture.Service.EstablishAsync(
            Surface(identityPluginId: "crm", assignedAtUtc: Now.AddDays(-1)),
            Audience,
            null,
            Authenticated("lead-42"));

        // The operator points the surface at a different provider: everything the
        // previous one vouched for stops counting.
        var afterSwap = await fixture.Service.EstablishAsync(
            Surface(identityPluginId: "portal", assignedAtUtc: Now.AddMinutes(1)),
            Audience,
            loggedIn.CookieValue,
            SurfaceIdentityResolution.Anonymous);

        Assert.IsType<GuestSurfaceCaller>(afterSwap.Caller);
        Assert.True(afterSwap.WritesCookie);
    }

    [Fact]
    public async Task AGuestContextSurvivesAProviderChange()
    {
        var fixture = new SurfaceSessionFixture();
        var guest = await fixture.Service.EstablishAsync(
            Surface(identityPluginId: "crm", assignedAtUtc: Now.AddDays(-1)),
            Audience,
            null,
            SurfaceIdentityResolution.Anonymous);

        var afterSwap = await fixture.Service.EstablishAsync(
            Surface(identityPluginId: "portal", assignedAtUtc: Now.AddMinutes(1)),
            Audience,
            guest.CookieValue,
            SurfaceIdentityResolution.Anonymous);

        Assert.Equal(guest.Caller.Subject, afterSwap.Caller.Subject);
    }

    [Fact]
    public async Task AClosedSurface_LeavesAnExistingSessionIntactAndUnused()
    {
        var fixture = new SurfaceSessionFixture();
        var loggedIn = await fixture.Service.EstablishAsync(
            Surface(), Audience, null, Authenticated("lead-42"));

        var closed = await fixture.Service.EstablishAsync(
            Surface(),
            Audience,
            loggedIn.CookieValue,
            SurfaceIdentityResolution.Closed(SurfaceIdentityResolutionStatus.ProviderUnavailable, "outage"));

        Assert.True(closed.IsClosed);
        Assert.IsType<GuestSurfaceCaller>(closed.Caller);
        // Not revoked: the provider is unreachable, not gone — a transient outage
        // must not sign every visitor out.
        Assert.Single(fixture.Sessions.Sessions);
        Assert.False(closed.WritesCookie);
    }

    [Fact]
    public async Task EndingASession_RevokesItAndHandsBackAFreshGuest()
    {
        var fixture = new SurfaceSessionFixture();
        var loggedIn = await fixture.Service.EstablishAsync(
            Surface(), Audience, null, Authenticated("lead-42"));

        var ended = await fixture.Service.EndSessionAsync(Surface(), Audience, loggedIn.CookieValue);

        Assert.Empty(fixture.Sessions.Sessions);
        Assert.IsType<GuestSurfaceCaller>(ended.Caller);
        Assert.True(ended.WritesCookie);
    }

    private static SurfaceIdentityResolution Authenticated(string subjectId) =>
        SurfaceIdentityResolution.Authenticated(new AuthenticatedSurfaceCaller(
            new SurfaceSubject("crm.example", subjectId),
            new SurfaceIdentity(
                subjectId,
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
                "password",
                Now.AddMinutes(-1),
                Now.AddHours(2))));

    private static WorkspaceSurfaceSnapshot Surface(
        string surfaceKey = "portal",
        string? identityPluginId = null,
        DateTimeOffset? assignedAtUtc = null) =>
        new(
            Guid.NewGuid(),
            "workspace-a",
            surfaceKey,
            "Portal",
            "spa",
            null,
            null,
            "/",
            SurfaceAccessMode.Mixed,
            "de",
            null,
            null,
            null,
            null,
            true,
            Now,
            Now)
        {
            TenantKey = "tenant-a",
            IdentityPluginId = identityPluginId,
            IdentityAssignedAtUtc = assignedAtUtc,
        };

    private sealed class SurfaceSessionFixture
    {
        public SurfaceSessionFixture()
        {
            Clock = new FakeTimeProvider(Now);
            Sessions = new InMemorySurfaceSessionStore();
            Events = new RecordingBusinessEventBus();
            Service = new SurfaceSessionService(
                Sessions,
                new JsonSurfaceSessionCookieCodec(),
                Events,
                new SurfaceIdentityOptions(),
                Clock,
                NullLogger<SurfaceSessionService>.Instance);
        }

        public FakeTimeProvider Clock { get; }

        public InMemorySurfaceSessionStore Sessions { get; }

        public RecordingBusinessEventBus Events { get; }

        public SurfaceSessionService Service { get; }
    }
}

using Callora.Core.Application.Surfaces;
using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Tests.Support;
using Microsoft.Extensions.Time.Testing;

namespace Callora.Core.Tests.Application.Surfaces;

/// <summary>
/// On seams without a surface route to resolve from, the cookie names its own scope
/// (ADR-017 §9). The authenticator's job is to distrust that claim until the surface
/// and the stored session confirm it.
/// </summary>
public sealed class SurfaceSessionAuthenticatorTests
{
    private const string Audience = "portal.example.de";
    private static readonly DateTimeOffset Now = new(2026, 8, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task NoCookie_YieldsNoCaller()
    {
        var fixture = new AuthenticatorFixture();

        Assert.Null(await fixture.Authenticator.AuthenticateAsync(null, Audience));
    }

    [Fact]
    public async Task AGuestCookie_YieldsAGuestCallerWithoutTouchingTheSessionStore()
    {
        var fixture = new AuthenticatorFixture();
        var cookie = fixture.GuestCookie("g-1");

        var context = await fixture.Authenticator.AuthenticateAsync(cookie, Audience);

        var guest = Assert.IsType<GuestSurfaceCaller>(context!.Caller);
        Assert.Equal(SurfaceIdentityIssuers.Guest, guest.Subject.Issuer);
        Assert.Equal("g-1", guest.Subject.SubjectId);
    }

    [Fact]
    public async Task AnExpiredGuestCookie_YieldsNoCaller()
    {
        var fixture = new AuthenticatorFixture();
        var cookie = fixture.GuestCookie("g-1", issuedAtUtc: Now.AddDays(-31));

        Assert.Null(await fixture.Authenticator.AuthenticateAsync(cookie, Audience));
    }

    [Fact]
    public async Task ASessionCookie_YieldsTheStoredIdentity()
    {
        var fixture = new AuthenticatorFixture();
        var cookie = await fixture.SessionCookieAsync();

        var context = await fixture.Authenticator.AuthenticateAsync(cookie, Audience);

        Assert.Equal("workspace-a", context!.WorkspaceKey);
        Assert.Equal("portal", context.SurfaceKey);
        var authenticated = Assert.IsType<AuthenticatedSurfaceCaller>(context.Caller);
        Assert.Equal("crm.example", authenticated.Subject.Issuer);
        Assert.Equal("lead-42", authenticated.Subject.SubjectId);
        Assert.Equal(["agent"], authenticated.Identity.Claims["crm.roles"]);
    }

    [Fact]
    public async Task ACookiePresentedOnAnotherHost_YieldsNoCaller()
    {
        var fixture = new AuthenticatorFixture();
        var cookie = await fixture.SessionCookieAsync();

        Assert.Null(await fixture.Authenticator.AuthenticateAsync(cookie, "other.example.de"));
    }

    [Fact]
    public async Task ACookiePointingAtNoSession_YieldsNoCaller()
    {
        var fixture = new AuthenticatorFixture();
        var cookie = fixture.SessionCookie(Guid.NewGuid());

        Assert.Null(await fixture.Authenticator.AuthenticateAsync(cookie, Audience));
    }

    [Fact]
    public async Task AnExpiredSession_YieldsNoCaller()
    {
        var fixture = new AuthenticatorFixture();
        var cookie = await fixture.SessionCookieAsync();

        fixture.Clock.Advance(TimeSpan.FromHours(3));

        Assert.Null(await fixture.Authenticator.AuthenticateAsync(cookie, Audience));
    }

    [Fact]
    public async Task ASessionIssuedBeforeAProviderChange_YieldsNoCaller()
    {
        var fixture = new AuthenticatorFixture();
        var cookie = await fixture.SessionCookieAsync();

        fixture.AssignIdentityProviderAt(Now.AddMinutes(1));

        Assert.Null(await fixture.Authenticator.AuthenticateAsync(cookie, Audience));
    }

    [Fact]
    public async Task AnEnvelopeDisagreeingWithItsStoredSession_YieldsNoCaller()
    {
        var fixture = new AuthenticatorFixture();

        // The envelope claims the portal surface, the row it points at belongs to shop.
        var sessionId = await fixture.StoreSessionAsync(surfaceKey: "shop");
        var cookie = fixture.SessionCookie(sessionId);

        Assert.Null(await fixture.Authenticator.AuthenticateAsync(cookie, Audience));
    }

    [Fact]
    public async Task ACookieForAnInactiveSurface_YieldsNoCaller()
    {
        var fixture = new AuthenticatorFixture();
        var cookie = await fixture.SessionCookieAsync();

        fixture.DeactivateSurface();

        Assert.Null(await fixture.Authenticator.AuthenticateAsync(cookie, Audience));
    }

    private sealed class AuthenticatorFixture
    {
        private const string WorkspaceKey = "workspace-a";
        private const string SurfaceKey = "portal";
        private const string TenantKey = "tenant-a";

        private readonly JsonSurfaceSessionCookieCodec _codec = new();
        private readonly InMemoryWorkspaceSurfaceStore _surfaces = new();

        public AuthenticatorFixture()
        {
            Clock = new FakeTimeProvider(Now);
            Sessions = new InMemorySurfaceSessionStore();
            _surfaces.Seed(Surface(isActive: true, identityAssignedAtUtc: Now.AddDays(-1)));
            Authenticator = new SurfaceSessionAuthenticator(
                _codec, Sessions, _surfaces, new SurfaceIdentityOptions(), Clock);
        }

        public FakeTimeProvider Clock { get; }

        public InMemorySurfaceSessionStore Sessions { get; }

        public SurfaceSessionAuthenticator Authenticator { get; }

        public string GuestCookie(string subjectId, DateTimeOffset? issuedAtUtc = null) =>
            _codec.Protect(Envelope(SurfaceSessionEnvelopeKind.Guest, subjectId, issuedAtUtc));

        public string SessionCookie(Guid sessionId, DateTimeOffset? issuedAtUtc = null) =>
            _codec.Protect(Envelope(
                SurfaceSessionEnvelopeKind.Authenticated, sessionId.ToString("N"), issuedAtUtc));

        public async Task<string> SessionCookieAsync()
        {
            var sessionId = await StoreSessionAsync();
            return SessionCookie(sessionId);
        }

        public async Task<Guid> StoreSessionAsync(string surfaceKey = SurfaceKey)
        {
            var sessionId = Guid.NewGuid();
            await Sessions.CreateAsync(new SurfaceSession(
                sessionId,
                TenantKey,
                WorkspaceKey,
                surfaceKey,
                Audience,
                new SurfaceSubject("crm.example", "lead-42"),
                new SurfaceIdentity(
                    "Erika Muster",
                    new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
                    {
                        ["crm.roles"] = ["agent"],
                    },
                    "password",
                    Now.AddMinutes(-1),
                    Now.AddHours(2)),
                Now,
                Now.AddHours(2),
                "crm",
                "1.0.0"));
            return sessionId;
        }

        public void AssignIdentityProviderAt(DateTimeOffset assignedAtUtc) =>
            _surfaces.Seed(Surface(isActive: true, identityAssignedAtUtc: assignedAtUtc));

        public void DeactivateSurface() =>
            _surfaces.Seed(Surface(isActive: false, identityAssignedAtUtc: Now.AddDays(-1)));

        private static SurfaceSessionEnvelope Envelope(
            SurfaceSessionEnvelopeKind kind,
            string id,
            DateTimeOffset? issuedAtUtc) =>
            new(
                SurfaceSessionEnvelope.CurrentVersion,
                kind,
                id,
                TenantKey,
                WorkspaceKey,
                SurfaceKey,
                Audience,
                issuedAtUtc ?? Now);

        private static WorkspaceSurfaceSnapshot Surface(bool isActive, DateTimeOffset identityAssignedAtUtc) =>
            new(
                Guid.NewGuid(),
                WorkspaceKey,
                SurfaceKey,
                "Portal",
                "spa",
                null,
                null,
                "/",
                SurfaceAccessMode.Mixed,
                SurfaceRouting.Tree,
                "de",
                null,
                null,
                null,
                null,
                isActive,
                Now,
                Now)
            {
                TenantKey = TenantKey,
                IdentityPluginId = "crm",
                IdentityVersion = "1.0.0",
                IdentityAssignedAtUtc = identityAssignedAtUtc,
            };
    }
}

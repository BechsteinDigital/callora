using Callora.Core.Application.Surfaces;
using Callora.Surface.Rendering.Api.SurfaceContext;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// A context socket lives for hours; the permission behind it does not. These pin that the
/// connection falls when the session stops holding — sign-out, expiry, or a reassigned identity
/// provider (ADR-017 §6.3) — rather than keeping personal context flowing into a tab that may no
/// longer see it.
/// </summary>
public sealed class SurfaceContextRevalidatorTests
{
    private const string Host = "acme.example";
    private const string Cookie = "cookie-value";

    [Fact]
    public async Task AnAnonymousConnectionIsNeverInvalidated()
    {
        // Es gibt keine Sitzung zu verlieren: eine anonyme Verbindung erhält nur
        // flächenweite Werte, und die hängen an keiner widerrufbaren Identität.
        var time = new FakeTimeProvider();
        var revalidator = new SurfaceContextRevalidator(Probe(null), time);
        using var connection = new CancellationTokenSource();

        await revalidator.WatchAsync(cookieValue: null, Host, acceptedSubjectId: null, connection);

        Assert.False(connection.IsCancellationRequested);
    }

    [Fact]
    public async Task AConnectionSurvivesWhileItsSessionHolds()
    {
        var time = new FakeTimeProvider();
        var revalidator = new SurfaceContextRevalidator(Probe(Caller("anna")), time);
        using var connection = new CancellationTokenSource();

        var watching = revalidator.WatchAsync(Cookie, Host, "anna", connection);
        time.Advance(SurfaceContextRevalidator.Interval * 3);
        await Task.Yield();

        Assert.False(connection.IsCancellationRequested);

        await connection.CancelAsync();
        await watching;
    }

    [Fact]
    public async Task AConnectionFallsWhenTheSessionIsGone()
    {
        // Abmeldung, Ablauf, neu zugewiesener Identity-Provider — der Authenticator antwortet
        // in allen drei Fällen mit null, und alle drei bedeuten dasselbe für diese Verbindung.
        var time = new FakeTimeProvider();
        var revalidator = new SurfaceContextRevalidator(Probe(null), time);
        using var connection = new CancellationTokenSource();

        var watching = revalidator.WatchAsync(Cookie, Host, "anna", connection);
        time.Advance(SurfaceContextRevalidator.Interval);
        await watching;

        Assert.True(connection.IsCancellationRequested);
    }

    [Fact]
    public async Task AConnectionFallsWhenSomebodyElseIsOnTheCookie()
    {
        // Die Anker dieser Verbindung gehören dann jemand anderem. Sie fallen zu lassen und den
        // Client neu verbinden zu lassen ist die einzige richtige Antwort.
        var time = new FakeTimeProvider();
        var revalidator = new SurfaceContextRevalidator(Probe(Caller("bert")), time);
        using var connection = new CancellationTokenSource();

        var watching = revalidator.WatchAsync(Cookie, Host, "anna", connection);
        time.Advance(SurfaceContextRevalidator.Interval);
        await watching;

        Assert.True(connection.IsCancellationRequested);
    }

    [Fact]
    public async Task WithoutTheIdentitySubsystemThereIsNothingToWatch()
    {
        // Eine Komposition ohne Identitäts-Subsystem hat keine Sitzung hinter der Verbindung.
        // Der Revalidator fehlt dann nicht, er hat nur nichts zu tun.
        var time = new FakeTimeProvider();
        var revalidator = new SurfaceContextRevalidator(probe: null, time);
        using var connection = new CancellationTokenSource();

        await revalidator.WatchAsync(Cookie, Host, "anna", connection);

        Assert.False(connection.IsCancellationRequested);
    }

    // Nur die Identität zählt hier: der Revalidator vergleicht die SubjectId und sonst nichts.
    private static SurfaceCallerContext Caller(string subjectId) => new(
        new GuestSurfaceCaller(new SurfaceSubject("employees", subjectId)),
        TenantKey: "acme",
        WorkspaceKey: "acme",
        SurfaceKey: "agent-desk");

    private static SurfaceSessionProbe Probe(SurfaceCallerContext? returns) =>
        (_, _, _) => Task.FromResult(returns);
}

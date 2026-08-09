using System.Security.Claims;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Security;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Infrastructure.Surfaces;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// Was ein im Backend angemeldeter Operator auf einer Fläche mitbringt (ADR-023 §2).
/// <para>
/// Der Anlass: Ein Betreiber ohne Identity-Plugin kam auf seine eigene Fläche herein, aber ohne
/// jeden Claim — und jeder Block mit einer Anforderung meldete „missing claim". Er darf im Admin
/// telefonieren, auf seiner Fläche nicht.
/// </para>
/// </summary>
public sealed class OperatorPermissionsBecomeSurfaceClaimsTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 12, 0, 0, TimeSpan.Zero);

    private static BackendPrincipalSurfaceIdentitySource Source(params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "operator-7"),
            new(ClaimTypes.Name, "Ops"),
        };
        claims.AddRange(permissions.Select(p => new Claim(BackendClaimTypes.Permission, p)));

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")),
        };

        return new BackendPrincipalSurfaceIdentitySource(
            new HttpContextAccessor { HttpContext = httpContext },
            new FakeTimeProvider(Now));
    }

    private static HostSurfaceIdentityRequest Request() =>
        new("acme", "acme", "desk", "GET", "/", "de-DE", []);

    private static async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ClaimsOn(
        BackendPrincipalSurfaceIdentitySource source,
        SurfaceAuthentication authentication)
    {
        var result = await source.AuthenticateAsync(Request(), authentication);
        Assert.True(result.IsIdentified);
        return result.Claims;
    }

    [Fact]
    public async Task AnAdministrationSurfaceTurnsPermissionsIntoClaims()
    {
        // Die Aktion ist das LETZTE Segment (BackendPermissionKey), sonst zerfiele
        // `communication.calls.read` in `communication` + `calls.read` und träfe nie eine
        // Anforderung, die auf `communication.calls` lautet.
        var claims = await ClaimsOn(
            Source("communication.calls.read", "communication.calls.write"),
            SurfaceAuthentication.Administration);

        Assert.True(claims.TryGetValue("communication.calls", out var actions));
        Assert.Equal(["read", "write"], actions!.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task NoOtherAuthenticationCarriesThemAtAll()
    {
        // Die Gegenprobe zur eigentlichen Sorge aus ADR-017 §7: Vor ADR-023 galt diese Quelle
        // für JEDE Fläche ohne Identity-Plugin. Ohne diese Einschränkung bekäme eine öffentliche
        // Website die Adminrechte dessen, der zufällig woanders angemeldet ist.
        foreach (var authentication in new[] { SurfaceAuthentication.Public, SurfaceAuthentication.SurfaceIdentity })
        {
            var claims = await ClaimsOn(Source("communication.calls.read"), authentication);
            Assert.DoesNotContain("communication.calls", claims.Keys, StringComparer.Ordinal);
        }
    }

    [Fact]
    public async Task TheWildcardIsNotExpanded()
    {
        // Ein Maschinenschlüssel trägt `*`. Daraus „jeden Claim" zu machen hieße, dass die
        // Flächenseite Wildcard-Semantik lernen müsste — die zweite Stelle, die dieselbe Frage
        // beantwortet, und genau der Fehler, der diese Sitzung sechsmal gekostet hat.
        var claims = await ClaimsOn(Source("*"), SurfaceAuthentication.Administration);

        Assert.DoesNotContain("*", claims.Keys, StringComparer.Ordinal);
    }

    [Fact]
    public async Task APermissionNeverOverwritesTheWorkspaceBinding()
    {
        // Die Workspace-Bindung ist eine Aussage darüber, WER das ist. Eine Berechtigung darf
        // sie nicht neu behaupten können — sonst hinge die Mandantengrenze an einem Rollennamen.
        // Ohne gesetzte Bindung wäre das keine Überschreibung, sondern eine Erfindung: genau
        // deshalb greift der Schutz am NAMEN und nicht an „ist schon da".
        var source = Source($"{BackendPrincipalSurfaceIdentitySource.WorkspaceClaim}.read");
        var claims = await ClaimsOn(source, SurfaceAuthentication.Administration);

        Assert.DoesNotContain(
            BackendPrincipalSurfaceIdentitySource.WorkspaceClaim,
            claims.Keys,
            StringComparer.Ordinal);
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

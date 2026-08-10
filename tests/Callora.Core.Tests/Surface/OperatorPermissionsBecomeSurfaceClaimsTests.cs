using System.Security.Claims;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Security;
using Callora.Core.Application.Surfaces;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Infrastructure.Surfaces;
using Callora.Core.Tests.Support;
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

    private static BackendPrincipalSurfaceIdentitySource Source(params string[] permissions) =>
        SourceInRole(null, permissions);

    // Eigener Name statt Überladung: `Source("a", "b")` hätte sonst an die Rollen-Variante
    // gebunden und die erste Berechtigung stillschweigend zur Rolle gemacht.
    private static BackendPrincipalSurfaceIdentitySource SourceInRole(string? role, params string[] permissions)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "operator-7"),
            new(ClaimTypes.Name, "Ops"),
            // Die Sitzung MUSS ihre Reichweite tragen. Vorher stand hier keine, und die Quelle
            // prüfte auch keine: Sie authentifizierte auf jeder Fläche jedes Workspaces. Seit sie
            // die Bindung prüft (WorkspaceScopeEvaluator.HasWorkspaceAccess, fail-closed), wäre
            // ein Principal ohne Reichweite ein Zustand, den das System gar nicht erzeugt —
            // AdminLoginResolver vergibt immer entweder Platform-Scope oder Workspace-Scope
            // samt Schlüssel.
            //
            // Hier Platform-Scope, weil es in dieser Klasse um OPERATOREN geht: Sie sind an
            // keinen Workspace gebunden und tragen deshalb auch keinen host.workspace-key-Claim
            // auf die Fläche — was zwei der Tests unten ausdrücklich prüfen.
            new(BackendClaimTypes.CalloraScope, BackendAuthScopes.Platform),
        };
        if (role is not null)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        claims.AddRange(permissions.Select(p => new Claim(BackendClaimTypes.Permission, p)));

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")),
        };

        return new BackendPrincipalSurfaceIdentitySource(
            new HttpContextAccessor { HttpContext = httpContext },
            new FakeTimeProvider(Now),
            Catalog(),
            new SurfaceIdentityOptions());
    }

    // Ein Katalog mit einem Plugin-Beitrag. Nötig, weil KERN-Berechtigungen per Konstruktion
    // keine Flächen-Claims werden können: `tenant.read` ergibt die Funktion `tenant`, und ein
    // Claim-Schlüssel braucht einen Namensraum. Nur Plugins bringen dreisegmentige Schlüssel mit.
    private static StaticPluginExportCatalog Catalog() =>
        new StaticPluginExportCatalog().Add("communication", new PermissionContributor());

    private sealed class PermissionContributor : IHostAdminApiExtensionContributor
    {
        public string PluginId => "communication";

        public IReadOnlyList<string> PermissionKeys =>
            ["communication.calls.read", "communication.calls.write", "communication.accounts.read"];

        public IReadOnlyList<HostAdminApiRouteRegistration> Routes => [];

        public IReadOnlyList<HostAdminNavigationItem> NavigationItems => [];
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

    [Fact]
    public async Task ASuperAdminHoldsTheWholeInventory()
    {
        // Der Fall, den die laufende Instanz aufgedeckt hat: Ein SuperAdmin trägt NULL
        // Berechtigungs-Claims — er umgeht im Backend jede Prüfung über seine Rolle. Ohne
        // eigenen Zweig brächte ausgerechnet der Betreiber auf seiner Fläche nichts mit.
        var claims = await ClaimsOn(
            SourceInRole(BackendRoles.SuperAdmin),
            SurfaceAuthentication.Administration);

        Assert.NotEmpty(claims);
        // Gegenprobe zur Quelle: Es ist DASSELBE Inventar, das die Rollenverwaltung anbietet.
        Assert.Contains("communication.calls", claims.Keys, StringComparer.Ordinal);
        foreach (var permission in BackendPermissionInventory.All(Catalog()))
        {
            Assert.True(BackendPermissionKey.TryParse(permission, out var key));
            if (key.Function == BackendPrincipalSurfaceIdentitySource.WorkspaceClaim ||
                !SurfaceIdentityTokenSyntax.IsNamespacedKey(key.Function, new SurfaceIdentityOptions().MaxClaimKeyLength))
            {
                continue;
            }

            Assert.True(claims.ContainsKey(key.Function), $"Claim {key.Function} fehlt.");
            Assert.Contains(key.Action, claims[key.Function]);
        }
    }

    [Fact]
    public async Task APermissionThatCannotBeAClaimKeyIsSkippedInsteadOfClosingTheSurface()
    {
        // Am laufenden System gefunden: `config.read` ergibt die Funktion `config` — einsegmentig
        // und als Flächen-Claim unzulässig (die brauchen einen Namensraum). Durchgereicht ließ es
        // die Normalisierung die GANZE Identität verwerfen, und die Fläche antwortete mit 503.
        // Eine abgeleitete Bequemlichkeit darf keine Anmeldung kippen.
        var claims = await ClaimsOn(
            Source("config.read", "communication.calls.read"),
            SurfaceAuthentication.Administration);

        Assert.DoesNotContain("config", claims.Keys, StringComparer.Ordinal);
        Assert.Contains("communication.calls", claims.Keys, StringComparer.Ordinal);
    }

    [Fact]
    public async Task ASuperAdminOnAPublicSurfaceStillHoldsNothing()
    {
        // Die Rolle darf die Achse nicht aushebeln: Sonst wäre `Public` für einen SuperAdmin
        // stillschweigend `Administration`, und ADR-017 §7 gälte für ihn nicht mehr.
        var claims = await ClaimsOn(SourceInRole(BackendRoles.SuperAdmin), SurfaceAuthentication.Public);

        Assert.Empty(claims);
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

using System.Security.Claims;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Security;
using Callora.Core.Application.Surfaces;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Infrastructure.Surfaces;
using Callora.Core.Tests.Support;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// Die Mandantengrenze der host-abgeleiteten Flächen-Identität: Eine an einen Workspace
/// gebundene Backend-Sitzung authentifiziert nur auf Flächen DIESES Workspaces.
/// </summary>
/// <remarks>
/// <para>
/// Der Befund: <c>AuthenticateAsync</c> bekommt in <c>request.WorkspaceKey</c> den Workspace der
/// FLÄCHE und liest aus dem Principal den Workspace, an den die SITZUNG gebunden ist — verglich
/// beide aber nie. Ein Workspace-Admin von <c>acme</c> bekam damit auf der Administrations-Fläche
/// von <c>globex</c> eine gültige Identität, das Zugriffs-Gate sah einen
/// <c>AuthenticatedSurfaceCaller</c> und servierte. Die Fläche lud ihre Daten anschließend mit
/// IHREM Workspace-Schlüssel.
/// </para>
/// <para>
/// Das ist kein exotischer Aufbau: Ohne eigene Domain macht ADR-021 den Workspace-Schlüssel zum
/// Pfadpräfix auf derselben Origin — die fremde Fläche ist dann einen Pfadwechsel entfernt.
/// </para>
/// </remarks>
public sealed class HostIdentityRespectsWorkspaceBindingTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(SurfaceAuthentication.Administration)]
    [InlineData(SurfaceAuthentication.SurfaceIdentity)]
    public async Task ASessionBoundToAnotherWorkspace_IsAnonymous(SurfaceAuthentication authentication)
    {
        var source = Source(boundWorkspace: "acme");

        var result = await source.AuthenticateAsync(RequestFor("globex"), authentication);

        Assert.False(result.IsIdentified);
    }

    [Fact]
    public async Task ASessionBoundToTheSameWorkspace_IsIdentified()
    {
        var source = Source(boundWorkspace: "acme");

        var result = await source.AuthenticateAsync(RequestFor("acme"), SurfaceAuthentication.Administration);

        Assert.True(result.IsIdentified);
    }

    [Fact]
    public async Task WorkspaceKeysAreComparedCaseInsensitively()
    {
        // Workspace-Schlüssel werden überall ohne Rücksicht auf Groß-/Kleinschreibung
        // verglichen; eine Grenze, die daran scheitert, wäre eine Falle für den Betreiber.
        var source = Source(boundWorkspace: "ACME");

        var result = await source.AuthenticateAsync(RequestFor("acme"), SurfaceAuthentication.Administration);

        Assert.True(result.IsIdentified);
    }

    [Fact]
    public async Task APlatformOperator_IsIdentifiedOnEveryWorkspace()
    {
        // Ein Plattform-Operator ist per Definition nicht an einen Workspace gebunden —
        // seine Reichweite ist global (WorkspaceScopeEvaluator.IsOperator). Ihn auszusperren
        // hieße, den Betreiber von den Flächen zu trennen, die er betreiben soll.
        var source = Source(boundWorkspace: null, role: BackendRoles.SuperAdmin);

        var result = await source.AuthenticateAsync(RequestFor("globex"), SurfaceAuthentication.Administration);

        Assert.True(result.IsIdentified);
    }

    private static BackendPrincipalSurfaceIdentitySource Source(string? boundWorkspace, string? role = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "user-1"),
            new(ClaimTypes.Name, "Wer auch immer"),
        };
        if (!string.IsNullOrWhiteSpace(boundWorkspace))
        {
            claims.Add(new Claim(BackendClaimTypes.WorkspaceKey, boundWorkspace));
            claims.Add(new Claim(BackendClaimTypes.CalloraScope, BackendAuthScopes.Workspace));
        }

        if (role is not null)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")),
        };

        return new BackendPrincipalSurfaceIdentitySource(
            new HttpContextAccessor { HttpContext = httpContext },
            new FakeTimeProvider(Now),
            new StaticPluginExportCatalog(),
            new SurfaceIdentityOptions());
    }

    private static HostSurfaceIdentityRequest RequestFor(string workspaceKey) =>
        new("tenant-1", workspaceKey, "desk", "GET", "/", "de-DE", []);
}

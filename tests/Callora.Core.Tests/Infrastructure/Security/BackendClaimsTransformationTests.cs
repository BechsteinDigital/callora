using Callora.Core.Application.Policies;
using Callora.Core.Application.Security;
using Callora.Core.Infrastructure.Security;
using System.Security.Claims;
using Xunit;

namespace Callora.Core.Tests.Infrastructure.Security;

/// <summary>
/// Die RBAC-Projektion gilt für Plattform-Sessions, nicht für workspace-gebundene.
/// <para>
/// <b>Der Befund:</b> Rollennamen sind EIN Namensraum. Eine Workspace-Mitgliedschaft heißt
/// <c>admin</c> (<see cref="BackendRoles.Admin"/>), und eine Plattformrolle darf genauso heißen —
/// gesperrt ist nur <c>superadmin</c>. Wer beides gleich benennt, gab damit jedem Workspace-Admin
/// jedes Mandanten die Plattform-Permissions der gleichnamigen RBAC-Rolle, weil die Projektion
/// den Rollen-Claim der Session nachschlug, ohne auf den Scope zu sehen.
/// </para>
/// <para>
/// Eine Workspace-Session braucht die Projektion ohnehin nicht: Sie trägt ihre Permissions
/// vollständig aus <see cref="WorkspaceRolePermissions"/> im Token (AdminLoginResolver).
/// </para>
/// </summary>
public sealed class BackendClaimsTransformationTests
{
    private const string PlatformPermission = "plugins.install";

    [Fact]
    public async Task WorkspaceSession_DoesNotInheritAPlatformRoleOfTheSameName()
    {
        var transformation = new BackendClaimsTransformation(await StoreWithPlatformRoleNamedAdminAsync());

        var result = await transformation.TransformAsync(
            Session(BackendAuthScopes.Workspace, role: BackendRoles.Admin));

        Assert.DoesNotContain(
            result.Claims,
            claim => claim.Type == BackendClaimTypes.Permission && claim.Value == PlatformPermission);
    }

    [Fact]
    public async Task WorkspaceSession_DoesNotInheritTheGloballyAssignedRoleEither()
    {
        var store = await StoreWithPlatformRoleNamedAdminAsync();
        await store.UpsertRoleAsync("operator", [PlatformPermission]);
        await store.UpsertUserRoleAsync("user-1", "operator");
        var transformation = new BackendClaimsTransformation(store);

        var result = await transformation.TransformAsync(
            Session(BackendAuthScopes.Workspace, role: "member"));

        Assert.DoesNotContain(result.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == "operator");
        Assert.DoesNotContain(
            result.Claims,
            claim => claim.Type == BackendClaimTypes.Permission && claim.Value == PlatformPermission);
    }

    [Fact]
    public async Task PlatformSession_StillReceivesTheProjectedPermissions()
    {
        var transformation = new BackendClaimsTransformation(await StoreWithPlatformRoleNamedAdminAsync());

        var result = await transformation.TransformAsync(
            Session(BackendAuthScopes.Platform, role: BackendRoles.Admin));

        Assert.Contains(
            result.Claims,
            claim => claim.Type == BackendClaimTypes.Permission && claim.Value == PlatformPermission);
    }

    /// <summary>Eine Session ohne Scope-Claim bleibt projiziert — der Ausstieg gilt nur „workspace".</summary>
    [Fact]
    public async Task SessionWithoutAScopeClaim_StillReceivesTheProjectedPermissions()
    {
        var transformation = new BackendClaimsTransformation(await StoreWithPlatformRoleNamedAdminAsync());

        var result = await transformation.TransformAsync(Session(scope: null, role: BackendRoles.Admin));

        Assert.Contains(
            result.Claims,
            claim => claim.Type == BackendClaimTypes.Permission && claim.Value == PlatformPermission);
    }

    private static async Task<InMemoryBackendRbacStore> StoreWithPlatformRoleNamedAdminAsync()
    {
        var store = new InMemoryBackendRbacStore(new BackendHostOptions());
        await store.UpsertRoleAsync(BackendRoles.Admin, [PlatformPermission]);
        return store;
    }

    private static ClaimsPrincipal Session(string? scope, string role)
    {
        var claims = new List<Claim>
        {
            new("sub", "user-1"),
            new(ClaimTypes.Role, role)
        };

        if (scope is not null)
        {
            claims.Add(new Claim(BackendClaimTypes.CalloraScope, scope));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }
}

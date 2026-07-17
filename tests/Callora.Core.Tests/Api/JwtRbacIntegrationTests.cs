using Callora.Administration.Api;
using Callora.Core.Api;
using Callora.Core.Application.Audit;
using Callora.Core.Application.Entitlements;
using Callora.Core.Application.Lifecycle;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Policies;
using Callora.Core.Application.Security;
using Callora.Core.Infrastructure.Security;
using Callora.Core.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

namespace Callora.Core.Tests.Api;

public sealed class JwtRbacIntegrationTests
{
    [Fact]
    public async Task AdminJwt_HasAccessToApiSegment()
    {
        var options = CreateOptions();
        await using var app = await CreateAppAsync(options);
        var client = app.GetTestClient();
        AuthenticateWithBearer(client, CreateJwt(options, "admin-user", [BackendRoles.SuperAdmin]));

        var apiResponse = await client.GetAsync("/api/plugins/contracts/compatibility");
        Assert.Equal(HttpStatusCode.OK, apiResponse.StatusCode);
    }

    [Fact]
    public async Task UnknownRoleWithoutPermissions_IsForbidden()
    {
        var options = CreateOptions();
        await using var app = await CreateAppAsync(options);
        var client = app.GetTestClient();
        AuthenticateWithBearer(client, CreateJwt(options, "user-unknown", ["viewer"]));

        var response = await client.GetAsync("/api/plugins/contracts/compatibility");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RoleWithPluginCreateWithoutPluginDelete_CanInstallButCannotUninstall()
    {
        var options = CreateOptions();
        options.RbacRoles =
        [
            new BackendRbacRoleOptions
            {
                Role = "plugin.operator",
                Functions =
                [
                    new BackendRbacFunctionOptions
                    {
                        Function = "plugin",
                        Actions = ["create"]
                    }
                ]
            }
        ];

        await using var app = await CreateAppAsync(options);
        var client = app.GetTestClient();
        AuthenticateWithBearer(client, CreateJwt(options, "user-operator", ["plugin.operator"]));

        var install = await client.PostAsJsonAsync(
            "/api/plugins/install",
            new InstallPluginRequest("/tmp/plugin.dll", null, "tester"));
        var uninstall = await client.DeleteAsync("/api/plugins/plugin-x?requestedBy=tester");

        Assert.Equal(HttpStatusCode.BadRequest, install.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, uninstall.StatusCode);
    }

    [Fact]
    public async Task RbacAssignments_DefaultAdminOnly_ThenDelegatedRoleCanManageUsers()
    {
        var options = CreateOptions();
        options.RbacRoles =
        [
            new BackendRbacRoleOptions
            {
                Role = "plugin.operator",
                Functions =
                [
                    new BackendRbacFunctionOptions
                    {
                        Function = "plugin",
                        Actions = ["create"]
                    }
                ]
            }
        ];

        await using var app = await CreateAppAsync(options);
        var adminClient = app.GetTestClient();
        AuthenticateWithBearer(adminClient, CreateJwt(options, "admin-user", [BackendRoles.SuperAdmin]));

        var aliceClient = app.GetTestClient();
        AuthenticateWithBearer(aliceClient, CreateJwt(options, "alice", []));

        var aliceBeforeDelegation = await aliceClient.PutAsJsonAsync(
            "/api/security/rbac/users/bob",
            new RbacUserUpsertApiRequest("plugin.operator"));
        Assert.Equal(HttpStatusCode.Forbidden, aliceBeforeDelegation.StatusCode);

        // Delegating RBAC-user administration requires role.* (platform RBAC),
        // not user.* — the latter is the workspace-admin floor for /api/users.
        var createManagerRole = await adminClient.PutAsJsonAsync(
            "/api/security/rbac/roles/rbac.manager",
            new RbacRoleUpsertApiRequest(
            [
                new RbacFunctionActionApiRequest("role", ["read", "update"])
            ]));
        Assert.Equal(HttpStatusCode.OK, createManagerRole.StatusCode);

        var assignAlice = await adminClient.PutAsJsonAsync(
            "/api/security/rbac/users/alice",
            new RbacUserUpsertApiRequest("rbac.manager"));
        Assert.Equal(HttpStatusCode.OK, assignAlice.StatusCode);

        var aliceAfterDelegation = await aliceClient.PutAsJsonAsync(
            "/api/security/rbac/users/bob",
            new RbacUserUpsertApiRequest("plugin.operator"));
        Assert.Equal(HttpStatusCode.OK, aliceAfterDelegation.StatusCode);
    }

    [Fact]
    public async Task WorkspaceFloorPermissions_CannotManageGlobalRbacUsers()
    {
        // A workspace admin carries the user.* floor (WorkspaceRolePermissions)
        // for the workspace-scoped /api/users endpoints. That must NOT unlock the
        // global RBAC-user administration under /api/security/rbac/users, whose
        // BackendRbacUserRoles table has no workspace filter — otherwise a
        // workspace admin could read all platform assignments and grant itself
        // super admin. Those routes are gated on role.*, which is never in the floor.
        var options = CreateOptions();
        options.RbacRoles =
        [
            new BackendRbacRoleOptions
            {
                Role = "workspace.floor",
                Functions =
                [
                    new BackendRbacFunctionOptions { Function = "user", Actions = ["read", "update"] }
                ]
            }
        ];

        await using var app = await CreateAppAsync(options);
        var client = app.GetTestClient();
        AuthenticateWithBearer(client, CreateJwt(options, "workspace-admin", ["workspace.floor"]));

        var list = await client.GetAsync("/api/security/rbac/users");
        var upsert = await client.PutAsJsonAsync(
            "/api/security/rbac/users/bob",
            new RbacUserUpsertApiRequest("workspace.floor"));
        var delete = await client.DeleteAsync("/api/security/rbac/users/bob");

        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, upsert.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task WorkspaceScopedActivation_ForPluginOperator_IsNotBlockedByMembershipCheck()
    {
        var options = CreateOptions();
        options.RbacRoles =
        [
            new BackendRbacRoleOptions
            {
                Role = "plugin.operator",
                Functions =
                [
                    new BackendRbacFunctionOptions
                    {
                        Function = "plugin",
                        Actions = ["execute"]
                    }
                ]
            }
        ];

        await using var app = await CreateAppAsync(options);
        var aliceClient = app.GetTestClient();
        AuthenticateWithBearer(aliceClient, CreateJwt(options, "alice", ["plugin.operator"]));

        var response = await aliceClient.PostAsJsonAsync(
            "/api/plugins/voip/activate",
            new PluginLifecycleRequest("tester", "workspace-dialer"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static BackendHostOptions CreateOptions()
    {
        return new BackendHostOptions
        {
            JwtIssuer = "callora-tests",
            JwtAudience = "callora-host-api",
            JwtSigningKey = "callora-tests-signing-key-callora-tests-signing-key",
            EnableBootstrapApiKeys = false,
            RequireApiKeyAuthentication = true,
            ApiKeys = ["unused"],
            RbacRoles = []
        };
    }

    private static async Task<WebApplication> CreateAppAsync(
        BackendHostOptions options)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddSingleton(options);
        builder.Services.AddBackendApiSecurity(options);
        builder.Services.AddSingleton<IPluginLifecycleService, StaticPluginLifecycleService>();
        builder.Services.AddSingleton<IHostAuditStore, InMemoryHostAuditStore>();
        builder.Services.AddSingleton<IPluginEntitlementStore>(new InMemoryPluginEntitlementStore(options));
        builder.Services.AddSingleton<IPluginSignatureTrustStore, StaticPluginSignatureTrustStore>();
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapPluginEndpoints();
        app.MapRbacEndpoints();

        await app.StartAsync();
        return app;
    }

    private static void AuthenticateWithBearer(HttpClient client, string jwt)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
    }

    private static string CreateJwt(BackendHostOptions options, string subject, IReadOnlyList<string> roles)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.JwtSigningKey));

        var claims = new List<Claim>
        {
            new("sub", subject)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(30),
            Issuer = options.JwtIssuer,
            Audience = options.JwtAudience,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };

        return tokenHandler.WriteToken(tokenHandler.CreateToken(descriptor));
    }
}

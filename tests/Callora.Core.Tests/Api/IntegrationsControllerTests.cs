using Callora.Core.Application.Audit;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Callora.Core.Api;
using Callora.Core.Api.Admin.Integrations;
using Callora.Core.Application.Integrations;
using Callora.Core.Application.Policies;
using Callora.Core.Infrastructure.Security;
using Callora.Core.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Callora.Core.Tests.Api;

/// <summary>
/// Controller-model pilot for Phase C: verifies the IntegrationsController and the
/// [CalloraPermission] attribute over the real auth pipeline (TestServer).
/// </summary>
public sealed class IntegrationsControllerTests
{
    private const string ReaderRole = "integration-reader";

    [Fact]
    public async Task List_WithoutAuthentication_Returns401()
    {
        var options = CreateOptions();
        await using var app = await CreateAppAsync(options, new InMemoryIntegrationCredentialStore());
        var client = app.GetTestClient();

        var response = await client.GetAsync("/api/security/integrations");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task List_AuthenticatedWithoutPermission_Returns403()
    {
        var options = CreateOptions();
        await using var app = await CreateAppAsync(options, new InMemoryIntegrationCredentialStore());
        var client = app.GetTestClient();
        Authenticate(client, CreateJwt(options, "viewer-user", ["viewer"]));

        var response = await client.GetAsync("/api/security/integrations");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_WithReadPermission_Returns200_ButCreateIsForbidden()
    {
        var options = CreateOptions();
        await using var app = await CreateAppAsync(options, new InMemoryIntegrationCredentialStore());
        var client = app.GetTestClient();
        Authenticate(client, CreateJwt(options, "reader-user", [ReaderRole]));

        var list = await client.GetAsync("/api/security/integrations");
        var create = await client.PostAsJsonAsync(
            "/api/security/integrations",
            new IntegrationCreateApiRequest("billing", ReaderRole, "platform", null));

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task Create_SuperAdmin_ReturnsKeyOnce_AndAppearsInList()
    {
        var options = CreateOptions();
        var store = new InMemoryIntegrationCredentialStore();
        await using var app = await CreateAppAsync(options, store);
        var client = app.GetTestClient();
        Authenticate(client, CreateJwt(options, "admin", [BackendRoles.SuperAdmin]));

        var create = await client.PostAsJsonAsync(
            "/api/security/integrations",
            new IntegrationCreateApiRequest("billing", ReaderRole, "platform", null));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var created = await create.Content.ReadFromJsonAsync<IntegrationCreatedApiResponse>();
        Assert.NotNull(created);
        Assert.StartsWith(IntegrationApiKey.Prefix, created!.ApiKey, StringComparison.Ordinal);

        var list = await client.GetFromJsonAsync<IntegrationApiResponse[]>("/api/security/integrations");
        Assert.NotNull(list);
        Assert.Contains(list!, i => i.Name == "billing" && i.Role == ReaderRole);
    }

    [Fact]
    public async Task Create_WithOperatorRole_Returns400()
    {
        var options = CreateOptions();
        await using var app = await CreateAppAsync(options, new InMemoryIntegrationCredentialStore());
        var client = app.GetTestClient();
        Authenticate(client, CreateJwt(options, "admin", [BackendRoles.SuperAdmin]));

        var response = await client.PostAsJsonAsync(
            "/api/security/integrations",
            new IntegrationCreateApiRequest("god", BackendRoles.SuperAdmin, "platform", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static BackendHostOptions CreateOptions() => new()
    {
        JwtIssuer = "callora-tests",
        JwtAudience = "callora-host-api",
        JwtSigningKey = "callora-tests-signing-key-callora-tests-signing-key",
        EnableBootstrapApiKeys = false,
        RequireApiKeyAuthentication = true,
        ApiKeys = ["unused"],
        RbacRoles =
        [
            new BackendRbacRoleOptions
            {
                Role = ReaderRole,
                Functions = [new BackendRbacFunctionOptions { Function = "integration", Actions = ["read"] }]
            }
        ]
    };

    private static async Task<WebApplication> CreateAppAsync(
        BackendHostOptions options,
        InMemoryIntegrationCredentialStore integrationStore)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddSingleton(options);
        builder.Services.AddBackendApiSecurity(options);
        builder.Services.AddControllers().AddApplicationPart(typeof(IntegrationsController).Assembly);
        builder.Services.AddSingleton<IIntegrationCredentialStore>(integrationStore);
        builder.Services.AddSingleton<IHostAuditStore, InMemoryHostAuditStore>();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }

    private static void Authenticate(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static string CreateJwt(BackendHostOptions options, string subject, IReadOnlyList<string> roles)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.JwtSigningKey));

        var claims = new List<Claim> { new("sub", subject) };
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

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

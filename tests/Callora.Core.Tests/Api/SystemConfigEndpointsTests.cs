using Callora.Administration.Api;
using Callora.Core.Application.Configuration;
using Callora.Core.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Callora.Core.Tests.Api;

/// <summary>
/// The scoped configuration surface. Values resolve workspace &gt; tenant &gt;
/// global &gt; default, and the effective view must answer for the scope the
/// caller asks about — otherwise an operator edits a tenant value while looking
/// at the global one.
/// </summary>
public sealed class SystemConfigEndpointsTests
{
    private const string PluginId = "demo.plugin";

    [Fact]
    public async Task Effective_WithoutScope_ReturnsDefaultsAndGlobalValues()
    {
        await using var app = await CreateAppAsync();
        var client = OperatorClient(app);

        await SaveAsync(client, SystemConfigScopes.Global, scopeKey: null, ("greeting", "\"hallo global\""));

        var effective = await GetEffectiveAsync(client);

        Assert.Equal("\"hallo global\"", effective["greeting"]);
        // Untouched keys still read their definition default.
        Assert.Equal("42", effective["retries"]);
    }

    [Fact]
    public async Task Effective_ForTenant_AppliesTheTenantOverride()
    {
        await using var app = await CreateAppAsync();
        var client = OperatorClient(app);
        await SaveAsync(client, SystemConfigScopes.Global, scopeKey: null, ("greeting", "\"hallo global\""));
        await SaveAsync(client, SystemConfigScopes.Tenant, "tenant-a", ("greeting", "\"hallo tenant\""));

        var effective = await GetEffectiveAsync(client, tenantKey: "tenant-a");

        Assert.Equal("\"hallo tenant\"", effective["greeting"]);
    }

    [Fact]
    public async Task Effective_ForWorkspace_PrefersWorkspaceOverTenantAndGlobal()
    {
        await using var app = await CreateAppAsync();
        var client = OperatorClient(app);
        await SaveAsync(client, SystemConfigScopes.Global, scopeKey: null, ("greeting", "\"hallo global\""));
        await SaveAsync(client, SystemConfigScopes.Tenant, "tenant-a", ("greeting", "\"hallo tenant\""));
        await SaveAsync(client, SystemConfigScopes.Workspace, "workspace-a", ("greeting", "\"hallo workspace\""));

        var effective = await GetEffectiveAsync(client, tenantKey: "tenant-a", workspaceKey: "workspace-a");

        Assert.Equal("\"hallo workspace\"", effective["greeting"]);
    }

    [Fact]
    public async Task Effective_ForWorkspace_FallsBackToTheTenantValueWhenTheWorkspaceHasNone()
    {
        await using var app = await CreateAppAsync();
        var client = OperatorClient(app);
        await SaveAsync(client, SystemConfigScopes.Tenant, "tenant-a", ("greeting", "\"hallo tenant\""));

        var effective = await GetEffectiveAsync(client, tenantKey: "tenant-a", workspaceKey: "workspace-a");

        Assert.Equal("\"hallo tenant\"", effective["greeting"]);
    }

    [Fact]
    public async Task Effective_ForTenant_IsOperatorOnly()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        // A workspace-bound admin: permission granted, but no platform reach.
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "config.read");
        client.DefaultRequestHeaders.Add("X-Test-Workspace-Key", "workspace-a");

        var response = await client.GetAsync(
            $"/api/config/effective?pluginId={PluginId}&tenantKey=tenant-a&workspaceKey=workspace-a");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Effective_EchoesTheScopeItAnsweredFor()
    {
        await using var app = await CreateAppAsync();
        var client = OperatorClient(app);

        var response = await client.GetFromJsonAsync<JsonElement>(
            $"/api/config/effective?pluginId={PluginId}&tenantKey=tenant-a&workspaceKey=workspace-a");

        Assert.Equal("tenant-a", response.GetProperty("tenantKey").GetString());
        Assert.Equal("workspace-a", response.GetProperty("workspaceKey").GetString());
    }

    [Fact]
    public async Task SaveValues_ForWorkspace_IsAllowedForTheBoundAdmin()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "config.update,config.read");
        client.DefaultRequestHeaders.Add("X-Test-Workspace-Key", "workspace-a");

        var response = await client.PutAsJsonAsync(
            "/api/config/values",
            new UpsertSystemConfigValuesApiRequest(
                PluginId,
                SystemConfigScopes.Workspace,
                "workspace-a",
                ValuesFor(("greeting", "\"hallo\""))));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task SaveValues_ForAnotherWorkspace_IsRefused()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "config.update");
        client.DefaultRequestHeaders.Add("X-Test-Workspace-Key", "workspace-a");

        var response = await client.PutAsJsonAsync(
            "/api/config/values",
            new UpsertSystemConfigValuesApiRequest(
                PluginId,
                SystemConfigScopes.Workspace,
                "workspace-b",
                ValuesFor(("greeting", "\"hallo\""))));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SaveValues_RejectsAScopedWriteWithoutAScopeKey()
    {
        await using var app = await CreateAppAsync();
        var client = OperatorClient(app);

        var response = await client.PutAsJsonAsync(
            "/api/config/values",
            new UpsertSystemConfigValuesApiRequest(
                PluginId,
                SystemConfigScopes.Workspace,
                ScopeKey: null,
                ValuesFor(("greeting", "\"hallo\""))));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static HttpClient OperatorClient(WebApplication app)
    {
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Permissions", "config.read,config.update");
        client.DefaultRequestHeaders.Add("X-Test-Callora-Scope", "platform");
        return client;
    }

    private static Dictionary<string, JsonElement?> ValuesFor(params (string Key, string Json)[] values) =>
        values.ToDictionary(
            pair => pair.Key,
            pair => (JsonElement?)JsonSerializer.Deserialize<JsonElement>(pair.Json),
            StringComparer.OrdinalIgnoreCase);

    private static async Task SaveAsync(
        HttpClient client,
        string scope,
        string? scopeKey,
        params (string Key, string Json)[] values)
    {
        var response = await client.PutAsJsonAsync(
            "/api/config/values",
            new UpsertSystemConfigValuesApiRequest(PluginId, scope, scopeKey, ValuesFor(values)));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<Dictionary<string, string>> GetEffectiveAsync(
        HttpClient client,
        string? tenantKey = null,
        string? workspaceKey = null)
    {
        var query = $"/api/config/effective?pluginId={PluginId}";
        if (tenantKey is not null)
        {
            query += $"&tenantKey={tenantKey}";
        }

        if (workspaceKey is not null)
        {
            query += $"&workspaceKey={workspaceKey}";
        }

        var payload = await client.GetFromJsonAsync<JsonElement>(query);
        return payload.GetProperty("valuesByKey")
            .EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetString() ?? string.Empty);
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var store = new InMemorySystemConfigStore();
        await store.ReplaceDefinitionsForPluginAsync(
            PluginId,
            "1.0.0",
            [
                new SystemConfigDefinitionInput("greeting", "Begrüßung", "text", null, "\"hallo default\"", null, null, 10, true),
                new SystemConfigDefinitionInput("retries", "Versuche", "number", null, "42", null, null, 20, true),
                new SystemConfigDefinitionInput("token", "Token", "secret", null, null, null, null, 30, true),
            ]);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication("Header")
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>("Header", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<ISystemConfigStore>(store);
        builder.Services.AddSingleton<SystemConfigResolver>();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapSystemConfigEndpoints();
        await app.StartAsync();
        return app;
    }
}

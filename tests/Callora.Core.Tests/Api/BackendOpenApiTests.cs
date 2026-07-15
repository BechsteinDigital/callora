using System.Net;
using Callora.Administration.Api.Admin.Integrations;
using Callora.Core.Api.OpenApi;
using Callora.Core.Application.Policies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Callora.Core.Tests.Api;

/// <summary>
/// Verifies the Microsoft.AspNetCore.OpenApi pipeline (Phase C0): both documents
/// generate at runtime and the API-key security scheme transformer runs.
/// </summary>
public sealed class BackendOpenApiTests
{
    [Fact]
    public async Task ApiDocument_IsGenerated_WithApiKeySecurityScheme()
    {
        var options = new BackendHostOptions { ApiKeyHeaderName = "X-Callora-Api-Key" };
        await using var app = await CreateAppAsync(options);
        var client = app.GetTestClient();

        var response = await client.GetAsync("/openapi/api.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("securitySchemes", json, StringComparison.Ordinal);
        Assert.Contains(options.ApiKeyHeaderName, json, StringComparison.Ordinal);
        // The platform document includes the non-workspace IntegrationsController route.
        Assert.Contains("/api/security/integrations", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorkspaceDocument_IsGenerated()
    {
        var options = new BackendHostOptions { ApiKeyHeaderName = "X-Callora-Api-Key" };
        await using var app = await CreateAppAsync(options);
        var client = app.GetTestClient();

        var response = await client.GetAsync("/openapi/workspace.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<WebApplication> CreateAppAsync(BackendHostOptions options)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddSingleton(options);
        builder.Services.AddControllers().AddApplicationPart(typeof(IntegrationsController).Assembly);
        builder.Services.AddBackendOpenApi();

        var app = builder.Build();
        app.MapControllers();
        app.MapOpenApi();
        await app.StartAsync();
        return app;
    }
}

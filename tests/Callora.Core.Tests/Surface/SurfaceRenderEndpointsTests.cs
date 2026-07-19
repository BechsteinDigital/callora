using Callora.Core.Application.Workspaces;
using Callora.Core.Tests.Support;
using Callora.Surface.Rendering;
using Callora.Surface.Rendering.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using Xunit;

namespace Callora.Core.Tests.Surface;

public sealed class SurfaceRenderEndpointsTests
{
    [Fact]
    public async Task Render_ResolvesWorkspaceByHost_ReturnsRenderedHtml()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://acme.example.de/");

        var response = await client.GetAsync("/surface/render");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", response.Content.Headers.ContentType!.ToString());
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("id=\"callora-app\"", html, StringComparison.Ordinal);
        Assert.Contains("data-workspace=\"acme\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_UnknownHost_ReturnsNotFound()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://unknown.example.de/");

        var response = await client.GetAsync("/surface/render");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var store = new InMemoryWorkspaceManagementStore();
        store.AddTenant("tenant-a");
        _ = await store.UpsertAsync("tenant-a", "acme", "Acme", "spa", isActive: true, publicBaseUrl: "https://acme.example.de");

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IWorkspaceManagementStore>(store);
        builder.Services.AddCalloraSurfaceRendering();

        var app = builder.Build();
        app.MapSurfaceRenderEndpoints();
        await app.StartAsync();
        return app;
    }
}

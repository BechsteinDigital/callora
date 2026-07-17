using Callora.Administration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using System.Net;
using Xunit;

namespace Callora.Core.Tests.Api;

public sealed class AdminSpaServingTests
{
    [Fact]
    public async Task AdminDeepLink_ReturnsSpaIndexHtml()
    {
        var webRoot = Directory.CreateTempSubdirectory("callora-admin-spa");
        var adminDir = Directory.CreateDirectory(Path.Combine(webRoot.FullName, "admin"));
        await File.WriteAllTextAsync(
            Path.Combine(adminDir.FullName, "index.html"),
            "<!doctype html><title>callora-admin</title>");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { WebRootPath = webRoot.FullName });
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        app.UseStaticFiles();
        app.MapAdminSpaFallback();
        await app.StartAsync();

        var client = app.GetTestClient();
        var response = await client.GetAsync("/admin/settings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("callora-admin", body);

        await app.StopAsync();
        webRoot.Delete(recursive: true);
    }

    [Fact]
    public async Task AdminRoute_OutranksStorefrontCatchAll_NoRedirectLoop()
    {
        var webRoot = Directory.CreateTempSubdirectory("callora-admin-spa-prio");
        var adminDir = Directory.CreateDirectory(Path.Combine(webRoot.FullName, "admin"));
        await File.WriteAllTextAsync(
            Path.Combine(adminDir.FullName, "index.html"),
            "<!doctype html><title>callora-admin</title>");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { WebRootPath = webRoot.FullName });
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        app.UseStaticFiles();
        app.MapAdminSpaFallback();
        // The workspace storefront catch-all would otherwise intercept /admin and
        // self-redirect it to AdminShellBaseUrl ("/admin/") — an infinite loop. The
        // concrete admin route must outrank it.
        app.MapGet("/{**path:nonfile}", () => Results.Redirect("/admin/"));
        await app.StartAsync();

        var client = app.GetTestClient();
        var response = await client.GetAsync("/admin/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("callora-admin", await response.Content.ReadAsStringAsync());

        await app.StopAsync();
        webRoot.Delete(recursive: true);
    }
}

using Callora.Administration;
using Microsoft.AspNetCore.Builder;
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
}

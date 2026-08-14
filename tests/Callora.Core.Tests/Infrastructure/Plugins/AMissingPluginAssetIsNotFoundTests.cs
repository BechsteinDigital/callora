using Callora.Core.Infrastructure.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using System.Net;
using Xunit;

namespace Callora.Core.Tests.Infrastructure.Plugins;

/// <summary>
/// Eine Datei, die es unter <c>/plugin-assets/</c> nicht gibt, endet mit 404 — und wird nicht an
/// die Middleware dahinter weitergereicht.
/// </summary>
/// <remarks>
/// <para>
/// Der Anlass (#306): Ohne diesen Abschluss lief die Anfrage weiter in die Authentifizierung und
/// bekam 401 mit leerem Body. Ein leerer Body hat keinen Content-Type, und der Browser meldete
/// daraufhin „Refused to execute script … its MIME type ('') is not executable". Gesucht wurde
/// danach in den Auslieferungsoptionen, in der Content-Type-Zuordnung und in der CSP — die Ursache
/// war eine Datei, die das Bundle nie mitgebracht hatte.
/// </para>
/// <para>
/// Geprüft wird beides: dass eine fehlende Datei 404 ergibt UND dass eine vorhandene weiterhin
/// ausgeliefert wird. Ein Abschluss, der zu früh greift, wäre die schlimmere Fassung desselben
/// Fehlers — dann läge jedes Bundle daneben.
/// </para>
/// </remarks>
public sealed class AMissingPluginAssetIsNotFoundTests
{
    [Fact]
    public async Task AFileThatWasNeverPublishedAnswersWith404()
    {
        await using var app = await StartedHostAsync();

        var response = await app.GetTestClient()
            .GetAsync("/plugin-assets/videoconference/app/surface/vendor/mediapipe/wasm/vision_wasm_nosimd_internal.js");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Die Gegenprobe, ohne die der Test oben auch dann bestünde, wenn der Abschluss ALLES
    /// abwiese — dann wäre kein einziges Plugin-Bundle mehr erreichbar.
    /// </summary>
    [Fact]
    public async Task APublishedFileIsStillServed()
    {
        await using var app = await StartedHostAsync(publish: true);

        var response = await app.GetTestClient().GetAsync("/plugin-assets/demo/app/admin/main.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("/* bundle */", await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Der 401 kam aus der Middleware HINTER der Auslieferung. Der Test baut sie deshalb mit auf —
    /// ohne sie liefe er gegen eine Pipeline, in der es den Fehler nie gegeben hätte.
    /// </summary>
    private static async Task<WebApplication> StartedHostAsync(bool publish = false)
    {
        var contentRoot = Directory.CreateTempSubdirectory("callora-asset-404-").FullName;
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = contentRoot });
        builder.WebHost.UseTestServer();
        var app = builder.Build();

        if (publish)
        {
            var published = Path.Combine(
                PluginAssetWebRoot.Resolve(app.Environment), "plugin-assets", "demo", "app", "admin");
            Directory.CreateDirectory(published);
            await File.WriteAllTextAsync(Path.Combine(published, "main.js"), "/* bundle */");
        }

        PluginAssetStaticFiles.Use(app, app.Environment);

        // Steht für alles, was in der echten Pipeline folgt und eine durchgereichte Adresse für
        // etwas anderes hält, als sie ist.
        app.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        });

        await app.StartAsync();
        return app;
    }
}

using System.Net;
using Callora.Core.Infrastructure.Plugins;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Xunit;

namespace Callora.Core.Tests.Infrastructure.Plugins;

/// <summary>
/// Ein Plugin-Bundle wird auch dann ausgeliefert, wenn das Projekt kein
/// <c>wwwroot/</c>-Verzeichnis hat.
/// </summary>
/// <remarks>
/// Genau dieser Fall traf jede Distribution: Ein Kompositionsprojekt bezieht seine statischen
/// Dateien aus Paketen und legt selbst kein <c>wwwroot/</c> an, also ist
/// <c>IWebHostEnvironment.WebRootPath</c> leer. Der Publisher wich auf
/// <c>AppContext.BaseDirectory/wwwroot</c> aus und schrieb dorthin; die Auslieferung kannte
/// diesen Rückfall nicht und lieferte nichts aus.
///
/// <para>
/// Sichtbar wurde das als 404 auf jedes Plugin-Bundle und als Ladefehler für jedes Plugin in
/// der Admin-Shell — was nach einem Fehler in den Plugins aussah und keiner war. Zwei
/// Rückfälle, die sich nicht kennen, sind schlimmer als keiner.
/// </para>
/// </remarks>
public sealed class PluginAssetsAreServedWithoutAWebRootTests
{
    [Fact]
    public async Task ABundleWrittenByThePublisherIsServed()
    {
        var contentRoot = Directory.CreateTempSubdirectory("callora-no-webroot-").FullName;
        try
        {
            // WebRootPath wird bewusst NICHT gesetzt. Ein leerer String wäre falsch: ASP.NET
            // deutet ihn als "nimm den Content-Root" und der Fall träte nie ein. Ohne Angabe
            // und ohne wwwroot-Verzeichnis bleibt WebRootPath null — genau der Zustand eines
            // Kompositionsprojekts, das seine statischen Dateien aus Paketen bezieht.
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ContentRootPath = contentRoot
            });
            builder.WebHost.UseTestServer();
            await using var app = builder.Build();

            Assert.True(
                string.IsNullOrWhiteSpace(app.Environment.WebRootPath),
                $"Der Test prüft den Fall OHNE WebRoot; hier steht '{app.Environment.WebRootPath}'.");

            // Dieselbe Auflösung, die der Publisher benutzt.
            var published = Path.Combine(
                PluginAssetWebRoot.Resolve(app.Environment), "plugin-assets", "demo", "app", "admin");
            Directory.CreateDirectory(published);
            await File.WriteAllTextAsync(Path.Combine(published, "main.js"), "/* bundle */");

            PluginAssetStaticFiles.Use(app, app.Environment);
            await app.StartAsync();

            var response = await app.GetTestClient()
                .GetAsync("/plugin-assets/demo/app/admin/main.js");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/javascript", response.Content.Headers.ContentType?.MediaType);
            Assert.Contains("bundle", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);

            await app.StopAsync();
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }
}

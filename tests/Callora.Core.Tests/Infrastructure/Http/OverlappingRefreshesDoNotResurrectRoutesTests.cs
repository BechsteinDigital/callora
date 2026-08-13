using Callora.Core.Infrastructure.Http;
using Microsoft.AspNetCore.Routing;
using Callora.Core.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Callora.Core.Tests.Infrastructure.Http;

/// <summary>
/// Zwei Neubauten der Plugin-Routentabelle, die sich überlappen.
/// </summary>
/// <remarks>
/// <para>
/// <c>Refresh</c> hängt an einem Lebenszyklus-Ereignis, und der Ereignis-Verteiler serialisiert nur
/// die Abonnenten EINES Ereignisses — zwei Ereignisse kurz nacheinander laufen nebenläufig. Wer
/// dabei den Katalog früher liest, aber später schreibt, gewinnt: Die Tabelle enthält danach ein
/// Plugin, das inzwischen deaktiviert ist, und zwar bis zum nächsten Ereignis.
/// </para>
/// <para>
/// Zwei Kosten, nicht eine. Die sichtbare: Die Routen des deaktivierten Plugins antworten weiter.
/// Die teurere: Der Delegat jeder Route hält seine Controller-Instanz fest, also hält die Tabelle
/// einen Ladekontext am Leben, der entladen werden sollte — ein Leck, das weit entfernt von seiner
/// Ursache auffällt (ADR-013, Punkt 4 der Prüfliste in #272).
/// </para>
/// </remarks>
public sealed class OverlappingRefreshesDoNotResurrectRoutesTests
{
    [Fact]
    public async Task ADeactivatedPluginDoesNotComeBackThroughAnOlderRebuild()
    {
        var catalog = new GatedPluginCatalog();
        catalog.SetExports(new TestPluginAdminController(), new DepartingPluginController());
        var dataSource = new PluginApiEndpointDataSource(
            catalog,
            NullLogger<PluginApiEndpointDataSource>.Instance);

        catalog.GateNextRead();

        // Der langsame Neubau: liest beide Plugins und hält dann an.
        var slowRebuild = Task.Run(dataSource.Refresh);
        catalog.WaitUntilGated();

        // Währenddessen wird das zweite Plugin deaktiviert und ein Neubau dafür angestoßen.
        catalog.SetExports(new TestPluginAdminController());
        var fastRebuild = Task.Run(dataSource.Refresh);

        // Dem zweiten Neubau Zeit geben anzulaufen. Die Wartezeit entscheidet den Test nicht: Ohne
        // die Serialisierung ist er in Millisekunden fertig und schreibt vor dem langsamen; mit ihr
        // steht er am Lock und kann gar nicht vorbeiziehen. Zu kurz gewartet hieße also höchstens,
        // dass ein vorhandener Fehler durchrutscht — nie, dass korrekter Code rot wird.
        await Task.Delay(TimeSpan.FromMilliseconds(200));

        catalog.Release();
        await Task.WhenAll(slowRebuild, fastRebuild);

        var paths = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .OfType<string>()
            .ToArray();

        Assert.DoesNotContain(DepartingPluginController.RoutePath, paths);
        Assert.Contains("/api/test-plugin/ping", paths);
    }
}

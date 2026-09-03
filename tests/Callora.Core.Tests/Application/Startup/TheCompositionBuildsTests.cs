using Callora.Administration;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Security;
using Callora.Core.Infrastructure.DependencyInjection;
using Callora.Surface.Rendering;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Callora.Core.Tests.Application.Startup;

/// <summary>
/// Die Zusammenstellung des Hosts lässt sich bauen — mit Lifetime-Validierung.
/// </summary>
/// <remarks>
/// <para>
/// <b>Der Befund:</b> <c>src/Host/Dev/Program.cs</c> trägt den Kommentar, es falle auf, „wenn jemand
/// <c>AddCalloraHost</c> oder die Reihenfolge der Modulaufrufe bricht". Das stimmte nur für
/// Compile-Fehler. Eine fehlende Registrierung übersetzt sauber und scheitert erst bei
/// <c>builder.Build()</c> — und den Aufruf machte in der CI niemand. Ein Dienst, der im Testhost von
/// Hand registriert wird und in der echten Komposition fehlt, kam damit grün durch die Suite und
/// hätte den Start des Dev-Stacks abgeräumt.
/// </para>
/// <para>
/// <c>AddCalloraHost</c> setzt <c>ValidateScopes</c> und <c>ValidateOnBuild</c>, deshalb sagt schon
/// der Bau etwas: Er prüft jede <em>registrierte</em> Abhängigkeit auf Auflösbarkeit und auf einen
/// von einem Singleton gefangenen Scoped-Dienst.
/// </para>
/// <para>
/// <b>Und genau da hört er auf</b> — nachgemessen, nicht angenommen: Wird
/// <c>PluginSelfService</c> aus der Zusammenstellung entfernt, baut der Host weiter. Ein Dienst, der
/// nirgends registriert ist, wird auch nicht validiert, und ein <c>[FromServices]</c>-Parameter
/// eines Controllers wird erst beim Aufruf aufgelöst. Deshalb der zweite Test, der die Dienste
/// beim Namen nennt: Was nur über <c>[FromServices]</c> konsumiert wird, muss hier stehen, sonst
/// deckt es niemand.
/// </para>
/// <para>
/// Es wird nichts verbunden: Die Verbindungszeichenfolge muss nur gültig aussehen, denn ein
/// <c>DbContext</c> öffnet beim Auflösen keine Verbindung.
/// </para>
/// </remarks>
public sealed class TheCompositionBuildsTests
{
    [Fact]
    public void TheHostComposesWithoutAMissingOrCaptiveRegistration()
    {
        using var app = BuildHost();

        Assert.NotNull(app.Services);
    }

    /// <summary>
    /// Die Dienste der Mandantenebene lösen aus der echten Zusammenstellung auf, nicht nur aus den
    /// von Hand zusammengesetzten Testhosts.
    /// </summary>
    [Fact]
    public void TheTenantLevelServicesResolveFromTheRealComposition()
    {
        using var app = BuildHost();
        using var scope = app.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<WorkspaceReach>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<PluginSelfService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ITenantPluginDelegationStore>());
    }

    // Dieselben Aufrufe in derselben Reihenfolge wie src/Host/Dev/Program.cs. Weicht der Test davon
    // ab, prüft er eine Zusammenstellung, die niemand ausliefert.
    private static WebApplication BuildHost()
    {
        var builder = WebApplication.CreateBuilder();

        // Dieselben Schlüssel, die docker-compose.yml setzt — der Abschnitt heißt BackendHost.
        builder.Configuration["BackendHost:DatabaseConnectionString"] =
            "Host=localhost;Database=callora-composition-test;Username=u;Password=p";
        builder.Configuration["BackendHost:ApiKeys:0"] = "composition-test-key";

        builder.AddCalloraHost();
        builder.AddCalloraAdministration();
        builder.Services.AddCalloraSurfaceRendering();

        return builder.Build();
    }
}

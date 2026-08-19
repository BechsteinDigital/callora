using Callora.Core.Application.Policies;
using Callora.Core.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Callora.Core.Tests.Infrastructure.Persistence;

/// <summary>
/// ASP.NET löst <see cref="IStringLocalizerFactory"/> beim Aufbau der MvcOptions auf, also aus
/// der WURZEL des Containers und nicht aus einem Anfrage-Scope. Eine scoped Registrierung ist
/// deshalb nicht bloß unsauber, sondern verhindert den Start: Die DI-Prüfung meldet
/// "Cannot consume scoped service … from singleton 'IOptions&lt;MvcOptions&gt;'", und der Host
/// beendet sich mit Code 134, bevor er einen Port öffnet.
///
/// Das ist genau einmal passiert und blieb drei Tage unbemerkt, weil der Dev-Container weiter
/// "Up" meldete: dotnet watch fängt den Absturz ab und wartet auf eine Dateiänderung. Von außen
/// sah der Stack gesund aus.
/// </summary>
public sealed class TheLocalizerFactoryResolvesFromTheRootTests
{
    /// <summary>
    /// Die Zusage, die verletzt war: Diese Registrierung MUSS ein Singleton sein.
    /// </summary>
    /// <remarks>
    /// Geprüft wird der Deskriptor, nicht ein vollständig aufgebauter Container. Ein Versuch,
    /// hier <c>AddControllers()</c> mitzuregistrieren und <c>ValidateOnBuild</c> laufen zu
    /// lassen, scheitert an Diensten, die erst der WebApplicationBuilder mitbringt
    /// (<c>IWebHostEnvironment</c>, <c>IMemoryCache</c>) — der Test wäre dann rot, ohne dass
    /// etwas kaputt ist, und das ist schlimmer als kein Test.
    ///
    /// Den echten Startfall deckt der Smoke-Job in release.yml ab: Er startet den Host gegen
    /// eine echte Datenbank und wartet auf /ready. Dieser Schutz existierte bereits, als der
    /// Fehler entstand — er lief nur nie, weil das Repository keine Tags hatte und der Job
    /// an einem v*-Tag hängt.
    /// </remarks>
    [Fact]
    public void TheFactoryIsRegisteredAsASingleton()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackendPersistence(new BackendHostOptions
        {
            DatabaseConnectionString = "Host=localhost;Database=x;Username=u;Password=p"
        });

        var descriptor = Assert.Single(
            services,
            service => service.ServiceType == typeof(IStringLocalizerFactory));

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void StringLocalizerFactory_ResolvesWithoutAScope()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackendPersistence(new BackendHostOptions
        {
            DatabaseConnectionString = "Host=localhost;Database=x;Username=u;Password=p"
        });

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });

        // Bewusst OHNE CreateScope: Das ist die Auflösung, die MVC durchführt.
        var factory = provider.GetRequiredService<IStringLocalizerFactory>();

        Assert.NotNull(factory);
    }

    /// <summary>
    /// Ohne laufende Anfrage gibt es keinen Katalog. Der Vertrag sieht dafür bereits das
    /// richtige Verhalten vor — ein unbekannter Schlüssel kommt als sein eigener Name zurück,
    /// mit <c>ResourceNotFound</c> —, und genau das muss auch hier gelten statt einer
    /// NullReferenceException in einem Hintergrunddienst.
    /// </summary>
    [Fact]
    public void OutsideARequest_TheLocalizerFallsBackToTheKey()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBackendPersistence(new BackendHostOptions
        {
            DatabaseConnectionString = "Host=localhost;Database=x;Username=u;Password=p"
        });

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });

        var localizer = provider.GetRequiredService<IStringLocalizerFactory>()
            .Create("whatever", "wherever");
        var result = localizer["callora.unknown.key"];

        Assert.True(result.ResourceNotFound);
        Assert.Equal("callora.unknown.key", result.Value);
    }
}

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
    /// Der Fall, der den Host wirklich tötet. Die beiden Tests darunter prüfen einzelne
    /// Registrierungen; erst zusammen mit MVC entsteht die Kette, die beim Start scheitert:
    /// <c>AddControllers</c> bringt Konfiguratoren für <c>MvcOptions</c> mit, die den Localizer
    /// aus einem Singleton ziehen. Ein Test ohne sie ist grün, während der Prozess nicht startet
    /// — genau so ist der Fehler drei Tage lang durchgerutscht.
    /// </summary>
    [Fact]
    public void WithMvc_TheWholeContainerValidates()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        services.AddBackendPersistence(new BackendHostOptions
        {
            DatabaseConnectionString = "Host=localhost;Database=x;Username=u;Password=p"
        });

        // ValidateOnBuild ist, was ASP.NET in Development beim Start tut.
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        Assert.NotNull(provider);
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

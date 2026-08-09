using Callora.Core.Application.Policies;
using Callora.Core.Application.Surfaces;
using Callora.Surface.Rendering;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Callora.Core.Tests.Hosting;

/// <summary>
/// Jeder Singleton muss sich am Wurzelanbieter bauen lassen.
/// </summary>
/// <remarks>
/// <c>SurfaceContextRevalidator</c> war ein Singleton, dessen Factory den scoped
/// <c>SurfaceSessionAuthenticator</c> aus dem Wurzelanbieter zog. Das ist nicht nur eine
/// Prüfungsverletzung: Der Revalidator lebt für den ganzen Prozess und hätte den DbContext
/// einer einzelnen Anfrage über deren Lebensdauer hinaus festgehalten.
///
/// <para>
/// Aufgefallen ist es als <b>500 auf einer beliebigen Anfrage</b> — hier auf der Raumliste des
/// Videokonferenz-Plugins, also so weit weg von der Registrierung, wie es nur geht. Der Host
/// startete sauber; erst der erste Controller, dessen Modellbinder einen Dienst auflöste, brachte
/// den Graphen zum Bauen.
/// </para>
///
/// <para>
/// Genau deshalb baut dieser Test nicht nur den Container, sondern löst jeden Singleton auch
/// wirklich auf. <c>ValidateOnBuild</c> prüft die deklarierten Konstruktoren; eine Factory, die
/// <c>GetService</c> selbst aufruft, ist für sie undurchsichtig — und Factories sind gerade die
/// Stelle, an der so ein Fehler entsteht.
/// </para>
/// </remarks>
public sealed class EverySingletonResolvesFromTheRootProviderTests
{
    [Fact]
    public async Task TheSurfaceRenderingSingletonsBuildWithoutAScope()
    {
        // Ein echter Web-Builder, weil die Renderschicht IWebHostEnvironment braucht — und weil
        // ein anderes Umfeld als das des Hosts genau die Fehler durchließe, um die es hier geht.
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddSingleton(new BackendHostOptions());
        // Der Authenticator MUSS registriert sein, sonst prüft der Test nichts: Eine Factory, die
        // einen nicht registrierten Dienst holt, bekommt null zurück statt einer Ausnahme — und
        // der Fehler, um den es geht, kann in so einer Zusammenstellung gar nicht auftreten.
        // Scoped, wie im Host (CalloraHostCompositionExtensions).
        builder.Services.AddScoped<SurfaceSessionAuthenticator>(_ => null!);
        builder.Services.AddCalloraSurfaceRendering();

        // ValidateScopes ist die entscheidende Prüfung: Sie lehnt einen scoped Dienst am
        // Wurzelanbieter ab. ValidateOnBuild bleibt AUS — es prüfte auch die MVC-Infrastruktur
        // des Frameworks und meldete dort Dinge, die kein Host je auflöst.
        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateScopes = true;
            options.ValidateOnBuild = false;
        });

        await using var app = builder.Build();
        var provider = app.Services;

        // Nur UNSERE Registrierungen: Was das Framework mitbringt, ist nicht unsere Zusicherung,
        // und ein Fremdfehler darin machte diesen Test zu einem, den man abschaltet.
        var singletons = builder.Services
            .Where(descriptor => descriptor.Lifetime == ServiceLifetime.Singleton)
            .Select(descriptor => descriptor.ServiceType)
            .Where(type => type.Assembly == typeof(CalloraSurfaceRenderingExtensions).Assembly)
            .Where(type => !type.IsGenericTypeDefinition)
            .Distinct()
            .ToArray();

        Assert.NotEmpty(singletons);
        foreach (var serviceType in singletons)
        {
            var exception = Record.Exception(() => provider.GetService(serviceType));
            Assert.True(
                exception is null,
                $"""
                 Der Singleton {serviceType.Name} lässt sich am Wurzelanbieter nicht bauen:
                 {exception?.Message}
                 Eine Factory, die einen scoped Dienst holt, hält ihn über die Anfrage hinaus —
                 und meldet sich als 500 an einer beliebigen anderen Stelle.
                 """);
        }
    }
}

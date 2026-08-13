using Callora.Core.Infrastructure.DependencyInjection;
using Callora.Core.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Polly.CircuitBreaker;
using System.Net;
using Xunit;

namespace Callora.Core.Tests.Infrastructure.DependencyInjection;

/// <summary>
/// Die Resilienz-Kette des Webhook-Clients — geprüft an ihrer Wirkung, nicht an ihrer Registrierung.
/// </summary>
/// <remarks>
/// <para>
/// Eine Resilienz-Kette ist die Sorte Vorkehrung, die nur im Betrieb auffällt, wenn sie fehlt: Der
/// Aufruf funktioniert weiterhin, es wird nur nicht wiederholt und nicht unterbrochen. Ein Test auf
/// „ist registriert" hätte hier nichts geholfen — die drei Fallen unten lassen sich alle so bauen,
/// dass die Registrierung stimmt und die Wirkung ausbleibt.
/// </para>
/// <para>
/// Aufgebaut über <see cref="WebhookDeliveryResilienceExtensions.AddWebhookDeliveryResilience"/>,
/// also über dieselbe Methode, die die Composition-Root aufruft.
/// </para>
/// </remarks>
public sealed class WebhookDeliveryResilienceTests
{
    private const string ClientName = "resilience-test";

    /// <summary>
    /// Die teuerste der drei Fallen: <c>HttpClient.Timeout</c> umschließt den gesamten Aufruf
    /// einschließlich aller Wiederholungen. Stünde er noch auf den zehn Sekunden, die vorher die
    /// einzige Schutzmaßnahme waren, käme der erste Versuch durch und jede Wiederholung stürbe am
    /// Client — die Kette wäre eingebaut und wirkungslos.
    /// </summary>
    [Fact]
    public async Task AFailedDeliveryIsRetried()
    {
        var attempts = new CountingHttpMessageHandler(HttpStatusCode.InternalServerError);
        using var provider = BuildProvider(attempts);
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName);

        using var response = await client.PostAsync("https://receiver.example/hook", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(3, attempts.Count); // ein Versuch plus zwei Wiederholungen
    }

    /// <summary>
    /// Ein erfolgreicher Aufruf darf nicht wiederholt werden — die Gegenprobe, ohne die der obige
    /// Test auch bestünde, wenn blind dreimal gesendet würde.
    /// </summary>
    [Fact]
    public async Task ASuccessfulDeliveryIsSentOnce()
    {
        var attempts = new CountingHttpMessageHandler(HttpStatusCode.OK);
        using var provider = BuildProvider(attempts);
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName);

        using var response = await client.PostAsync("https://receiver.example/hook", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, attempts.Count);
    }

    /// <summary>
    /// Der Unterbrecher öffnet und hört auf, den toten Empfänger zu behelligen.
    /// </summary>
    /// <remarks>
    /// Mit den Vorgaben des Standard-Handlers wäre dieser Test nicht zu schreiben: Er verlangt 100
    /// Anfragen in 30 Sekunden, bevor er urteilt, und so viele Zustellungen gehen an einen einzelnen
    /// Empfänger nie. Der Test steht deshalb genau für die Abweichung — nimmt jemand die Vorgaben
    /// zurück, fällt er um, statt dass der Unterbrecher still zur Attrappe wird.
    /// </remarks>
    [Fact]
    public async Task ADeadReceiverStopsBeingCalled()
    {
        var attempts = new CountingHttpMessageHandler(HttpStatusCode.InternalServerError);
        using var provider = BuildProvider(attempts);
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName);

        for (var i = 0; i < 6; i++)
        {
            try
            {
                using var _ = await client.PostAsync("https://dead.example/hook", new StringContent("{}"));
            }
            catch (BrokenCircuitException)
            {
                // Ab hier hält der Unterbrecher die Zustellung an — genau das ist das Ergebnis.
            }
        }

        var afterBreak = attempts.Count;
        try
        {
            using var _ = await client.PostAsync("https://dead.example/hook", new StringContent("{}"));
        }
        catch (BrokenCircuitException)
        {
        }

        Assert.Equal(afterBreak, attempts.Count);
    }

    /// <summary>
    /// Ein toter Empfänger darf den anderen nicht die Zustellung nehmen. Ohne die Auswahl nach
    /// Authority teilen sich alle Empfänger einen Unterbrecher, weil der Client einer ist — das wäre
    /// schlechter als gar keiner.
    /// </summary>
    [Fact]
    public async Task ADeadReceiverDoesNotBlockTheOthers()
    {
        var attempts = new CountingHttpMessageHandler(request =>
            request.RequestUri!.Host == "dead.example" ? HttpStatusCode.InternalServerError : HttpStatusCode.OK);
        using var provider = BuildProvider(attempts);
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(ClientName);

        for (var i = 0; i < 8; i++)
        {
            try
            {
                using var _ = await client.PostAsync("https://dead.example/hook", new StringContent("{}"));
            }
            catch (BrokenCircuitException)
            {
            }
        }

        using var healthy = await client.PostAsync("https://healthy.example/hook", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.OK, healthy.StatusCode);
    }

    private static ServiceProvider BuildProvider(HttpMessageHandler primaryHandler)
    {
        var services = new ServiceCollection();
        services.AddHttpClient(ClientName, client => client.Timeout = TimeSpan.FromSeconds(60))
            .ConfigurePrimaryHttpMessageHandler(() => primaryHandler)
            .AddWebhookDeliveryResilience();
        return services.BuildServiceProvider();
    }
}

using Microsoft.Extensions.Http.Resilience;

namespace Callora.Core.Infrastructure.DependencyInjection;

/// <summary>
/// Wiederholung, Unterbrecher und Zeitgrenzen für die Webhook-Zustellung.
/// </summary>
/// <remarks>
/// <para>
/// Vorher gab es genau eine Schutzmaßnahme: zehn Sekunden Timeout am Client. Ein Empfänger, der
/// zuverlässig hineinläuft, band damit für jede Zustellung zehn Sekunden Worker-Zeit — und die
/// Warteschlange wiederholte es bis zu fünfmal, statt ihn eine Weile auszulassen.
/// </para>
/// <para>
/// Warum eine eigene Methode und nicht drei Zeilen in der Composition-Root: Die Werte unten sind
/// alle von den Vorgaben abgewichen, und jede Abweichung hat einen Grund, der ohne Test wieder
/// verloren geht. Der wichtigste steht bei <see cref="ConfigureCircuitBreaker"/>.
/// </para>
/// </remarks>
public static class WebhookDeliveryResilienceExtensions
{
    /// <summary>
    /// Hängt die Standard-Pipeline an den Zustellclient und passt sie an den Zustellbetrieb an.
    /// </summary>
    public static IHttpClientBuilder AddWebhookDeliveryResilience(this IHttpClientBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder
            .AddStandardResilienceHandler(options =>
            {
                // Der bisherige Wert, jetzt an der Stelle, an der er hingehört: Er begrenzt den
                // EINZELNEN Versuch. Ein toter Empfänger kostet damit weiterhin höchstens zehn
                // Sekunden je Versuch, nicht mehr das gesamte Zustellbudget.
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);

                // Drei Versuche insgesamt, nicht vier: Die Warteschlange wiederholt bereits bis zu
                // fünfmal (MaxAttempts im Dispatcher). Beides multipliziert sich, und aus der
                // Vorgabe würden zwanzig Zustellungen je Ereignis. Der Zweck hier ist der TCP-
                // Ruckler nach 200 ms, nicht der Ausfall über Minuten — den trägt die Warteschlange.
                options.Retry.MaxRetryAttempts = 2;
                options.Retry.Delay = TimeSpan.FromSeconds(1);
                options.Retry.UseJitter = true;

                ConfigureCircuitBreaker(options);

                // Muss über die Summe aus Versuchen und Wartezeiten passen (3 × 10 s plus Backoff),
                // sonst schneidet das Gesamt-Timeout den letzten Versuch ab.
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(45);
            })
            // Der eigentliche Grund, warum der Unterbrecher hier überhaupt nützt: Ohne diese Zeile
            // teilen sich ALLE Empfänger einen Unterbrecher, weil der Client einer ist. Ein einziger
            // toter Empfänger nähme dann allen anderen die Zustellung — schlimmer als gar kein
            // Unterbrecher. Die Dokumentation erwähnt die Auswahl nach Authority nur beim Hedging;
            // es gibt sie für die Standard-Pipeline genauso.
            .SelectPipelineByAuthority();

        return builder;
    }

    /// <summary>
    /// Der Unterbrecher, weg von Vorgaben, die für Dienst-zu-Dienst-Verkehr gedacht sind.
    /// </summary>
    /// <remarks>
    /// Die Vorgabe verlangt 100 Anfragen in 30 Sekunden, bevor sie überhaupt urteilt. Webhook-
    /// Zustellungen sind Hintergrundarbeit; an einen einzelnen Empfänger gehen so viele in dieser
    /// Zeit nie. Übernommen wäre der Unterbrecher eingebaut und würde nie auslösen — die Sorte
    /// Vorkehrung, die im Code steht, im Betrieb aber nicht existiert.
    /// </remarks>
    private static void ConfigureCircuitBreaker(HttpStandardResilienceOptions options)
    {
        options.CircuitBreaker.MinimumThroughput = 5;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(1);

        // Die Hälfte statt zehn Prozent: Ein Empfänger, der jede zweite Zustellung verliert, ist
        // kaputt. Bei zehn Prozent und fünf Zustellungen genügte ein einzelner Fehlschlag — ein
        // Wackler nähme dem Empfänger dann für die Unterbrechungsdauer alles.
        options.CircuitBreaker.FailureRatio = 0.5;

        // Fünf Sekunden wären hier folgenlos: Die Warteschlange kommt ohnehin erst Minuten später
        // wieder vorbei, der Unterbrecher wäre bis dahin längst zu.
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
    }
}

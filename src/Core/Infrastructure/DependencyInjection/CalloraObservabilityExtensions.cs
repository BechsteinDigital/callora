using Callora.Core.Application.Monitoring;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Callora.Core.Infrastructure.DependencyInjection;

/// <summary>
/// Die Observability-Kette des Hosts: Traces, Metriken und Logs in EINER OpenTelemetry-Pipeline.
/// </summary>
/// <remarks>
/// Als eigene Methode aus der Composition-Root herausgezogen, damit sie ohne den ganzen Host
/// aufrufbar ist. Vorher lag sie inline in <c>AddCalloraHost</c>, das eine Datenbank, Optionen und
/// ein Dutzend Subsysteme braucht — geprüft hat sie deshalb nichts. Eine Zeile, die niemand testen
/// kann, verschwindet irgendwann, ohne dass es auffällt: Ein fehlender Exporter meldet sich nicht,
/// er liefert einfach keine Daten mehr.
/// </remarks>
public static class CalloraObservabilityExtensions
{
    /// <summary>Registriert Tracing, Metriken und Logging samt OTLP-Export, wenn konfiguriert.</summary>
    public static IServiceCollection AddCalloraObservability(
        this IServiceCollection services,
        ObservabilityOptions observabilityOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(observabilityOptions);

            services.AddSingleton(observabilityOptions);
            var openTelemetry = services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(observabilityOptions.ServiceName))
                .WithTracing(tracing => tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation()
                    // Wildcard aus demselben Grund wie bei den Metern unten, und nicht nur aus
                    // Bequemlichkeit: Der Renderpfad liegt in Callora.Surface.Rendering, also in einem
                    // Modul ÜBER dem Core. Seinen ActivitySource hier namentlich zu nennen hieße, dass
                    // der Core ein höheres Modul kennt — die Abhängigkeit liefe verkehrt herum. Mit
                    // dem Wildcard wird jede Callora-Quelle erfasst, auch die aus Plugins.
                    .AddSource("Callora.*"))
                .WithMetrics(metrics => metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    // Domänenneutral: ein Wildcard erfasst alle Callora-Meter — die Core-
                    // Subsysteme (Callora.Core.PluginLifecycle/BackgroundJobs/Webhooks) ebenso
                    // wie Plugin-Meter (z. B. Callora.Voip.Calls) —, ohne dass der Core einen
                    // konkreten Plugin-Meter-Namen kennt.
                    .AddMeter("Callora.*"))
                // Logs gehören in DIESELBE Kette wie Traces und Metriken, sonst endet die Hälfte des
                // Aufbaus im Nichts: Ohne sie tragen Logzeilen zwar TraceId und SpanId, landen aber
                // nur im Konsolenanbieter — wer eine langsame Anfrage im Trace findet, kommt von dort
                // nicht zu ihren Logzeilen und umgekehrt. Die teure Hälfte (Instrumentierung von
                // ASP.NET, HttpClient, EF Core) stand längst; das hier ist die billige Verbindung.
                .WithLogging(
                    static _ => { },
                    static options =>
                    {
                        // Ohne Scopes geht genau das verloren, was die Zeilen einer Anfrage
                        // zusammenhält — ASP.NET legt Anfrage-Id und Endpunkt als Scope an, nicht als
                        // Teil der Nachricht.
                        options.IncludeScopes = true;

                        // Die formatierte Nachricht mitschicken: Ohne sie überträgt der Exporter nur
                        // Vorlage und Parameter. Für eine Suche im Backend ist das der Unterschied
                        // zwischen "finde den Text, den ich im Terminal gesehen habe" und "kenne die
                        // Vorlage auswendig".
                        options.IncludeFormattedMessage = true;
                    });

            if (!string.IsNullOrWhiteSpace(observabilityOptions.OtlpEndpoint))
            {
                openTelemetry.UseOtlpExporter(
                    OpenTelemetry.Exporter.OtlpExportProtocol.Grpc,
                    new Uri(observabilityOptions.OtlpEndpoint));
            }

        return services;
    }
}

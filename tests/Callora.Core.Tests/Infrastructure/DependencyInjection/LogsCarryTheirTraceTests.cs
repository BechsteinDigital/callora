using Callora.Core.Application.Monitoring;
using Callora.Core.Infrastructure.DependencyInjection;
using Callora.Core.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using System.Diagnostics;
using Xunit;

namespace Callora.Core.Tests.Infrastructure.DependencyInjection;

/// <summary>
/// Die Verbindung zwischen Trace und Logzeile.
/// </summary>
/// <remarks>
/// <para>
/// Traces und Metriken gingen an das OTLP-Ziel, Logs nicht — sie landeten im Konsolenanbieter.
/// Wer eine langsame Anfrage im Trace fand, kam von dort nicht zu ihren Logzeilen und umgekehrt.
/// Die teure Hälfte des Aufbaus (Instrumentierung von ASP.NET, HttpClient, EF Core) stand längst;
/// gefehlt hat die billige Verbindung dazwischen.
/// </para>
/// <para>
/// Geprüft wird an <see cref="CalloraObservabilityExtensions.AddCalloraObservability"/> und nicht
/// an einer im Test nachgebauten Kette — sonst prüfte der Test seine eigene Konfiguration und
/// nicht die des Hosts. Genau dafür ist die Methode aus der Composition-Root herausgezogen worden.
/// </para>
/// </remarks>
public sealed class LogsCarryTheirTraceTests
{
    [Fact]
    public void ALogWrittenInsideAnActivityCarriesItsTraceId()
    {
        var exported = new List<CapturedLogRecord>();
        using var provider = BuildProvider(exported);
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("test");

        using var source = new ActivitySource("Callora.Test.Logging");
        using var listener = ListenTo(source);
        using var activity = source.StartActivity("work");
        Assert.NotNull(activity);

        logger.LogInformation("etwas passiert");
        provider.GetRequiredService<LoggerProvider>().ForceFlush();

        var record = Assert.Single(exported);
        Assert.Equal(activity!.TraceId, record.TraceId);
        Assert.Equal(activity.SpanId, record.SpanId);
    }

    /// <summary>
    /// Ohne Scopes fehlt, was die Zeilen einer Anfrage zusammenhält: ASP.NET legt Anfrage-Id und
    /// Endpunkt als Scope an, nicht als Teil der Nachricht.
    /// </summary>
    [Fact]
    public void ScopesReachTheExporter()
    {
        var exported = new List<CapturedLogRecord>();
        using var provider = BuildProvider(exported);
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("test");

        using (logger.BeginScope(new Dictionary<string, object> { ["workspace.key"] = "acme" }))
        {
            logger.LogInformation("mit Bereich");
        }

        provider.GetRequiredService<LoggerProvider>().ForceFlush();

        var record = Assert.Single(exported);
        Assert.Contains(record.Scopes, pair => pair.Key == "workspace.key" && (string?)pair.Value == "acme");
    }

    /// <summary>
    /// Ohne die formatierte Nachricht überträgt der Exporter nur Vorlage und Parameter — die Suche
    /// im Backend fände dann nicht den Text, den jemand im Terminal gesehen hat.
    /// </summary>
    [Fact]
    public void TheFormattedMessageIsExported()
    {
        var exported = new List<CapturedLogRecord>();
        using var provider = BuildProvider(exported);
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("test");

        logger.LogInformation("Fläche {Surface} gerendert", "default");
        provider.GetRequiredService<LoggerProvider>().ForceFlush();

        var record = Assert.Single(exported);
        Assert.Equal("Fläche default gerendert", record.FormattedMessage);
    }

    /// <summary>
    /// Baut die Observability-Kette des Hosts und hängt nur einen Sammel-Exporter an, weil der
    /// echte OTLP-Export einen Endpunkt bräuchte.
    /// </summary>
    private static ServiceProvider BuildProvider(ICollection<CapturedLogRecord> exported)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCalloraObservability(new ObservabilityOptions { ServiceName = "callora-test" });
        services.ConfigureOpenTelemetryLoggerProvider(logging =>
            logging.AddProcessor(new SimpleLogRecordExportProcessor(new CapturingLogRecordExporter(exported))));
        return services.BuildServiceProvider();
    }

    private static ActivityListener ListenTo(ActivitySource source)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = candidate => candidate.Name == source.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}

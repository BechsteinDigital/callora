using Callora.Surface.Rendering.Api;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Sammelt die Messungen des Renderpfad-Meters für die Dauer eines Tests.
/// <para>
/// Der Meter ist statisch und prozessweit: Läuft ein anderer Test parallel, emittiert er in
/// denselben Listener. Deshalb wird thread-sicher gesammelt und beim Abfragen gefiltert — dieselbe
/// Vorsichtsmaßnahme, die PluginLifecycleTelemetryTests seit PLAT-222 trifft.
/// </para>
/// </summary>
public sealed class RenderMetricRecorder : IDisposable
{
    private readonly ConcurrentBag<RenderMeasurement> _measurements = [];
    private readonly MeterListener _listener;

    public RenderMetricRecorder()
    {
        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == SurfaceRenderTelemetry.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            }
        };

        _listener.SetMeasurementEventCallback<long>(
            (instrument, value, tags, _) => Record(instrument.Name, value, tags));
        _listener.SetMeasurementEventCallback<double>(
            (instrument, value, tags, _) => Record(instrument.Name, value, tags));
        _listener.Start();
    }

    /// <summary>Alle Messungen eines Instruments, ungefiltert.</summary>
    public IReadOnlyList<RenderMeasurement> All(string instrumentName) =>
        [.. _measurements.Where(measurement => measurement.Instrument == instrumentName)];

    /// <summary>
    /// Die eine Messung, die zum Filter passt. Wirft, wenn es keine oder mehrere gibt — bei
    /// parallelen Tests ist „mehrere" ein echter Befund und keine Nebensache.
    /// </summary>
    public RenderMeasurement Only(string instrumentName, string? workspace = null, string? reason = null)
    {
        var matches = All(instrumentName)
            .Where(measurement => workspace is null || measurement.Workspace == workspace)
            .Where(measurement => reason is null || measurement.Reason == reason)
            .ToArray();

        return matches.Length == 1
            ? matches[0]
            : throw new InvalidOperationException(
                $"Erwartet wurde genau eine Messung von '{instrumentName}' " +
                $"(workspace: {workspace ?? "beliebig"}, reason: {reason ?? "beliebig"}), gefunden: {matches.Length}.");
    }

    public void Dispose() => _listener.Dispose();

    private void Record(string instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        // Kopiert statt durchgereicht: Ein ref-artiger Span darf weder in eine lokale Funktion
        // noch in ein Feld — und die Messung soll den Callback überleben.
        var byName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            byName[tag.Key] = tag.Value?.ToString() ?? string.Empty;
        }

        _measurements.Add(new RenderMeasurement(
            instrument,
            value,
            byName.GetValueOrDefault("workspace.key", string.Empty),
            byName.GetValueOrDefault("surface.key", string.Empty),
            byName.GetValueOrDefault("surface.render.outcome", string.Empty),
            byName.GetValueOrDefault("surface.render.reason", string.Empty)));
    }
}

using OpenTelemetry;
using OpenTelemetry.Logs;
using System.Diagnostics;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Nimmt Log-Einträge aus einer OpenTelemetry-Pipeline entgegen und hält fest, was an ihnen zählt.
/// </summary>
/// <remarks>
/// <para>
/// Selbst geschrieben statt <c>OpenTelemetry.Exporter.InMemory</c> zu referenzieren — ein Paket
/// weniger für drei Zeilen Arbeit.
/// </para>
/// <para>
/// Wichtiger: Es werden <b>Werte</b> herausgezogen, nicht der <see cref="LogRecord"/> aufgehoben.
/// Die SDK poolt diese Objekte und gibt sie nach dem Export wieder frei; ein gespeicherter Record
/// zeigt später auf den Inhalt eines fremden Log-Eintrags. Das ist die Sorte Fehler, die im Test
/// grün aussieht und beim zweiten Eintrag falsch wird.
/// </para>
/// </remarks>
public sealed class CapturingLogRecordExporter(ICollection<CapturedLogRecord> captured) : BaseExporter<LogRecord>
{
    public override ExportResult Export(in Batch<LogRecord> batch)
    {
        foreach (var record in batch)
        {
            var scopeValues = new List<KeyValuePair<string, object?>>();
            record.ForEachScope(
                static (scope, state) =>
                {
                    foreach (var pair in scope)
                    {
                        state.Add(pair);
                    }
                },
                scopeValues);

            captured.Add(new CapturedLogRecord(
                record.FormattedMessage,
                record.TraceId,
                record.SpanId,
                scopeValues));
        }

        return ExportResult.Success;
    }
}

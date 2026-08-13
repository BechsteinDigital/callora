namespace Callora.Core.Tests.Support;

/// <summary>Eine einzelne Messung des Renderpfad-Meters, mit den Tags, auf die Alarme sich stützen.</summary>
public sealed record RenderMeasurement(
    string Instrument,
    double Value,
    string Workspace,
    string Surface,
    string Outcome,
    string Reason);

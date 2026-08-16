namespace Callora.Core.Application.Monitoring;

/// <summary>
/// Was ein Browser meldet, wenn in ihm etwas gescheitert ist (#294).
/// </summary>
/// <param name="Source">Woher die Meldung kommt — <c>admin</c> oder <c>surface</c>.</param>
/// <param name="Message">Die Fehlermeldung, wie der Browser sie kennt.</param>
/// <param name="Stack">Der Stacktrace, sofern es einen gibt.</param>
/// <param name="Url">Die Seite, auf der es passierte.</param>
/// <remarks>
/// Eine feste Feldliste, kein freies Objekt: Was hier hereinkommt, kommt auf der öffentlichen
/// Fläche von jedem, und was der Absender bestimmen kann, bestimmt er dann auch über das
/// Betriebslog. Vier benannte Felder sind die Grenze, die man später nicht mehr ziehen kann.
/// </remarks>
public sealed record ClientErrorReport(
    string Source,
    string Message,
    string? Stack,
    string? Url);

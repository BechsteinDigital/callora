using Callora.Core.Application.Monitoring;
using Callora.Core.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Callora.Core.Api;

/// <summary>
/// Die Senke für Fehler aus dem Browser (#294).
/// </summary>
/// <remarks>
/// Seit #270 gehen die Server-Logs über OpenTelemetry ans OTLP-Ziel und tragen ihre Trace-Id.
/// Genau dort fehlten die Browser-Fehler: Eine kaputte Oberfläche meldete sich erst, wenn ein
/// Kunde anrief.
///
/// <para>
/// Ein Handler ohne Senke wäre Dekoration gewesen, eine offene Senke ein Einfallstor — die Fläche
/// ist öffentlich, denn ein Besucher einer Kundenseite hat keine Sitzung. Deshalb steht vor dem
/// Log dreierlei: eine eigene, enge Rate-Begrenzung je Client, eine harte Größengrenze auf dem
/// Rumpf, und die Entschärfung des Inhalts in <see cref="ClientErrorSanitizer"/>. Ausgewertet wird
/// nichts — die Meldung ist die Aussage des Browsers, nicht unsere.
/// </para>
///
/// <para>
/// Die Antwort ist leer und immer dieselbe. Wer meldet, erfährt nichts über das System, und schon
/// gar nicht seine eigene Eingabe zurück.
/// </para>
/// </remarks>
[ApiController]
[AllowAnonymous]
[Route("api/client-errors")]
[Tags("Monitoring")]
public sealed class ClientErrorsController : ControllerBase
{
    /// <summary>Größe, ab der ein Rumpf abgewiesen wird, statt gelesen zu werden.</summary>
    public const int MaxRequestBodyBytes = 8 * 1024;

    /// <summary>
    /// Logkategorie der Browser-Meldungen. Eigen, damit ein Betreiber sie zusammen sieht und
    /// getrennt vom Rest steuern kann — sie kommen von außen und verhalten sich entsprechend.
    /// </summary>
    public const string LogCategory = "Callora.ClientErrors";

    [HttpPost]
    [EnableRateLimiting(BackendRateLimiting.ClientErrorPolicy)]
    [RequestSizeLimit(MaxRequestBodyBytes)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public IActionResult Report(
        [FromBody] ClientErrorReport report,
        [FromServices] ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);

        var sanitized = ClientErrorSanitizer.Sanitize(report);
        loggerFactory.CreateLogger(LogCategory).LogWarning(
            "Browser error in {ClientSource} at {ClientUrl}: {ClientMessage} {ClientStack}",
            sanitized.Source,
            sanitized.Url ?? "(unbekannt)",
            sanitized.Message,
            sanitized.Stack ?? string.Empty);

        // Angenommen, nicht verarbeitet: Was daraus wird, entscheidet der Betrieb, und der
        // Absender wartet nicht darauf.
        return Accepted();
    }
}

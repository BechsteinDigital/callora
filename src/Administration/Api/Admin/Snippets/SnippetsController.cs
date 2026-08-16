using Callora.Core.Application.Configuration;
using Callora.Core.Application.Security;
using Callora.Core.Application.Snippets;
using Callora.Core.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Callora.Administration.Api.Admin.Snippets;

/// <summary>
/// Oberflächentexte ansehen und überschreiben (ADR-024, #273).
/// </summary>
/// <remarks>
/// Der Grund, warum es diese Fläche gibt: Wer „Warenkorb" in „Bestellung" ändern will, darf dafür
/// kein Paket neu bauen müssen. Das unterscheidet ein Snippet-System von einer Lösung zur Bauzeit.
///
/// <para>
/// Gearbeitet wird immer auf EINER Ebene, die der Aufrufer nennt. Wer im Workspace steht, sieht
/// und ändert, was dort gesetzt ist — nicht, was von Mandant oder global durchschlägt. Die
/// aufgelöste Kette ist die Sicht des Renderpfads; hier wäre sie die Ansicht, in der niemand mehr
/// sagen kann, was das Zurücknehmen einer Zeile bewirkt.
/// </para>
/// </remarks>
[ApiController]
[Authorize]
[Route("api/snippets")]
[Produces("application/json")]
[Tags("Snippets")]
public sealed class SnippetsController : ControllerBase
{
    [HttpGet]
    [CalloraPermission(BackendPermissionKeys.SnippetRead)]
    [ProducesResponseType<SnippetApiResponse[]>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string locale,
        [FromServices] SnippetAdminService service,
        CancellationToken cancellationToken,
        [FromQuery] string scope = SystemConfigScopes.Global,
        [FromQuery] string scopeKey = "")
    {
        ArgumentNullException.ThrowIfNull(service);

        if (string.IsNullOrWhiteSpace(locale))
        {
            return Problem(
                detail: "Ohne Locale gibt es nichts zu zeigen: Ein Schlüssel trägt seinen Text je Sprache.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var entries = await service
                .ListAsync(locale, scope, scopeKey, cancellationToken)
                .ConfigureAwait(false);
            return Ok(entries.Select(ToResponse).ToArray());
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPut("{snippetKey}")]
    [CalloraPermission(BackendPermissionKeys.SnippetUpdate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Set(
        string snippetKey,
        [FromQuery] string locale,
        [FromBody] SetSnippetApiRequest request,
        [FromServices] SnippetAdminService service,
        CancellationToken cancellationToken,
        [FromQuery] string scope = SystemConfigScopes.Global,
        [FromQuery] string scopeKey = "")
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(service);

        if (string.IsNullOrWhiteSpace(locale))
        {
            return Problem(
                detail: "Ohne Locale ist nicht entschieden, welche Sprache dieser Text betrifft.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            await service
                .SetAsync(snippetKey, locale, scope, scopeKey, request.Value ?? string.Empty, ResolveActor(), cancellationToken)
                .ConfigureAwait(false);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>Nimmt die Abweichung zurück — der Text des Pakets gilt danach wieder.</summary>
    [HttpDelete("{snippetKey}")]
    [CalloraPermission(BackendPermissionKeys.SnippetUpdate)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Reset(
        string snippetKey,
        [FromQuery] string locale,
        [FromServices] SnippetAdminService service,
        CancellationToken cancellationToken,
        [FromQuery] string scope = SystemConfigScopes.Global,
        [FromQuery] string scopeKey = "")
    {
        ArgumentNullException.ThrowIfNull(service);

        if (string.IsNullOrWhiteSpace(locale))
        {
            return Problem(
                detail: "Ohne Locale ist nicht entschieden, welche Sprache zurückgesetzt wird.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            await service.ResetAsync(snippetKey, locale, scope, scopeKey, cancellationToken).ConfigureAwait(false);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private string ResolveActor() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.Identity?.Name
        ?? "unknown";

    private static SnippetApiResponse ToResponse(SnippetAdminEntry entry) => new(
        entry.SnippetKey,
        entry.Locale,
        entry.PluginId,
        entry.BaseValue,
        entry.OverrideValue,
        entry.EffectiveValue,
        entry.IsOverridden,
        entry.IsOrphaned);
}

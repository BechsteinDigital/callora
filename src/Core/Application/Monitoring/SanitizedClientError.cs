namespace Callora.Core.Application.Monitoring;

/// <summary>
/// Eine Browser-Meldung, wie sie ins Betriebslog darf — siehe <see cref="ClientErrorSanitizer"/>.
/// </summary>
public sealed record SanitizedClientError(
    string Source,
    string Message,
    string? Stack,
    string? Url);

namespace Callora.Plugins.Dialer.Application.Numbers;

/// <summary>
/// One number in a workspace dial list.
/// </summary>
public sealed record DialNumberEntry(
    string NumberId,
    string Number,
    string? DisplayName,
    DateTimeOffset AddedAtUtc);

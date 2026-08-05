namespace Callora.Administration.Api;

/// <param name="IsDisabled">
/// Whether the account is deactivated: it keeps its data and memberships but
/// authenticates nowhere and has its live sessions rejected (#104).
/// </param>
/// <param name="IsLockedOut">
/// Whether repeated failed sign-ins currently block authentication. Clears itself
/// when the lockout window elapses.
/// </param>
public sealed record BackendUserApiResponse(
    string ExternalId,
    string? Email,
    string? DisplayName,
    bool HasPassword,
    string? PasswordHashAlgorithm,
    bool IsDisabled,
    bool IsLockedOut,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

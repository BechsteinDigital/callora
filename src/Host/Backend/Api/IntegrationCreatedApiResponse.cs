namespace Callora.Host.Backend.Api;

/// <summary>
/// Returned once on creation — the only time the plaintext key is exposed (PLAT-264).
/// </summary>
public sealed record IntegrationCreatedApiResponse(
    Guid Id,
    string Name,
    string ApiKey,
    string KeyPrefix,
    string Role,
    string Scope,
    string? WorkspaceKey);

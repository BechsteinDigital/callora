namespace Callora.Core.Api;

/// <summary>Listing projection for integrations; never carries the secret key (PLAT-264).</summary>
public sealed record IntegrationApiResponse(
    Guid Id,
    string Name,
    string KeyPrefix,
    string Role,
    string Scope,
    string? WorkspaceKey,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? RevokedAtUtc);

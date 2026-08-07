namespace Callora.Administration.Api;

public sealed record WorkspaceApiResponse(
    string TenantKey,
    string WorkspaceKey,
    string DisplayName,
    string WorkspaceType,
    bool IsActive,
    bool TenantIsActive,
    string? PublicHost,
    string? ThemePluginId,
    string? ThemeVersion,
    string? ThemeAssignedBy,
    DateTimeOffset? ThemeAssignedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

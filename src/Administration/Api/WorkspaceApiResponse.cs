namespace Callora.Administration.Api;

public sealed record WorkspaceApiResponse(
    string TenantKey,
    string WorkspaceKey,
    string DisplayName,
    string WorkspaceType,
    bool IsActive,
    bool TenantIsActive,
    string? PublicBaseUrl,
    string? PublicHost,
    string PublicPathPrefix,
    string? ThemePluginId,
    string? ThemeVersion,
    string? ThemeAssignedBy,
    DateTimeOffset? ThemeAssignedAtUtc,
    string SurfaceAccessPolicy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

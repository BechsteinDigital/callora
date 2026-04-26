namespace Callora.Host.Backend.Application.Abstractions.Workspaces;

public sealed record WorkspaceSnapshot(
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
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

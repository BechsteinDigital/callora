using Callora.Core.Domain.Workspaces;

namespace Callora.Core.Application.Workspaces;

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
    DateTimeOffset UpdatedAtUtc)
{
    /// <summary>Who may reach this workspace's public surface. Defaults to Public.</summary>
    public SurfaceAccessPolicy SurfaceAccessPolicy { get; init; } = SurfaceAccessPolicy.Public;
}

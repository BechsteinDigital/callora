namespace Callora.Core.Application.Workspaces;

/// <summary>
/// The workspace as a data container. Public routing and the access mode live on
/// its surfaces — see <see cref="WorkspaceSurfaceSnapshot"/>.
/// </summary>
/// <param name="ThemePluginId">
/// Default theme for the workspace's surfaces; a surface may override it.
/// </param>
public sealed record WorkspaceSnapshot(
    string TenantKey,
    string WorkspaceKey,
    string DisplayName,
    string WorkspaceType,
    bool IsActive,
    bool TenantIsActive,
    string? ThemePluginId,
    string? ThemeVersion,
    string? ThemeAssignedBy,
    DateTimeOffset? ThemeAssignedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

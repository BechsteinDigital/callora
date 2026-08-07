namespace Callora.Core.Application.Workspaces;

/// <summary>
/// The workspace as a data container. Der Zugriffsmodus und der Pfad liegen auf seinen
/// Oberflächen — siehe <see cref="WorkspaceSurfaceSnapshot"/>. Eine Basis-URL kann dagegen
/// den Workspace SELBST bezeichnen: <see cref="PublicHost"/>.
/// </summary>
/// <param name="PublicHost">
/// Der Host, unter dem dieser Workspace erreichbar ist, oder <c>null</c>. Ohne ihn beginnt
/// der Pfad jeder Oberfläche mit dem Workspace-Schlüssel.
/// </param>
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
    string? PublicHost,
    string? ThemePluginId,
    string? ThemeVersion,
    string? ThemeAssignedBy,
    DateTimeOffset? ThemeAssignedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

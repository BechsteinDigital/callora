namespace Callora.Administration.Api;

/// <summary>
/// Who vouches for a surface's visitors (ADR-017 §5.2).
/// </summary>
/// <param name="WorkspaceKey">Workspace owning the surface.</param>
/// <param name="SurfaceKey">Surface the assignment belongs to.</param>
/// <param name="IdentityPluginId">Assigned plugin, or null when the surface has no provider.</param>
/// <param name="IdentityVersion">Version of that plugin at assignment time.</param>
/// <param name="AssignedBy">Operator who assigned it.</param>
/// <param name="AssignedAtUtc">When it was assigned; older sessions stop counting from here.</param>
/// <param name="IsAvailable">
/// Whether the assigned plugin is effectively available in the workspace. False means
/// the surface is closed for authenticated access — surfaced here rather than hidden.
/// </param>
public sealed record SurfaceIdentityAssignmentApiResponse(
    string WorkspaceKey,
    string SurfaceKey,
    string? IdentityPluginId,
    string? IdentityVersion,
    string? AssignedBy,
    DateTimeOffset? AssignedAtUtc,
    bool IsAvailable);

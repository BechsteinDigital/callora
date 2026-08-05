namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Who vouches for a surface's visitors, and since when (ADR-017 §5.2). The audit
/// fields are part of the answer, not decoration: for a theme "who assigned it" is
/// convenience, here it is the provenance of every identity the surface accepts.
/// </summary>
/// <param name="WorkspaceKey">Workspace owning the surface.</param>
/// <param name="SurfaceKey">Surface the assignment belongs to.</param>
/// <param name="PluginId">Assigned plugin, or null when the surface has no provider.</param>
/// <param name="Version">Version of that plugin at assignment time.</param>
/// <param name="AssignedBy">Operator who assigned it.</param>
/// <param name="AssignedAtUtc">When it was assigned; also the instant older sessions stop counting.</param>
/// <param name="IsAvailable">
/// Whether the assigned plugin is effectively available in the workspace right now.
/// False means the surface is closed for authenticated access — visible in the admin
/// rather than silently degraded (ADR-017 §6.2).
/// </param>
public sealed record SurfaceIdentityAssignment(
    string WorkspaceKey,
    string SurfaceKey,
    string? PluginId,
    string? Version,
    string? AssignedBy,
    DateTimeOffset? AssignedAtUtc,
    bool IsAvailable);

using Callora.Core.Domain.Workspaces;

namespace Callora.Core.Application.Workspaces;

/// <summary>Read model of one workspace surface (ADR-014 §5).</summary>
public sealed record WorkspaceSurfaceSnapshot(
    Guid Id,
    string WorkspaceKey,
    string SurfaceKey,
    string DisplayName,
    string SurfaceType,
    string? PublicBaseUrl,
    string? PublicHost,
    string PublicPathPrefix,
    SurfaceAccessMode AccessMode,
    string? Locale,
    string? TemplatePluginId,
    string? TemplateVersion,
    string? ThemePluginId,
    string? ThemeVersion,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    /// <summary>
    /// Tenant that owns the surface's workspace. Additive to the read model so the
    /// public render path can build a per-surface context; defaults to empty for
    /// callers that do not project it.
    /// </summary>
    public string TenantKey { get; init; } = string.Empty;
}

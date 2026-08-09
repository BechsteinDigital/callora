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
    SurfaceAuthentication Authentication,
    SurfaceRouting Routing,
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
    /// Der Elternknoten, oder null für eine Anwendungswurzel (ADR-019). Die Verwaltung braucht
    /// ihn, um einen geerbten Wert von einem eigenen zu unterscheiden.
    /// </summary>
    public string? ParentSurfaceKey { get; init; }

    /// <summary>Reihenfolge unter Geschwistern.</summary>
    public int Position { get; init; }

    /// <summary>
    /// Claims, die ein Besucher mitbringen muss (ADR-019 §4) — kommagetrennt, leer heißt keine
    /// Anforderung. Kumulativ entlang der Kette.
    /// </summary>
    public string? RequiredClaims { get; init; }

    /// <summary>Claims, die jeder Besucher dieser Fläche mitbringt.</summary>
    public string? GrantedClaims { get; init; }

    /// <summary>
    /// Tenant that owns the surface's workspace. Additive to the read model so the
    /// public render path can build a per-surface context; defaults to empty for
    /// callers that do not project it.
    /// </summary>
    public string TenantKey { get; init; } = string.Empty;

    /// <summary>
    /// Plugin assigned as this surface's identity provider (ADR-017 §5.2), or null
    /// when none is assigned. Deliberately outside <see cref="WorkspaceSurfaceInput"/>:
    /// editing a surface must not silently drop who vouches for its visitors.
    /// </summary>
    public string? IdentityPluginId { get; init; }

    /// <summary>Version of the assigned identity plugin at assignment time.</summary>
    public string? IdentityVersion { get; init; }

    /// <summary>Who assigned the identity provider — audit material, not convenience.</summary>
    public string? IdentityAssignedBy { get; init; }

    /// <summary>
    /// When the identity provider was assigned; sessions issued before this instant
    /// predate the current provider and are no longer trusted (ADR-017 §6.3).
    /// </summary>
    public DateTimeOffset? IdentityAssignedAtUtc { get; init; }
}

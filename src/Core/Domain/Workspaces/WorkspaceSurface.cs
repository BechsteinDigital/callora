namespace Callora.Core.Domain.Workspaces;

/// <summary>
/// A concrete access/output surface within a workspace (ADR-014 §5) — the Callora
/// counterpart of a Shopware SalesChannel. A workspace has N surfaces on shared data,
/// each with its own domain/route, access mode, template and theme assignment.
/// </summary>
public sealed class WorkspaceSurface
{
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }

    /// <summary>Technical key, unique per workspace.</summary>
    public string SurfaceKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Extensible surface-type key (ADR-014 §16), not a closed enum. Default "spa".</summary>
    public string SurfaceType { get; set; } = "spa";

    public string? PublicBaseUrl { get; set; }

    public string? PublicHost { get; set; }

    public string PublicPathPrefix { get; set; } = "/";

    public SurfaceAccessMode AccessMode { get; set; } = SurfaceAccessMode.Mixed;

    public string? Locale { get; set; }

    public string? TemplatePluginId { get; set; }

    public string? TemplateVersion { get; set; }

    public string? ThemePluginId { get; set; }

    public string? ThemeVersion { get; set; }

    public string? ThemeAssignedBy { get; set; }

    public DateTimeOffset? ThemeAssignedAtUtc { get; set; }

    /// <summary>
    /// Plugin an operator assigned as this surface's identity provider (ADR-017 §5.2),
    /// or null when the surface has none. Assignment is operator data, not plugin
    /// self-declaration: a shipped login plugin cannot know a surface key the customer
    /// creates later.
    /// </summary>
    public string? IdentityPluginId { get; set; }

    /// <summary>Version of the assigned identity plugin at assignment time.</summary>
    public string? IdentityVersion { get; set; }

    /// <summary>
    /// Who assigned the identity provider. Unlike the theme equivalent this is not a
    /// convenience: who vouches for a surface's visitors, and since when, is audit
    /// material.
    /// </summary>
    public string? IdentityAssignedBy { get; set; }

    /// <summary>
    /// When the identity provider was assigned. Doubles as the invalidation boundary:
    /// a surface session issued before this instant predates the current provider and
    /// is no longer trusted (ADR-017 §6.3).
    /// </summary>
    public DateTimeOffset? IdentityAssignedAtUtc { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Workspace Workspace { get; set; } = null!;
}

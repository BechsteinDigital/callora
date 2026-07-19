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

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Workspace Workspace { get; set; } = null!;
}

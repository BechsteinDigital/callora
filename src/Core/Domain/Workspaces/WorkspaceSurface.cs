namespace Callora.Core.Domain.Workspaces;

/// <summary>
/// A node in a workspace's surface tree (ADR-019).
/// <para>
/// A node without a parent is an <b>application root</b> — what ADR-014 §5 called a surface,
/// the Callora counterpart of a Shopware SalesChannel: website, dialer, agent desktop. It
/// carries the access itself: host or path prefix, access mode, theme, identity provider.
/// </para>
/// <para>
/// A node with a parent is a <b>child</b> — what Shopware calls a category. It inherits the
/// access and overrides only what it needs of its own. Every node may carry a layout, which is
/// the point of the whole thing: there used to be exactly one layout per surface, so a website
/// with three pages would have needed three access surfaces.
/// </para>
/// </summary>
public sealed class WorkspaceSurface
{
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }

    /// <summary>
    /// The parent node, or null for an application root.
    /// <para>
    /// Always within the same workspace, and never a cycle — both are checked when the value is
    /// set, not when it is resolved. A cycle that surfaces at render time is an endless loop in
    /// the request path.
    /// </para>
    /// </summary>
    public Guid? ParentSurfaceId { get; set; }

    /// <summary>Ascending order among siblings — the order the navigation shows.</summary>
    public int Position { get; set; }

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

    /// <summary>
    /// Der Elternknoten als Navigation. Nur zum Lesen des Schlüssels da: Die Verwaltung zeigt
    /// den Elternteil als Schlüssel an, gespeichert ist eine Id — ohne diese Beziehung müsste
    /// jede Projektion eine zweite Abfrage machen oder den Schlüssel weglassen, und weglassen
    /// hieße, dass die API einen Baum ausliefert, in dem niemand den Elternteil sieht.
    /// </summary>
    public WorkspaceSurface? Parent { get; set; }
}

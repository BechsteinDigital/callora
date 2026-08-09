namespace Callora.Core.Domain.Workspaces;

/// <summary>
/// A workspace is the data container: which plugins are active, whose data lives
/// here, who may work in it. It has no address of its own — every way *into* the
/// data is a <see cref="WorkspaceSurface"/> (ADR-014 §5).
/// </summary>
public sealed class Workspace
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string WorkspaceKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string WorkspaceType { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    /// <summary>
    /// Der Host, unter dem dieser Workspace erreichbar ist — oder <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Eine Basis-URL kann einen Workspace bezeichnen (<c>kunde.de</c>) oder eine Fläche
    /// (<c>portal.kunde.de</c>). Eine SEITE kann nie eine Basis-URL sein; sie ist immer ein
    /// Pfadsegment unterhalb ihrer Fläche.
    ///
    /// <para>
    /// Trägt weder die Fläche noch der Workspace einen Host, beginnt der Pfad mit dem
    /// Workspace-Schlüssel: <c>host.de/&lt;workspace&gt;/&lt;fläche&gt;/&lt;seite&gt;</c>.
    /// Ohne dieses Segment beanspruchte jeder frisch angelegte Workspace die gesamte Origin —
    /// zwei davon waren nicht unterscheidbar, und die Auswertung entschied still und immer
    /// gleich, welcher gewinnt. Der andere war unerreichbar, ohne Hinweis irgendwo.
    /// </para>
    /// </remarks>
    public string? PublicHost { get; set; }

    /// <summary>
    /// Default theme for the workspace's surfaces. A surface without a theme of
    /// its own renders with this one; assigning a theme to a surface overrides it
    /// for that surface only. This is a default, not an appearance of the
    /// workspace itself — the workspace is never rendered.
    /// </summary>
    public string? ThemePluginId { get; set; }

    public string? ThemeVersion { get; set; }

    public string? ThemeAssignedBy { get; set; }

    public DateTimeOffset? ThemeAssignedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Domain.Tenants.Tenant Tenant { get; set; } = null!;

    public ICollection<WorkspaceMembership> Memberships { get; set; } = new List<WorkspaceMembership>();

    public ICollection<WorkspaceSurface> Surfaces { get; set; } = new List<WorkspaceSurface>();
}

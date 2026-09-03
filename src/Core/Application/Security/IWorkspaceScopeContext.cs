using Callora.Core.Extensibility;

namespace Callora.Core.Application.Security;

/// <summary>
/// The workspace a request is scoped to, consumed by the persistence-level
/// global query filter (PLAT-267). Operators and non-request contexts (jobs,
/// seeding, migrations) are not workspace-scoped, so the filter is bypassed
/// for them.
/// </summary>
[CalloraInternal("Workspace-scope resolution — enforcement, not a plugin contract (REV2 §7.2)")]
public interface IWorkspaceScopeContext
{
    /// <summary>True only for a workspace-bound caller — never for an operator.</summary>
    bool IsWorkspaceScoped { get; }

    /// <summary>The bound workspace key when <see cref="IsWorkspaceScoped"/> is true; otherwise null.</summary>
    string? WorkspaceKey { get; }

    /// <summary>
    /// True only for a tenant-bound caller — never for an operator, never for a workspace session.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ein Mandanten-Aufrufer ist an <em>keinen</em> Workspace gebunden, also wäre er ohne diese
    /// zweite Dimension schlicht „nicht workspace-scoped" — und das heißt beim Filter: Bypass, so wie
    /// bei einem Operator. Für einen Kunden, der die Instanz nicht besitzt, wäre das genau der
    /// Durchgriff auf fremde Mandanten, den PLAT-267 für Workspaces geschlossen hat.
    /// </para>
    /// <para>
    /// Default-Implementierung, damit bestehende Implementierer (Tests, Adapter) unverändert
    /// bleiben: Wer nichts von Mandanten weiß, ist keiner — fail-closed in die alte Richtung.
    /// </para>
    /// </remarks>
    bool IsTenantScoped => false;

    /// <summary>The bound tenant key when <see cref="IsTenantScoped"/> is true; otherwise null.</summary>
    string? TenantKey => null;
}

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
}

namespace Callora.Host.Backend.Application.Abstractions.Security;

/// <summary>
/// The workspace a request is scoped to, consumed by the persistence-level
/// global query filter (PLAT-267). Operators and non-request contexts (jobs,
/// seeding, migrations) are not workspace-scoped, so the filter is bypassed
/// for them.
/// </summary>
public interface IWorkspaceScopeContext
{
    /// <summary>True only for a workspace-bound caller — never for an operator.</summary>
    bool IsWorkspaceScoped { get; }

    /// <summary>The bound workspace key when <see cref="IsWorkspaceScoped"/> is true; otherwise null.</summary>
    string? WorkspaceKey { get; }
}

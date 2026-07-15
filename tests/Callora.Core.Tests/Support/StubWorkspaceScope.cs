using Callora.Core.Application.Security;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Test double for the request workspace scope. A non-null key models a
/// workspace-scoped caller; null models an unscoped operator/system context.
/// </summary>
public sealed class StubWorkspaceScope(string? workspaceKey) : IWorkspaceScopeContext
{
    public bool IsWorkspaceScoped => WorkspaceKey is not null;

    public string? WorkspaceKey { get; } = workspaceKey;
}

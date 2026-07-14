using Callora.Host.Backend.Application.Security;

namespace Callora.Host.Backend.Tests.Support;

/// <summary>
/// Test double for the request workspace scope. A non-null key models a
/// workspace-scoped caller; null models an unscoped operator/system context.
/// </summary>
public sealed class StubWorkspaceScope(string? workspaceKey) : IWorkspaceScopeContext
{
    public bool IsWorkspaceScoped => WorkspaceKey is not null;

    public string? WorkspaceKey { get; } = workspaceKey;
}

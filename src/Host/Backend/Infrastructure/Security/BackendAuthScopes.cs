namespace Callora.Host.Backend.Infrastructure.Security;

/// <summary>
/// Values of the <see cref="BackendClaimTypes.CalloraScope"/> claim.
/// </summary>
public static class BackendAuthScopes
{
    /// <summary>
    /// Platform-operator session issued by the operator login or the
    /// bootstrap API key; grants access across all workspaces.
    /// </summary>
    public const string Platform = "platform";

    /// <summary>
    /// Workspace session issued by the workspace login; locked to the
    /// workspace named in <see cref="BackendClaimTypes.WorkspaceKey"/>.
    /// </summary>
    public const string Workspace = "workspace";
}

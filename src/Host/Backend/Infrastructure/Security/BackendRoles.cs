namespace Callora.Host.Backend.Infrastructure.Security;

/// <summary>
/// Role names recognized by backend authorization policies.
/// </summary>
public static class BackendRoles
{
    /// <summary>
    /// Administrative role with full backend access.
    /// </summary>
    public const string Admin = "admin";

    /// <summary>
    /// Role used by API-key based host access.
    /// </summary>
    public const string HostApi = "host.api";
}

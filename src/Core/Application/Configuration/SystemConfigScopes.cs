namespace Callora.Core.Application.Configuration;

/// <summary>
/// Scope names of the system configuration, ordered from most to least specific.
/// </summary>
public static class SystemConfigScopes
{
    public const string Global = "global";
    public const string Tenant = "tenant";
    public const string Workspace = "workspace";

    public static bool IsValid(string? scope) =>
        scope is Global or Tenant or Workspace;
}

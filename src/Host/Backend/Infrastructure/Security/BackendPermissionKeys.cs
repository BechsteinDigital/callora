namespace Callora.Host.Backend.Infrastructure.Security;

/// <summary>
/// Permission keys for backend API authorization.
/// </summary>
public static class BackendPermissionKeys
{
    public const string TenantCreate = "tenant.create";
    public const string TenantRead = "tenant.read";
    public const string TenantUpdate = "tenant.update";
    public const string TenantDelete = "tenant.delete";
    public const string PluginCreate = "plugin.create";
    public const string PluginRead = "plugin.read";
    public const string PluginDelete = "plugin.delete";
    public const string PluginExecute = "plugin.execute";
    public const string CallRead = "call.read";
    public const string CallExecute = "call.execute";
    public const string ExtensionRead = "extension.read";
    public const string ExtensionUpdate = "extension.update";
    public const string RoleRead = "role.read";
    public const string RoleUpdate = "role.update";
    public const string UserCreate = "user.create";
    public const string UserRead = "user.read";
    public const string UserUpdate = "user.update";
    public const string UserDelete = "user.delete";
    public const string WorkspaceCreate = "workspace.create";
    public const string WorkspaceRead = "workspace.read";
    public const string WorkspaceUpdate = "workspace.update";
    public const string WorkspaceDelete = "workspace.delete";
}

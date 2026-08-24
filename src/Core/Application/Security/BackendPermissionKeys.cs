namespace Callora.Core.Application.Security;

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
    public const string ConfigRead = "config.read";
    public const string ConfigUpdate = "config.update";
    public const string WebhookRead = "webhook.read";
    public const string WebhookManage = "webhook.manage";
    public const string NotificationRead = "notification.read";
    public const string MediaRead = "media.read";
    public const string MediaManage = "media.manage";
    public const string CustomFieldRead = "customfield.read";
    public const string CustomFieldUpdate = "customfield.update";
    public const string FlowRead = "flow.read";
    public const string FlowManage = "flow.manage";
    public const string JobRead = "job.read";

    /// <summary>
    /// Enables the plugin execution recorder and reads what it captured. Separate from
    /// <see cref="JobRead"/> and the rest: recordings contain SQL command text, which is a
    /// wider disclosure than any other monitoring endpoint makes.
    /// </summary>
    public const string DiagnosticsRecord = "diagnostics.record";
    public const string ExtensionRead = "extension.read";
    public const string ExtensionUpdate = "extension.update";
    public const string RoleRead = "role.read";
    public const string RoleUpdate = "role.update";
    /// <summary>
    /// Global identity administration (credentials, erasure, data-subject
    /// export). Platform-only: workspace roles never receive a <c>user.*</c>
    /// write permission, because these operations reach across workspaces.
    /// </summary>
    public const string UserCreate = "user.create";
    public const string UserRead = "user.read";
    public const string UserUpdate = "user.update";
    public const string UserDelete = "user.delete";

    /// <summary>
    /// Workspace-membership administration: who belongs to a workspace and in
    /// which workspace role. Grantable to workspace administrators — the
    /// endpoints confine a workspace-bound caller to its own workspace.
    /// </summary>
    public const string MembershipRead = "membership.read";
    public const string MembershipUpdate = "membership.update";
    public const string MembershipDelete = "membership.delete";
    public const string WorkspaceCreate = "workspace.create";
    public const string WorkspaceRead = "workspace.read";
    public const string WorkspaceUpdate = "workspace.update";
    public const string WorkspaceDelete = "workspace.delete";
    public const string IntegrationRead = "integration.read";
    public const string IntegrationManage = "integration.manage";

    /// <summary>
    /// Oberflächentexte (ADR-024). Eigene Rechte statt <c>config.*</c>, weil es einen Fall gibt,
    /// den es sonst nicht gäbe: jemandem die Texte anvertrauen, ohne ihm die Konfiguration zu
    /// geben. Ein Snippet ist Wortlaut, keine Einstellung.
    /// </summary>
    public const string SnippetRead = "snippet.read";
    public const string SnippetUpdate = "snippet.update";
}

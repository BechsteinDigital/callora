namespace Callora.Administration.Api;

/// <summary>
/// Whether the tenant's workspace administrators may assign this plugin themselves.
/// </summary>
public sealed record SetTenantPluginDelegationApiRequest(bool WorkspacesMayAssign);

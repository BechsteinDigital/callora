namespace Callora.Core.Application.Http.Contracts;

/// <summary>
/// Marker for plugin API controllers. The host discovers implementations
/// in the plugin assembly on activation (Shopware-style routing discovery),
/// maps their <see cref="CalloraRouteAttribute"/> routes and removes them
/// again on deactivation. Controllers derive from
/// <see cref="AdminApiController"/> or <see cref="WorkspaceApiController"/>.
/// </summary>
public interface IApiController;

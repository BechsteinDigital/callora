using Callora.Core.Extensibility;

namespace Callora.Core.Application.Http.Contracts;

/// <summary>
/// Marker for plugin API controllers. The host discovers implementations
/// in the plugin assembly on activation (Shopware-style routing discovery),
/// maps their <see cref="CalloraRouteAttribute"/> routes and removes them
/// again on deactivation. Controllers derive from
/// <see cref="AdminApiController"/> or <see cref="WorkspaceApiController"/>.
/// </summary>
[CalloraExtensible("Extension point — implement (via AdminApiController/WorkspaceApiController) to expose a plugin API controller (REV2 §8.2)")]
public interface IApiController;

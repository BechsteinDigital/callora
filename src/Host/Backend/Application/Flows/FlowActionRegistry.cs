using Callora.Host.PluginContracts.Application.Flows;
using Callora.Hosting.Application.Plugins;

namespace Callora.Host.Backend.Application.Flows;

/// <summary>
/// Resolves flow action handlers from host DI and plugin exports; plugin
/// handlers can override host types of the same key.
/// </summary>
public sealed class FlowActionRegistry(
    IEnumerable<IFlowActionHandler> hostHandlers,
    ICalloraPluginCatalog pluginCatalog)
{
    public IFlowActionHandler? Resolve(string actionType)
    {
        if (string.IsNullOrWhiteSpace(actionType))
        {
            return null;
        }

        var normalized = actionType.Trim();
        var pluginHandler = pluginCatalog
            .GetExports<IFlowActionHandler>()
            .LastOrDefault(handler => string.Equals(handler.Type, normalized, StringComparison.OrdinalIgnoreCase));

        return pluginHandler ?? hostHandlers
            .LastOrDefault(handler => string.Equals(handler.Type, normalized, StringComparison.OrdinalIgnoreCase));
    }
}

using Callora.Core.Application.Flows.Contracts;
using Callora.Core.Application.Plugins;

namespace Callora.Core.Application.Flows;

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

        return HostPluginResolution.ResolvePluginWins(
            hostHandlers,
            pluginCatalog.GetExports<IFlowActionHandler>(),
            static handler => handler.Type,
            actionType.Trim());
    }
}

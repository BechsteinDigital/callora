using Callora.Host.PluginContracts.Application.Events;
using Callora.Hosting.Application.Plugins;

namespace Callora.Host.Backend.Application.Events.Business;

/// <summary>
/// Discovery of all business events available on the platform — from host
/// providers and plugin exports. Powers the flow-builder and webhook UI so
/// they know which events exist and which fields each carries (PLAT-270).
/// </summary>
public sealed class BusinessEventRegistry(
    IEnumerable<IBusinessEventProvider> hostProviders,
    ICalloraPluginCatalog pluginCatalog)
{
    public IReadOnlyList<BusinessEventDescriptor> ListDescriptors()
    {
        var byName = new Dictionary<string, BusinessEventDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var descriptor in CollectDescriptors())
        {
            byName[descriptor.EventName] = descriptor;
        }

        return byName.Values
            .OrderBy(static descriptor => descriptor.EventName, StringComparer.Ordinal)
            .ToArray();
    }

    private IEnumerable<BusinessEventDescriptor> CollectDescriptors() =>
        hostProviders
            .Concat(pluginCatalog.GetExports<IBusinessEventProvider>())
            .SelectMany(static provider => provider.GetDescriptors());
}

using Callora.Hosting.Application.Plugins;

namespace Callora.Host.Backend.Tests.Support;

internal sealed class StaticPluginCatalog : ICalloraPluginCatalog
{
    private readonly Dictionary<Type, IReadOnlyList<object>> _exports;
    private readonly string _pluginId;

    public StaticPluginCatalog(Dictionary<Type, IReadOnlyList<object>> exports, string pluginId = "test-plugin")
    {
        _exports = exports;
        _pluginId = pluginId;
    }

    public bool TryGetExport(Type contractType, out object? service)
    {
        if (_exports.TryGetValue(contractType, out var exports) && exports.Count > 0)
        {
            service = exports[0];
            return true;
        }

        service = null;
        return false;
    }

    public IReadOnlyList<object> GetExports(Type contractType)
    {
        if (_exports.TryGetValue(contractType, out var exports))
        {
            return exports;
        }

        return Array.Empty<object>();
    }

    public IReadOnlyList<CalloraPluginExport> GetOwnedExports(Type contractType) =>
        GetExports(contractType)
            .Select(service => new CalloraPluginExport(_pluginId, service))
            .ToArray();
}

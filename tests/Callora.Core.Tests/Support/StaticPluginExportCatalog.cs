using Callora.Core.Application.Plugins;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Catalog over a fixed set of plugin-owned exports, matched by contract type.
/// Lets a test model "plugin X exports this service" without the plugin runtime.
/// </summary>
public sealed class StaticPluginExportCatalog : ICalloraPluginCatalog
{
    private readonly List<CalloraPluginExport> _exports = [];

    /// <summary>Registers one export under an owning plugin id.</summary>
    /// <param name="pluginId">Plugin that owns the export.</param>
    /// <param name="service">The exported service instance.</param>
    public StaticPluginExportCatalog Add(string pluginId, object service)
    {
        _exports.Add(new CalloraPluginExport(pluginId, service));
        return this;
    }

    public bool TryGetExport(Type contractType, out object? service)
    {
        service = Matching(contractType).FirstOrDefault()?.Service;
        return service is not null;
    }

    public IReadOnlyList<object> GetExports(Type contractType) =>
        Matching(contractType).Select(x => x.Service).ToArray();

    public IReadOnlyList<CalloraPluginExport> GetOwnedExports(Type contractType) =>
        Matching(contractType).ToArray();

    private IEnumerable<CalloraPluginExport> Matching(Type contractType) =>
        _exports.Where(x => contractType.IsInstanceOfType(x.Service));
}

using Callora.Host.PluginContracts.Application.Http;
using Callora.Hosting.Application.Plugins;

namespace Callora.Host.Backend.Tests.Support;

/// <summary>
/// Catalog whose controller exports can change between refreshes —
/// simulates plugin activation and deactivation.
/// </summary>
public sealed class MutablePluginCatalog : ICalloraPluginCatalog
{
    private const string OwningPluginId = "test-plugin";
    private object[] _controllers = [];

    public void SetExports(params object[] controllers) => _controllers = controllers;

    public bool TryGetExport(Type contractType, out object? service)
    {
        service = contractType == typeof(IApiController) ? _controllers.FirstOrDefault() : null;
        return service is not null;
    }

    public IReadOnlyList<object> GetExports(Type contractType) =>
        contractType == typeof(IApiController) ? _controllers : [];

    public IReadOnlyList<CalloraPluginExport> GetOwnedExports(Type contractType) =>
        contractType == typeof(IApiController)
            ? _controllers.Select(controller => new CalloraPluginExport(OwningPluginId, controller)).ToArray()
            : [];
}

using Callora.Hosting.Application.Plugins;

namespace Callora.Host.Backend.Application.Lifecycle;

internal sealed class EmptyCalloraPluginCatalog : ICalloraPluginCatalog
{
    public static EmptyCalloraPluginCatalog Instance { get; } = new();

    public bool TryGetExport(Type contractType, out object? service)
    {
        service = null;
        return false;
    }

    public IReadOnlyList<object> GetExports(Type contractType) => [];

    public IReadOnlyList<CalloraPluginExport> GetOwnedExports(Type contractType) => [];
}

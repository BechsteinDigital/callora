namespace Callora.Hosting.Application.Plugins;

/// <summary>
/// Read-only access to runtime plugin exports.
/// </summary>
public interface ICalloraPluginCatalog
{
    /// <summary>
    /// Tries to resolve one exported service by contract type.
    /// </summary>
    bool TryGetExport(Type contractType, out object? service);

    /// <summary>
    /// Returns all exported services for one contract type.
    /// </summary>
    IReadOnlyList<object> GetExports(Type contractType);

    /// <summary>
    /// Returns all exports for one contract type together with their owning
    /// plugin id — for consumers that must gate or attribute by plugin.
    /// </summary>
    IReadOnlyList<CalloraPluginExport> GetOwnedExports(Type contractType);
}

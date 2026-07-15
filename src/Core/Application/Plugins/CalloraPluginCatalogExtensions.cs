namespace Callora.Core.Application.Plugins;

/// <summary>
/// Convenience helpers for typed plugin export access.
/// </summary>
public static class CalloraPluginCatalogExtensions
{
    /// <summary>
    /// Tries to resolve one exported service by contract type.
    /// </summary>
    public static bool TryGetExport<TContract>(this ICalloraPluginCatalog catalog, out TContract? service)
        where TContract : class
    {
        ArgumentNullException.ThrowIfNull(catalog);

        if (catalog.TryGetExport(typeof(TContract), out var candidate) &&
            candidate is TContract typed)
        {
            service = typed;
            return true;
        }

        service = null;
        return false;
    }

    /// <summary>
    /// Returns all exported services for one contract type.
    /// </summary>
    public static IReadOnlyList<TContract> GetExports<TContract>(this ICalloraPluginCatalog catalog)
        where TContract : class
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var exports = catalog.GetExports(typeof(TContract));
        if (exports.Count == 0)
            return Array.Empty<TContract>();

        var typed = new List<TContract>(exports.Count);
        foreach (var export in exports)
        {
            if (export is TContract candidate)
                typed.Add(candidate);
        }

        return typed;
    }
}

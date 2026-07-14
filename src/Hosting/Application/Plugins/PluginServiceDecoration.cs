using Callora.Host.PluginContracts.Application.Extensibility;

namespace Callora.Hosting.Application.Plugins;

/// <summary>
/// Composes plugin-exported <see cref="IServiceDecorator{TService}"/>s around
/// a base platform service (PLAT-266). Host service registrations run their
/// base implementation through this at resolve time, so plugins can wrap any
/// decoratable host service — even though plugins load after the DI container
/// is built.
/// </summary>
public static class PluginServiceDecoration
{
    /// <summary>
    /// Wraps <paramref name="baseService"/> with all exported decorators for
    /// <typeparamref name="TService"/>, applied in ascending
    /// <see cref="IServiceDecorator{TService}.Order"/> (lowest closest to the
    /// base). Returns the base unchanged when no decorators are registered.
    /// </summary>
    public static TService Decorate<TService>(TService baseService, ICalloraPluginCatalog pluginCatalog)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(baseService);
        ArgumentNullException.ThrowIfNull(pluginCatalog);

        var decorators = pluginCatalog
            .GetExports<IServiceDecorator<TService>>()
            .OrderBy(static decorator => decorator.Order)
            .ToArray();

        var current = baseService;
        foreach (var decorator in decorators)
        {
            current = decorator.Decorate(current) ?? current;
        }

        return current;
    }
}

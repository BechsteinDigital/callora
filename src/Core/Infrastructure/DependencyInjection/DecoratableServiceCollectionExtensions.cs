using Callora.Core.Application.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Callora.Core.Infrastructure.DependencyInjection;

/// <summary>
/// Registration helpers for decoratable host services (the Callora counterpart of
/// Symfony service decoration, REV2 §4.1/§9.2).
/// </summary>
public static class DecoratableServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TService"/> as a decoratable singleton: the concrete
    /// <typeparamref name="TImplementation"/> base plus a per-call proxy that composes the
    /// live plugin decorator chain. Plugins decorate it by exporting an
    /// <c>IServiceDecorator&lt;TService&gt;</c>; no per-service proxy type is needed.
    /// </summary>
    public static IServiceCollection AddDecoratableSingleton<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<TImplementation>();
        services.AddSingleton<TService>(sp => DecoratingServiceProxy<TService>.Wrap(
            sp.GetRequiredService<TImplementation>(),
            sp.GetRequiredService<ICalloraPluginCatalog>()));
        return services;
    }
}

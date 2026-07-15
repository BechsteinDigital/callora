namespace Callora.Core.Application.Extensibility.Contracts;

/// <summary>
/// Wraps an existing platform service to change or extend its behavior — the
/// Callora counterpart of Symfony's service decoration (PLAT-266). A plugin
/// exports one per service it wants to decorate; the host composes all
/// decorators around the base implementation, ordered by <see cref="Order"/>
/// (lower runs closer to the base, i.e. is wrapped first). Each decorator
/// receives the inner service and must delegate to it for unchanged paths.
/// </summary>
/// <typeparam name="TService">The decorated service contract.</typeparam>
public interface IServiceDecorator<TService>
    where TService : class
{
    /// <summary>
    /// Composition order — lower values wrap closer to the base service, so
    /// higher values run first when the outermost decorator is called.
    /// </summary>
    int Order { get; }

    /// <summary>Returns a wrapper delegating to <paramref name="inner"/>.</summary>
    TService Decorate(TService inner);
}

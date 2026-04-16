namespace Callora.Hosting.Infrastructure.DependencyInjection;

/// <summary>
/// Minimal host service registry abstraction used to avoid hard framework coupling.
/// </summary>
public interface IHostServiceRegistry
{
    /// <summary>
    /// Registers a singleton service type mapping.
    /// </summary>
    void AddSingleton(Type serviceType, Type implementationType);

    /// <summary>
    /// Registers a singleton instance.
    /// </summary>
    void AddSingleton(Type serviceType, object instance);
}

namespace Callora.Modules.Abstractions.Application.Registration;

/// <summary>
/// Minimal service registry abstraction for module registration.
/// </summary>
public interface IModuleServiceRegistry
{
    /// <summary>
    /// Registers a singleton service mapping.
    /// </summary>
    void AddSingleton(Type serviceType, Type implementationType);
}

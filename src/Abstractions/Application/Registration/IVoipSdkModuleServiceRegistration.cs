namespace Callora.Modules.Abstractions.Application.Registration;

/// <summary>
/// Registers module services into dependency injection.
/// </summary>
public interface ICalloraModuleServiceRegistration
{
    /// <summary>
    /// Registers services needed by the module.
    /// </summary>
    void Register(IModuleServiceRegistry services);
}

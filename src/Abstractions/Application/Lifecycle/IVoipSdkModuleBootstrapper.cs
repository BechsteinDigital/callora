namespace Callora.Modules.Abstractions.Application.Lifecycle;

/// <summary>
/// Bootstraps module runtime behavior.
/// </summary>
public interface ICalloraModuleBootstrapper
{
    /// <summary>
    /// Invoked once after service registration finished.
    /// </summary>
    Task BootstrapAsync(IServiceProvider services, CancellationToken cancellationToken = default);
}

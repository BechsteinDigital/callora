namespace Callora.Modules.Abstractions.Application.Lifecycle;

/// <summary>
/// Core runtime hook for modules to attach behavior after bootstrap.
/// </summary>
public interface IRuntimeModuleHook
{
    /// <summary>
    /// Invoked when the hosting layer completed module registration.
    /// </summary>
    void OnModulesBootstrapped();
}

namespace Callora.Hosting.Application.Contracts;

/// <summary>
/// Core hosting extension point for external host integrations.
/// </summary>
public interface ICalloraHostBuilderHook
{
    /// <summary>
    /// Applies host-specific configuration.
    /// </summary>
    void Configure(IServiceProvider serviceProvider);
}

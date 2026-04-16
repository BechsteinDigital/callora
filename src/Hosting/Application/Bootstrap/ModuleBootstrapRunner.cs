using Callora.Modules.Abstractions.Application.Lifecycle;

namespace Callora.Hosting.Application.Bootstrap;

/// <summary>
/// Executes registered module bootstrappers.
/// </summary>
public sealed class ModuleBootstrapRunner
{
    private readonly IEnumerable<ICalloraModuleBootstrapper> _bootstrappers;

    /// <summary>
    /// Creates a new runner instance.
    /// </summary>
    public ModuleBootstrapRunner(IEnumerable<ICalloraModuleBootstrapper> bootstrappers)
    {
        _bootstrappers = bootstrappers;
    }

    /// <summary>
    /// Executes all bootstrappers sequentially.
    /// </summary>
    public async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        foreach (var bootstrapper in _bootstrappers)
        {
            await bootstrapper.BootstrapAsync(services, cancellationToken).ConfigureAwait(false);
        }
    }
}

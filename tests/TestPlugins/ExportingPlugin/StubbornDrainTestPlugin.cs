using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Domain.Plugins.Contracts;
using Microsoft.Extensions.Logging;

namespace Callora.TestPlugin.Exporting;

/// <summary>
/// A plugin whose work never runs dry: <see cref="DrainAsync"/> waits until the host's deadline
/// cancels it. Exists to prove the deadline is real — a plugin may delay a deactivation, never
/// prevent it.
/// </summary>
public sealed class StubbornDrainTestPlugin : IHostManagedPlugin, IDrainablePlugin
{
    private ILogger? _lifecycle;

    public string PluginId => "stubborn-drain-test-plugin";

    public string DisplayName => "Stubborn Drain Test Plugin";

    public ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        _lifecycle = (context.Services.GetService(typeof(ILoggerFactory)) as ILoggerFactory)
            ?.CreateLogger(PluginId);
        _lifecycle?.LogInformation("start");
        return ValueTask.CompletedTask;
    }

    public async ValueTask DrainAsync(CancellationToken cancellationToken = default)
    {
        _lifecycle?.LogInformation("drain-begin");
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Deliberately swallowed: this models the well-behaved half of a stubborn plugin, one
            // that notices the deadline and returns instead of throwing at the host.
            _lifecycle?.LogInformation("drain-cancelled");
        }
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        _lifecycle?.LogInformation("stop");
        return ValueTask.CompletedTask;
    }
}

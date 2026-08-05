using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Domain.Plugins.Contracts;
using Microsoft.Extensions.Logging;

namespace Callora.TestPlugin.Exporting;

/// <summary>
/// A plugin that reports its own lifecycle so a test can assert the order the host runs it in.
/// </summary>
/// <remarks>
/// It reports through <see cref="ILoggerFactory"/> because that is one of the few surfaces the
/// curated plugin service provider hands out — and therefore the only channel a fixture has for
/// speaking back across the load-context boundary without inventing a contract for it.
/// </remarks>
public sealed class DrainingTestPlugin : IHostManagedPlugin, IDrainablePlugin
{
    private ILogger? _lifecycle;

    public string PluginId => "draining-test-plugin";

    public string DisplayName => "Draining Test Plugin";

    public ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        _lifecycle = (context.Services.GetService(typeof(ILoggerFactory)) as ILoggerFactory)
            ?.CreateLogger(PluginId);
        _lifecycle?.LogInformation("start");
        return ValueTask.CompletedTask;
    }

    public ValueTask DrainAsync(CancellationToken cancellationToken = default)
    {
        _lifecycle?.LogInformation("drain");
        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        _lifecycle?.LogInformation("stop");
        return ValueTask.CompletedTask;
    }
}

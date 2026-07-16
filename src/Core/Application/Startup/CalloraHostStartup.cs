using Callora.Core.Application.Options;
using Callora.Core.Application.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Callora.Core.Application.Startup;

/// <summary>
/// Host startup helper for optional plugin auto-loading. Plugins are installed
/// first, then activated in dependency order (REV2 §5.1) so a plugin's required
/// capabilities are provided before it starts, independent of file-system order.
/// </summary>
public sealed class CalloraHostStartup
{
    private readonly CalloraHostingOptions _options;
    private readonly ICalloraPluginRuntime? _pluginRuntime;

    /// <summary>
    /// Creates a startup helper.
    /// </summary>
    public CalloraHostStartup(
        CalloraHostingOptions options,
        ICalloraPluginRuntime? pluginRuntime = null)
    {
        _options = options;
        _pluginRuntime = pluginRuntime;
    }

    /// <summary>
    /// Installs plugins from the configured directory, then activates the loaded
    /// plugins in dependency order. Capability dependencies are resolved via an
    /// <see cref="IPluginPackageRegistryReader"/> from <paramref name="services"/>
    /// when available; otherwise activation falls back to discovery order.
    /// </summary>
    public async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (_pluginRuntime is null)
        {
            return;
        }

        if (_options.AutoLoadPlugins &&
            !string.IsNullOrWhiteSpace(_options.PluginDirectory) &&
            Directory.Exists(_options.PluginDirectory))
        {
            foreach (var pluginAssembly in Directory.EnumerateFiles(_options.PluginDirectory, "*.dll", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _pluginRuntime.InstallAsync(pluginAssembly, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }

        if (_options.AutoActivateInstalledPlugins)
        {
            var registryReader = services.GetService<IPluginPackageRegistryReader>();
            await ActivateInDependencyOrderAsync(registryReader, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ActivateInDependencyOrderAsync(
        IPluginPackageRegistryReader? registryReader,
        CancellationToken cancellationToken)
    {
        var plugins = _pluginRuntime!.LoadedPlugins
            .Select(static plugin => (plugin.PluginId, plugin.AssemblyPath))
            .ToList();

        foreach (var pluginId in await PluginActivationOrdering.OrderAsync(plugins, registryReader, cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _pluginRuntime.ActivateAsync(pluginId, cancellationToken).ConfigureAwait(false);
        }
    }
}

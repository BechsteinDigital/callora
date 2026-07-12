using Callora.Hosting.Application.Options;
using Callora.Hosting.Application.Plugins;

namespace Callora.Hosting.Application.Startup;

/// <summary>
/// Host startup helper for optional plugin auto-loading.
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
    /// Installs and optionally activates plugins from the configured plugin directory.
    /// </summary>
    public async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        if (_pluginRuntime is null)
            return;

        if (_options.AutoLoadPlugins &&
            !string.IsNullOrWhiteSpace(_options.PluginDirectory) &&
            Directory.Exists(_options.PluginDirectory))
        {
            foreach (var pluginAssembly in Directory.EnumerateFiles(_options.PluginDirectory, "*.dll", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var install = await _pluginRuntime.InstallAsync(pluginAssembly, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (_options.AutoActivateInstalledPlugins &&
                    install.Plugin is not null &&
                    install.IsSuccess)
                {
                    await _pluginRuntime.ActivateAsync(install.Plugin.PluginId, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        if (_options.AutoActivateInstalledPlugins)
        {
            var activeCandidates = _pluginRuntime.LoadedPlugins
                .Where(static plugin => plugin.State == RuntimePluginState.Active)
                .Select(static plugin => plugin.PluginId)
                .ToArray();

            foreach (var pluginId in activeCandidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _pluginRuntime.ActivateAsync(pluginId, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

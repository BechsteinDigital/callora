using Callora.Hosting.Application.Bootstrap;
using Callora.Hosting.Application.Options;
using Callora.Modules.Abstractions.Application.Plugins;

namespace Callora.Hosting.Application.Startup;

/// <summary>
/// Host startup helper for bootstrapping Callora modules.
/// </summary>
public sealed class CalloraHostStartup
{
    private readonly ModuleBootstrapRunner _runner;
    private readonly CalloraHostingOptions _options;
    private readonly ICalloraPluginRuntime? _pluginRuntime;

    /// <summary>
    /// Creates a startup helper.
    /// </summary>
    public CalloraHostStartup(
        ModuleBootstrapRunner runner,
        CalloraHostingOptions options,
        ICalloraPluginRuntime? pluginRuntime = null)
    {
        _runner = runner;
        _options = options;
        _pluginRuntime = pluginRuntime;
    }

    /// <summary>
    /// Bootstraps modules when configured.
    /// </summary>
    public async Task StartAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        if (_options.AutoBootstrapModules)
        {
            await _runner.RunAsync(services, cancellationToken).ConfigureAwait(false);
        }

        if (_options.AutoLoadPlugins &&
            _pluginRuntime is not null &&
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

        if (_options.AutoActivateInstalledPlugins && _pluginRuntime is not null)
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

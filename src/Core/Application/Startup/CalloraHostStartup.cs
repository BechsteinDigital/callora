using Callora.Core.Application.Lifecycle;
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
            await InstallFromDirectoryAsync(services, cancellationToken).ConfigureAwait(false);
        }

        if (_options.AutoActivateInstalledPlugins)
        {
            var registryReader = services.GetService<IPluginPackageRegistryReader>();
            await ActivateInDependencyOrderAsync(registryReader, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Installiert die Assemblies aus dem Plugin-Verzeichnis — über dieselben Tore wie jede
    /// andere Installation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hier stand ein direkter Aufruf von <c>ICalloraPluginRuntime.InstallAsync</c>. Der lud die
    /// Assembly und war fertig: keine Signaturprüfung, kein Registry-Abgleich, kein
    /// Audit-Eintrag. Wer eine DLL in das Verzeichnis legen konnte, brachte sie am gesamten
    /// Vertrauensmodell vorbei in den Prozess — und zwar auf dem Normalweg, denn dieser Dienst
    /// läuft VOR der geprüften Discovery und findet das Verzeichnis als Erster vor.
    /// </para>
    /// <para>
    /// Ohne <see cref="IPluginLifecycleService"/> wird nichts installiert. Ein Rückfall auf die
    /// rohe Runtime wäre die Lücke von vorhin unter anderem Namen: eine Komposition, der die
    /// Tore fehlen, darf kein Schlupfloch sein, sondern installiert eben nicht.
    /// </para>
    /// </remarks>
    private async Task InstallFromDirectoryAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        // Eigener Scope: Der Lifecycle-Service ist scoped (er schreibt in die Datenbank), der
        // Startup-Pfad läuft außerhalb jedes Requests.
        using var scope = services.CreateScope();
        if (scope.ServiceProvider.GetService<IPluginLifecycleService>() is not { } lifecycle)
        {
            return;
        }

        foreach (var pluginAssembly in Directory.EnumerateFiles(
                     _options.PluginDirectory, "*.dll", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await lifecycle
                .InstallAsync(
                    new InstallPluginCommand(pluginAssembly, EntryTypeName: null, RequestedBy: "system:startup-autoload"),
                    cancellationToken)
                .ConfigureAwait(false);
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

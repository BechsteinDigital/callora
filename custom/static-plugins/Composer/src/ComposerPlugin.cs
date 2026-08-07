using Callora.Core.Application.Persistence.Contracts;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Surfaces.Layout;
using Callora.Core.Domain.Plugins.Contracts;
using Callora.Plugin.Composer.Application;
using Callora.Plugin.Composer.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Callora.Plugin.Composer;

/// <summary>
/// The Surface Composer: the editor in which someone who does not write code builds a surface
/// from blocks.
/// <para>
/// A plugin with full citizenship, not a special case in the core (ADR-014 §10.4). It brings its
/// own schema, implements the core's one contract — <c>ISurfaceLayoutSource</c> — and is the most
/// demanding consumer of the platform rather than an exception to it. Nothing about layouts, the
/// editor or its domain leaks into the core; uninstall the plugin and a surface renders from
/// <c>.njk</c> as it always did.
/// </para>
/// </summary>
public sealed class ComposerPlugin : IHostManagedPlugin
{
    private ILogger? _logger;

    /// <inheritdoc />
    public string PluginId => "composer";

    /// <inheritdoc />
    public string DisplayName => "Surface Composer";

    /// <inheritdoc />
    public async ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var loggers = context.Services.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
        _logger = loggers?.CreateLogger<ComposerPlugin>();

        var factory = context.Services.GetService(typeof(IPluginDbContextFactory<ComposerDbContext>))
            as IPluginDbContextFactory<ComposerDbContext>;
        if (factory is null)
        {
            // A host composed without plugin persistence. The composer is data — without a
            // database there is nothing to edit and nothing to publish, so it stays quiet rather
            // than half-registering a source that could never answer.
            _logger?.LogWarning(
                "Composer started without plugin persistence; no layouts will be served.");
            return;
        }

        // The schema must exist before anything reads it — first step, as the contract says.
        await factory.MigrateAsync(cancellationToken).ConfigureAwait(false);

        var store = new SurfaceLayoutStore(factory, TimeProvider.System);
        var sourceLogger = loggers?.CreateLogger<ComposerLayoutSource>()
            ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ComposerLayoutSource>.Instance;

        context.Export<ISurfaceLayoutSource>(new ComposerLayoutSource(store, sourceLogger));
    }

    /// <inheritdoc />
    public ValueTask StopAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}

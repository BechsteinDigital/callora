using Callora.Core.Application.Persistence.Contracts;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Domain.Plugins.Contracts;
using Callora.Plugin.Communication.Api.WebSocket;
using Callora.Plugin.Communication.Application.Admin;
using Callora.Plugin.Communication.Application.Compliance;
using Callora.Plugin.Communication.Infrastructure.Media;
using Callora.Plugin.Communication.Infrastructure.Persistence;
using Callora.Plugin.Communication.Infrastructure.Persistence.Stores;

namespace Callora.Plugin.Communication;

/// <summary>
/// First-party System-Tier communication foundation. Composition Root: v1 exports the
/// operator control surface (Admin API). Domain/persistence, the SDK/media bridge and the
/// public REST/WebSocket/Webhook consumer surface arrive in later bausteine.
/// </summary>
public sealed class CommunicationPlugin : IHostManagedPlugin
{
    /// <summary>Stable plugin identifier.</summary>
    public const string Id = "communication";

    /// <inheritdoc />
    public string PluginId => Id;

    /// <inheritdoc />
    public string DisplayName => "Communication";

    /// <inheritdoc />
    public async ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Export<IHostAdminApiExtensionContributor>(new CommunicationAdminApiExtensionContributor());

        // Persistenz: eigenes Schema migrieren + GDPR-Purge-Contributor exportieren — nur wenn der
        // Host die DB-Factory bereitstellt (ein minimaler Host ohne Persistenz degradiert sauber).
        if (context.Services.GetService(typeof(IPluginDbContextFactory<CommunicationDbContext>))
            is IPluginDbContextFactory<CommunicationDbContext> dbContextFactory)
        {
            await dbContextFactory.MigrateAsync(cancellationToken).ConfigureAwait(false);

            context.Export<IWorkspaceDataPurgeContributor>(new CommunicationDataPurgeContributor(
                new CommunicationWorkspaceDataPurger(dbContextFactory)));

            // Media WebSocket surface (/ws/communication/media/{connectToken}) — the connect-token
            // authorizer resolves sessions against the plugin DB, so it needs the factory. Audio
            // attaches once a call runtime exists (B5); until then the bridge closes cleanly.
            context.Export<IHostWebSocketEndpointContributor>(new CommunicationMediaWebSocketContributor(
                new EfMediaStreamSessionStore(dbContextFactory),
                new NoCallAudioStreamProvider()));
        }
    }

    /// <inheritdoc />
    public ValueTask StopAsync(CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}

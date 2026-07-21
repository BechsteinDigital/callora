using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Domain.Plugins.Contracts;
using Callora.Plugin.Communication.Application.Admin;

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
    public ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Export<IHostAdminApiExtensionContributor>(new CommunicationAdminApiExtensionContributor());

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask StopAsync(CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}

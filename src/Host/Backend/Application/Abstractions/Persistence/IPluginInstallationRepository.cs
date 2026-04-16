using Callora.Host.Backend.Domain.Plugins;

namespace Callora.Host.Backend.Application.Abstractions.Persistence;

public interface IPluginInstallationRepository
{
    Task<IReadOnlyList<PluginInstallation>> ListAsync(CancellationToken cancellationToken = default);

    Task<PluginInstallation?> GetByPluginIdAsync(
        string pluginId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        PluginInstallation installation,
        CancellationToken cancellationToken = default);
}

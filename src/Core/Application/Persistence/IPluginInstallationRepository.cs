using Callora.Core.Domain.Plugins;

namespace Callora.Core.Application.Persistence;

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

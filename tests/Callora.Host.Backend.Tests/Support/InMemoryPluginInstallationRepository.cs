using Callora.Host.Backend.Application.Abstractions.Persistence;
using Callora.Host.Backend.Domain.Plugins;

namespace Callora.Host.Backend.Tests.Support;

public sealed class InMemoryPluginInstallationRepository : IPluginInstallationRepository
{
    private readonly Dictionary<string, PluginInstallation> _items = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<PluginInstallation>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PluginInstallation>>(_items.Values.OrderBy(x => x.PluginId).ToList());

    public Task<PluginInstallation?> GetByPluginIdAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        _items.TryGetValue(pluginId, out var installation);
        return Task.FromResult(installation);
    }

    public Task AddAsync(
        PluginInstallation installation,
        CancellationToken cancellationToken = default)
    {
        _items[installation.PluginId] = installation;
        return Task.CompletedTask;
    }
}

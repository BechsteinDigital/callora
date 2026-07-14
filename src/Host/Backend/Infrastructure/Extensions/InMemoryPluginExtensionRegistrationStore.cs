using Callora.Host.Backend.Application.Extensions;
using Callora.Host.Backend.Application.Plugins;

namespace Callora.Host.Backend.Infrastructure.Extensions;

public sealed class InMemoryPluginExtensionRegistrationStore : IPluginExtensionRegistrationStore
{
    private readonly object _sync = new();
    private readonly Dictionary<string, PluginExtensionRegistrationSnapshot> _items =
        new(StringComparer.OrdinalIgnoreCase);

    public ValueTask UpsertAsync(
        string pluginId,
        IReadOnlyList<PluginPackageExtensionRegistration> extensionRegistrations,
        IReadOnlyList<string> capabilities,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        var normalizedPluginId = pluginId.Trim();
        var normalizedCapabilities = capabilities
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var normalizedRegistrations = extensionRegistrations
            .Where(x => !string.IsNullOrWhiteSpace(x.ExtensionPointId))
            .Select(x => new PluginPackageExtensionRegistration(x.ExtensionPointId.Trim(), x.Surface))
            .ToArray();

        lock (_sync)
        {
            _items[normalizedPluginId] = new PluginExtensionRegistrationSnapshot(
                normalizedPluginId,
                normalizedRegistrations,
                normalizedCapabilities);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask RemoveAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return ValueTask.CompletedTask;
        }

        lock (_sync)
        {
            _items.Remove(pluginId.Trim());
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<PluginExtensionRegistrationSnapshot>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            return ValueTask.FromResult<IReadOnlyList<PluginExtensionRegistrationSnapshot>>(
                _items.Values
                    .OrderBy(x => x.PluginId, StringComparer.OrdinalIgnoreCase)
                    .ToArray());
        }
    }
}

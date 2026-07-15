using Callora.Core.Application.Features;
using Callora.Core.Application.Policies;

namespace Callora.Core.Infrastructure.Features;

/// <summary>
/// Feature flags from <see cref="BackendHostOptions.FeatureFlags"/> (PLAT-263).
/// Unknown flags default to off; lookups are case-insensitive.
/// </summary>
public sealed class ConfiguredFeatureFlagService : IFeatureFlagService
{
    private readonly IReadOnlyDictionary<string, bool> _flags;

    public ConfiguredFeatureFlagService(BackendHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _flags = new Dictionary<string, bool>(
            options.FeatureFlags ?? new Dictionary<string, bool>(),
            StringComparer.OrdinalIgnoreCase);
    }

    public bool IsEnabled(string key) =>
        !string.IsNullOrWhiteSpace(key) && _flags.TryGetValue(key.Trim(), out var enabled) && enabled;

    public IReadOnlyDictionary<string, bool> GetAll() => _flags;
}

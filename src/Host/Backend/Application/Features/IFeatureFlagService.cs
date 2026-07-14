namespace Callora.Host.Backend.Application.Features;

/// <summary>
/// Central feature-flag lookup (PLAT-263). Flags gate risky features and cloud
/// rollouts and are resolved from configuration.
/// </summary>
public interface IFeatureFlagService
{
    /// <summary>True when the named flag is defined and enabled; false otherwise (default off).</summary>
    bool IsEnabled(string key);

    /// <summary>All defined flags and their state.</summary>
    IReadOnlyDictionary<string, bool> GetAll();
}

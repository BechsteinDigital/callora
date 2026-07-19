using Callora.Core.Extensibility;

namespace Callora.Core.Application.Features;

/// <summary>
/// Central feature-flag lookup (PLAT-263). Flags gate risky features and cloud
/// rollouts and are resolved from configuration.
/// </summary>
[CalloraExtensible(ExtensionPointMode.Decoratable, "Decorate via IServiceDecorator<IFeatureFlagService> to resolve flags from an external provider (REV2 §4.1)")]
public interface IFeatureFlagService
{
    /// <summary>True when the named flag is defined and enabled; false otherwise (default off).</summary>
    bool IsEnabled(string key);

    /// <summary>All defined flags and their state.</summary>
    IReadOnlyDictionary<string, bool> GetAll();
}

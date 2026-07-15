namespace Callora.Core.Application.Policies;

/// <summary>
/// Rollout ring assigned to one tenant for plugin activation.
/// </summary>
public enum PluginRolloutRing
{
    Stable = 0,
    Beta = 1,
    Dev = 2
}

namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// A plugin's source of runtime-conditional capability grants, exported so the host can derive whether
/// a plugin's <c>conditionalCapabilities</c> are currently provided. The host reads
/// <see cref="CurrentGrants"/> when evaluating availability and subscribes to
/// <see cref="CapabilitiesChanged"/> to react to runtime transitions (health-derived, see the
/// runtime-capability mechanism).
/// </summary>
/// <remarks>
/// <see cref="CapabilitiesChanged"/> may be raised from any thread (typically a plugin's own signaling
/// thread); handlers must be fast and non-blocking. <see cref="CurrentGrants"/> is a point-in-time
/// snapshot of the grants that hold when it is read.
/// </remarks>
public interface IRuntimeCapabilitySource
{
    /// <summary>The runtime-conditional capabilities currently provided, across all scopes.</summary>
    IReadOnlyCollection<RuntimeCapabilityGrant> CurrentGrants { get; }

    /// <summary>Raised when a runtime-conditional capability becomes satisfied or unsatisfied.</summary>
    event Action<RuntimeCapabilityChanged>? CapabilitiesChanged;
}

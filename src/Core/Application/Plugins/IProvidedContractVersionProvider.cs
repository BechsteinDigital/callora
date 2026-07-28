using Callora.Core.Extensibility;
using Semver;

namespace Callora.Core.Application.Plugins;

/// <summary>
/// Resolves the SemVer version the host actually provides for a plugin dependency
/// (a shared contract or a framework assembly), so the install-time dependency gate
/// can compare it against the plugin's declared npm range.
/// </summary>
/// <remarks>
/// Abstracted so <see cref="PluginDependencyVersionGate"/> is testable without loading
/// real assemblies. A contract that the host does not provide resolves to <c>null</c> —
/// its presence is the activation planner's concern, not this gate's (Runtime-Capability
/// ABI-Compat).
/// </remarks>
[CalloraInternal("Dependency-version resolution for the install gate — not a plugin contract")]
public interface IProvidedContractVersionProvider
{
    /// <summary>
    /// Resolves the provided version of <paramref name="contractId"/>, or <c>null</c>
    /// when the host provides no assembly under that identity.
    /// </summary>
    SemVersion? Resolve(string contractId);
}

using Callora.Core.Extensibility;

namespace Callora.Core.Application.Security;

/// <summary>
/// The permission keys installed plugins declare in their manifests.
/// </summary>
/// <remarks>
/// Read from the manifests of installed plugins rather than from a table: the declarations
/// already live in <c>registry.json</c>, and persisting a second copy would create two
/// answers that can disagree — with the disagreement showing up as a key an operator can see
/// but not grant, which is the failure this whole path exists to remove.
/// </remarks>
[CalloraInternal("Permission inventory source — enforcement, not a plugin contract (REV2 §7.2)")]
public interface IPluginDeclaredPermissionCatalog
{
    /// <summary>Every key declared by an installed plugin, de-duplicated.</summary>
    Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default);
}

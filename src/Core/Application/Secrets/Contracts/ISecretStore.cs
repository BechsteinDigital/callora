using Callora.Core.Extensibility;

namespace Callora.Core.Application.Secrets.Contracts;

/// <summary>
/// Host-provided read access to named secrets. Backed by environment
/// variables and configuration by default; extensible to vault providers.
/// Secrets never live in the repository.
/// </summary>
[CalloraInternal("Host secret store — enforcement, not a plugin contract; plugins protect their own data via IPluginDataProtector (REV2 §7.2)")]
public interface ISecretStore
{
    /// <summary>
    /// Returns the secret value, or null when it is not configured.
    /// </summary>
    Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default);
}

namespace Callora.Host.PluginContracts.Application.Secrets;

/// <summary>
/// Host-provided read access to named secrets. Backed by environment
/// variables and configuration by default; extensible to vault providers.
/// Secrets never live in the repository.
/// </summary>
public interface ISecretStore
{
    /// <summary>
    /// Returns the secret value, or null when it is not configured.
    /// </summary>
    Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken = default);
}

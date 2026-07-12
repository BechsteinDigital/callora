namespace Callora.Host.PluginContracts.Application.Secrets;

/// <summary>
/// Host-provided encryption for sensitive plugin data at rest (for example
/// credentials stored in the plugin data store). Payloads are isolated per
/// plugin: a value protected for one plugin cannot be unprotected for another.
/// Resolvable from <c>IHostPluginContext.Services</c>.
/// </summary>
public interface IPluginDataProtector
{
    /// <summary>
    /// Encrypts one value for the given plugin.
    /// </summary>
    string Protect(string pluginId, string plaintext);

    /// <summary>
    /// Tries to decrypt one previously protected value. Returns false when the
    /// payload is not a valid protected value for this plugin (for example
    /// legacy plaintext), leaving migration decisions to the caller.
    /// </summary>
    bool TryUnprotect(string pluginId, string protectedValue, out string plaintext);
}

namespace Callora.Core.Domain.Plugins;

/// <summary>
/// A plugin lifecycle transition was rejected against the aggregate's current state, e.g.
/// activating or deactivating an installation that is already uninstalled. Caller-facing
/// fault with a stable code, raised by the <see cref="PluginInstallation"/> aggregate itself.
/// </summary>
public sealed class PluginInstallationException : CalloraException
{
    private const int Conflict = 409;

    private PluginInstallationException(string errorCode, int statusCode, string message)
        : base(errorCode, statusCode, message)
    {
    }

    /// <summary>Error code for a lifecycle transition on an already-uninstalled plugin.</summary>
    public const string AlreadyUninstalledCode = "PLUGIN__ALREADY_UNINSTALLED";

    /// <summary>The plugin is already uninstalled and cannot undergo further lifecycle transitions.</summary>
    /// <param name="pluginId">The uninstalled plugin.</param>
    public static PluginInstallationException AlreadyUninstalled(string pluginId) =>
        new(AlreadyUninstalledCode, Conflict, $"Plugin '{pluginId}' is already uninstalled.");
}

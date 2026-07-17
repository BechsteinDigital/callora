using Callora.Core.Domain.Plugins;
using Xunit;

namespace Callora.Core.Tests.Domain.Plugins;

/// <summary>
/// The plugin aggregate rejects lifecycle transitions after uninstall with a
/// typed <see cref="PluginInstallationException"/> carrying a stable code and
/// HTTP status (R4). Both activation and deactivation are guarded.
/// </summary>
public sealed class PluginInstallationTests
{
    private static PluginInstallation UninstalledInstallation()
    {
        var now = DateTimeOffset.UtcNow;
        var installation = PluginInstallation.CreateInstalled(
            "acme.plugin", "Acme", "/plugins/acme.dll", entryTypeName: null, now);
        installation.MarkUninstalled(now);
        return installation;
    }

    [Fact]
    public void MarkActivated_AfterUninstall_ThrowsAlreadyUninstalledConflict()
    {
        var installation = UninstalledInstallation();

        var ex = Assert.Throws<PluginInstallationException>(
            () => installation.MarkActivated(DateTimeOffset.UtcNow));

        Assert.Equal(PluginInstallationException.AlreadyUninstalledCode, ex.ErrorCode);
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public void MarkDeactivated_AfterUninstall_ThrowsAlreadyUninstalledConflict()
    {
        var installation = UninstalledInstallation();

        var ex = Assert.Throws<PluginInstallationException>(
            () => installation.MarkDeactivated(DateTimeOffset.UtcNow));

        Assert.Equal(PluginInstallationException.AlreadyUninstalledCode, ex.ErrorCode);
        Assert.Equal(409, ex.StatusCode);
    }
}

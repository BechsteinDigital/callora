using Callora.Core.Application.Security;
using Xunit;

namespace Callora.Core.Tests.Application.Security;

/// <summary>
/// Eine Berechtigung, die ein Endpunkt verlangt, muss auch vergeben werden können.
/// </summary>
/// <remarks>
/// Der Katalog der Rollenverwaltung filterte über <see cref="BackendPermissionKeyValidator"/>,
/// und der verlangte genau zwei Segmente aus einer festen Aktionsliste. Jede Plugin-Berechtigung
/// hat drei (<c>communication.accounts.read</c>) oder eine eigene Aktion
/// (<c>composer.layout.publish</c>) — also fiel sie heraus.
///
/// <para>
/// Das ist die teuerste Kombination, die es gibt: Die Absicherung wirkte weiter, denn die
/// Autorisierung vergleicht die ganze Zeichenkette. Nur vergeben konnte man die Berechtigung
/// nicht. Ein Operator sah „missing the 'communication.calls' claim" und fand in der
/// Rollenverwaltung nichts, was er hätte anhaken können.
/// </para>
/// </remarks>
public sealed class PluginPermissionsReachTheRoleEditorTests
{
    [Theory]
    // Genau die Schlüssel, die die drei First-Party-Plugins mitbringen.
    [InlineData("communication.accounts.read", "communication.accounts", "read")]
    [InlineData("communication.accounts.manage", "communication.accounts", "manage")]
    [InlineData("communication.calls.read", "communication.calls", "read")]
    [InlineData("composer.layout.read", "composer.layout", "read")]
    [InlineData("composer.layout.write", "composer.layout", "write")]
    [InlineData("composer.layout.publish", "composer.layout", "publish")]
    // Und die des Kerns, die weiterhin gelten müssen.
    [InlineData("user.read", "user", "read")]
    [InlineData("workspace.update", "workspace", "update")]
    public void APluginPermissionIsValidAndSplitsAtTheLastDot(string key, string function, string action)
    {
        Assert.True(BackendPermissionKeyValidator.IsValid(key), $"{key} fiele aus dem Katalog.");

        Assert.True(BackendPermissionKey.TryParse(key, out var parsed));
        // Die Aktion ist das LETZTE Segment. Am ersten zu teilen machte aus zwei getrennten
        // Berechtigungen eine Gruppe, die keine ist.
        Assert.Equal(function, parsed.Function);
        Assert.Equal(action, parsed.Action);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    // Ohne Aktion ist es keine Berechtigung, sondern ein Namensraum.
    [InlineData("communication")]
    [InlineData("communication.")]
    [InlineData(".read")]
    [InlineData("communication..read")]
    public void SomethingThatIsNotAPermissionKeyStaysInvalid(string? key)
    {
        Assert.False(BackendPermissionKeyValidator.IsValid(key!));
    }
}

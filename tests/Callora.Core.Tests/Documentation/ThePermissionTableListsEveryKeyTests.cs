using Callora.Core.Application.Security;
using Callora.Core.Tests.Cli;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace Callora.Core.Tests.Documentation;

/// <summary>
/// Jeder Kern-Berechtigungsschlüssel steht in <c>docs-site/reference/permissions.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Der Befund:</b> Die Tabelle war bereits veraltet — <c>PUT /api/workspaces/{wk}/plugins/{id}</c>
/// stand in keiner Zeile, obwohl der Endpunkt seit Längerem existiert. Eine Rechte-Tabelle, die
/// Schlüssel auslässt, ist schlimmer als keine: Wer eine Rolle schneidet, liest sie und hält sie für
/// vollständig, und das fehlende Recht fällt erst auf, wenn jemand einen 403 bekommt, den niemand
/// erklären kann.
/// </para>
/// <para>
/// Geprüft wird nur die Richtung, die veralten kann: Jeder Schlüssel im Code braucht eine Zeile. Der
/// umgekehrte Fall — eine Zeile ohne Schlüssel — ist gutartig, weil Plugins eigene Schlüssel
/// mitbringen und die Tabelle sie als Beispiele führen darf.
/// </para>
/// </remarks>
public sealed class ThePermissionTableListsEveryKeyTests
{
    [Fact]
    public void EveryCorePermissionKeyHasARow()
    {
        var table = File.ReadAllText(Path.Combine(
            ScaffoldedPluginFixture.ResolveRepositoryRoot(),
            "docs-site/reference/permissions.md"));

        var documented = Regex
            .Matches(table, @"^\|\s*`([a-z][a-z0-9.]*)`\s*\|", RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        var missing = typeof(BackendPermissionKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true, FieldType.FullName: "System.String" })
            .Select(field => (string)field.GetRawConstantValue()!)
            .Where(key => !documented.Contains(key))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(missing);
    }
}

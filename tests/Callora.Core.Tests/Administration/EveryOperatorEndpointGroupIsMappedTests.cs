using System.Text.RegularExpressions;
using Xunit;

namespace Callora.Core.Tests.Administration;

/// <summary>
/// Die Operator-API wird vollständig registriert — keine Gruppe hinter einer Bedingung.
/// </summary>
/// <remarks>
/// <c>/api/tenants</c> hing an <c>EnableTenantManagementApi</c>. Den Schalter setzte keine
/// Konfiguration, kein Test und keine Distribution: Die Mandantenverwaltung war in <em>jeder</em>
/// Installation tot, während die Admin-Oberfläche sie im Menü führte und ihre Endpunkte
/// bedingungslos aufrief.
///
/// <para>
/// Der Schalter fügte auch keine Autorisierung hinzu — jeder Endpunkt verlangt ohnehin
/// <c>tenant.read</c> bzw. <c>tenant.write</c>. Er konnte nur eines: aus einem 403 („dürfen Sie
/// nicht") ein 404 machen („gibt es nicht"). Das ist die teure Sorte Fehler, weil der Aufrufer
/// an der falschen Stelle sucht.
/// </para>
///
/// <para>
/// Als Quelltextregel geprüft, nicht über einen Aufruf: Ein Verhaltenstest belegt, dass EINE
/// Gruppe registriert ist. Diese Regel muss für jede gelten, die morgen jemand hinzufügt.
/// Wer wirklich eine Fläche abschalten will, muss dann auch die Navigation mit abschalten —
/// und dieser Test ist die Stelle, an der er darüber stolpert.
/// </para>
/// </remarks>
public sealed class EveryOperatorEndpointGroupIsMappedTests
{
    [Fact]
    public void NoEndpointGroupIsRegisteredBehindACondition()
    {
        var source = File.ReadAllText(
            Path.Combine(RepositoryRoot(), "src", "Administration", "CalloraAdministrationExtensions.cs"));

        var body = MapCalloraAdministrationBody(source);
        var conditional = ConditionalBlocksIn(body)
            .SelectMany(block => Regex.Matches(block, @"app\.Map(\w+Endpoints)\(").Select(match => match.Groups[1].Value))
            .ToArray();

        Assert.True(
            conditional.Length == 0,
            $"""
             Diese Endpunkt-Gruppen werden nur bedingt registriert: {string.Join(", ", conditional)}.
             Ein Client, der sie kennt, bekommt dann 404 statt 403 — und sucht den Fehler bei sich.
             """);
    }

    /// <summary>Der Rumpf von <c>MapCalloraAdministration</c>, bis zur nächsten Methode.</summary>
    private static string MapCalloraAdministrationBody(string source)
    {
        var start = source.IndexOf("MapCalloraAdministration(this WebApplication app)", StringComparison.Ordinal);
        Assert.True(start > 0, "MapCalloraAdministration nicht gefunden — der Test prüft nichts mehr.");

        var end = source.IndexOf("return app;", start, StringComparison.Ordinal);
        Assert.True(end > start, "Kein Ende von MapCalloraAdministration gefunden.");

        return source[start..end];
    }

    /// <summary>Was zwischen einem <c>if (…) {</c> und seiner schließenden Klammer steht.</summary>
    private static IEnumerable<string> ConditionalBlocksIn(string body)
    {
        foreach (Match match in Regex.Matches(body, @"\bif\s*\([^)]*\)\s*\{"))
        {
            var depth = 1;
            var index = match.Index + match.Length;
            var start = index;
            while (index < body.Length && depth > 0)
            {
                depth += body[index] switch { '{' => 1, '}' => -1, _ => 0 };
                index++;
            }

            yield return body[start..(index - 1)];
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Callora.Host.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}

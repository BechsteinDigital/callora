using System.Text.RegularExpressions;
using Callora.Core.Tests.Cli;
using Xunit;

namespace Callora.Core.Tests.Architecture;

/// <summary>
/// Kein Projekt der Plattform darf auf eine Plugin-<em>Implementierung</em> zeigen. Auf einen
/// Plugin-<em>Vertrag</em> darf es.
/// </summary>
/// <remarks>
/// Diese Regel entscheidet, ob das öffentliche Repository nach dem Repo-Schnitt noch baut.
/// Die Plugins ziehen in eigene, private Repositories; ihre Verträge werden als öffentliche
/// Pakete ausgeliefert. Zeigt bis dahin irgendein Projekt unter <c>src/</c> auf eine
/// Implementierung, hängt die öffentliche Plattform an einem privaten Repository — und das
/// merkt man erst, wenn der Klon eines Außenstehenden nicht restauriert.
///
/// <para>
/// Die bestehenden Kanten sind Verträge und bleiben erlaubt: Die Distribution muss
/// <c>Callora.Plugin.Communication.Abstractions</c> in den Default-Ladekontext bringen, damit
/// Host und Plugin dieselbe <c>ICommunicationChannelRegistry</c>-Typidentität teilen
/// (REV2 §10.1A) — und der CLI-Inspektionskontext gibt bei unbekannten Assemblies
/// <c>null</c> zurück, fällt also ebenfalls auf den Default-Kontext zurück.
/// </para>
///
/// <para>
/// Geprüft wird über die Projektdateien, nicht über kompilierte Assemblies: Die Kante soll
/// auffallen, sobald jemand sie schreibt, nicht erst, wenn sie etwas kaputt macht.
/// </para>
/// </remarks>
public sealed class PlatformDependsOnPluginContractsOnlyTests
{
    private static readonly Regex ProjectReferenceRegex = new(
        @"<ProjectReference\s+Include=""([^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [Fact]
    public void NoPlatformProjectReferencesAPluginImplementation()
    {
        var root = ScaffoldedPluginFixture.ResolveRepositoryRoot();
        var offenders = new List<string>();

        foreach (var projectFile in Directory.EnumerateFiles(
                     Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories))
        {
            foreach (Match match in ProjectReferenceRegex.Matches(File.ReadAllText(projectFile)))
            {
                var referenced = match.Groups[1].Value.Replace('\\', '/');
                if (!referenced.Contains("custom/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Ein Vertragsprojekt heißt so. Alles andere unter custom/ ist Implementierung.
                if (Path.GetFileNameWithoutExtension(referenced)
                    .EndsWith(".Abstractions", StringComparison.Ordinal))
                {
                    continue;
                }

                offenders.Add($"{Path.GetRelativePath(root, projectFile)} → {referenced}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Plattform-Projekte dürfen nur Plugin-VERTRÄGE referenzieren, keine Implementierungen. "
            + "Nach dem Repo-Schnitt hinge das öffentliche Repository sonst an einem privaten:"
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }
}

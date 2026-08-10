using System.Text.RegularExpressions;
using Callora.Core.Tests.Cli;
using Xunit;

namespace Callora.Core.Tests.Architecture;

/// <summary>
/// Kein Projekt der Plattform darf auf irgendetwas unter <c>custom/</c> zeigen — auch nicht
/// auf einen Plugin-Vertrag.
/// </summary>
/// <remarks>
/// Diese Regel entscheidet, ob das öffentliche Repository noch baut. Zeigt ein Projekt unter
/// <c>src/</c> auf ein Plugin, hängt die öffentliche Plattform an einem privaten Repository —
/// und das merkt man erst, wenn der Klon eines Außenstehenden nicht restauriert.
///
/// <para>
/// Die Regel war einmal weicher: Implementierungen verboten, <c>*.Abstractions</c> erlaubt.
/// Das war richtig, solange die Plugins hier lagen und die Distribution ihren Vertrag in den
/// Default-Ladekontext bringen musste (REV2 §10.1A). Seit die Plugins in eigenen Repositories
/// leben (ADR-020), gibt es unter <c>custom/</c> nichts mehr zu referenzieren, und die
/// Ausnahme wäre nur noch ein offenes Tor: Sie erlaubte eine Kante, die niemand mehr braucht.
/// WELCHE Vertragspakete vorgeladen werden, entscheidet ohnehin die Distribution
/// (<c>callora-production</c>), nicht das Framework.
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
    public void NoPlatformProjectReferencesAnythingUnderCustom()
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

                offenders.Add($"{Path.GetRelativePath(root, projectFile)} → {referenced}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Plattform-Projekte dürfen nichts unter custom/ referenzieren — auch keine Verträge. "
            + "Das öffentliche Repository hinge sonst an einem privaten. Wird ein Vertragspaket "
            + "gebraucht, kommt es als NuGet-Referenz, und die Entscheidung darüber gehört in die "
            + "Distribution (callora-production):"
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }
}

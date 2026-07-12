using System.Text.RegularExpressions;
using Xunit;

namespace Callora.Host.Backend.Tests.Architecture;

/// <summary>
/// Erzwingt die Repo-Regeln aus ENGINEERING_RULES.md und CODE_STRUCTURE_RULES.md:
/// keine partial classes, keine verschachtelten Typen, ein Typ pro Datei,
/// Zeilen-Cap und DDD-Schichtung. Baselines dürfen nur schrumpfen.
/// </summary>
public sealed class ArchitectureRulesTests
{
    private const int MaxLinesPerFile = 1000;

    private static readonly Regex TopLevelTypeRegex = new(
        @"^(public|internal)\s+(sealed\s+|static\s+|abstract\s+|readonly\s+|partial\s+)*(class|interface|enum|record)\b",
        RegexOptions.Compiled);

    private static readonly Regex NestedTypeRegex = new(
        @"^\s{4,}(public|internal|private|protected)\s+(sealed\s+|static\s+|abstract\s+|readonly\s+|partial\s+)*(class|interface|enum)\s+\w",
        RegexOptions.Compiled);

    private static readonly Regex PartialTypeRegex = new(
        @"\bpartial\s+(class|interface|record|struct)\b",
        RegexOptions.Compiled);

    /// <summary>Dateien mit erlaubten Mehrfachtyp-Treffern (nur Schrumpfen erlaubt).</summary>
    private static readonly HashSet<string> MultiTypeBaseline = new(StringComparer.OrdinalIgnoreCase)
    {
        // Enthält C#-Codevorlagen als String-Literale (falsch-positive Typtreffer).
        NormalizePath("src/Host/Cli/Application/PluginScaffolder.cs")
    };

    /// <summary>Domain-Dateien mit erlaubter Application-Referenz (nur Schrumpfen erlaubt).</summary>
    private static readonly HashSet<string> DomainLayerBaseline = new(StringComparer.OrdinalIgnoreCase)
    {
        // Öffentlicher Plugin-Einstiegsvertrag benötigt seinen Startkontext.
        NormalizePath("src/Host/PluginContracts/Domain/Plugins/IHostManagedPlugin.cs")
    };

    [Fact]
    public void ProductionCode_ContainsNoPartialTypes_OutsideGeneratedMigrations()
    {
        var violations = EnumerateProductionSourceFiles()
            .Where(file => !file.RelativePath.Contains("Migrations", StringComparison.OrdinalIgnoreCase))
            .Where(file => file.Lines.Any(line => PartialTypeRegex.IsMatch(line)))
            .Select(file => file.RelativePath)
            .ToArray();

        Assert.True(violations.Length == 0, "Partial types gefunden:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void ProductionCode_StaysBelowLineCap()
    {
        var violations = EnumerateProductionSourceFiles()
            .Where(file => file.Lines.Length > MaxLinesPerFile)
            .Select(file => $"{file.RelativePath} ({file.Lines.Length} Zeilen)")
            .ToArray();

        Assert.True(violations.Length == 0, $"Dateien über {MaxLinesPerFile} Zeilen:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void ProductionCode_DeclaresOneTopLevelTypePerFile()
    {
        var violations = EnumerateProductionSourceFiles()
            .Where(file => !MultiTypeBaseline.Contains(file.RelativePath))
            .Where(file => file.Lines.Count(line => TopLevelTypeRegex.IsMatch(line)) > 1)
            .Select(file => file.RelativePath)
            .ToArray();

        Assert.True(violations.Length == 0, "Mehrere Top-Level-Typen pro Datei:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void ProductionCode_ContainsNoNestedTypeDeclarations()
    {
        var violations = EnumerateProductionSourceFiles()
            .Where(file => !file.RelativePath.Contains("Migrations", StringComparison.OrdinalIgnoreCase))
            .Where(file => !MultiTypeBaseline.Contains(file.RelativePath))
            .Where(file => file.Lines.Any(line => NestedTypeRegex.IsMatch(line)))
            .Select(file => file.RelativePath)
            .ToArray();

        Assert.True(violations.Length == 0, "Verschachtelte Typen gefunden:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void DomainLayer_DoesNotDependOnApplicationOrInfrastructure()
    {
        var violations = EnumerateProductionSourceFiles()
            .Where(file => file.RelativePath.Contains($"{Path.DirectorySeparatorChar}Domain{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => !DomainLayerBaseline.Contains(file.RelativePath))
            .Where(file => file.Lines.Any(static line =>
                line.StartsWith("using ", StringComparison.Ordinal) &&
                (line.Contains(".Application", StringComparison.Ordinal) ||
                 line.Contains(".Infrastructure", StringComparison.Ordinal))))
            .Select(file => file.RelativePath)
            .ToArray();

        Assert.True(violations.Length == 0, "Domain-Schicht referenziert äußere Schichten:\n" + string.Join('\n', violations));
    }

    private static IEnumerable<(string RelativePath, string[] Lines)> EnumerateProductionSourceFiles()
    {
        var repoRoot = FindRepoRoot();
        var roots = new[] { Path.Combine(repoRoot, "src"), Path.Combine(repoRoot, "custom") };

        foreach (var root in roots.Where(Directory.Exists))
        {
            foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                    path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return (Path.GetRelativePath(repoRoot, path), File.ReadAllLines(path));
            }
        }
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Callora.Host.sln")))
                return current.FullName;

            current = current.Parent!;
        }

        throw new InvalidOperationException("Repo-Wurzel mit Callora.Host.sln nicht gefunden.");
    }

    private static string NormalizePath(string relativePath) =>
        relativePath.Replace('/', Path.DirectorySeparatorChar);
}

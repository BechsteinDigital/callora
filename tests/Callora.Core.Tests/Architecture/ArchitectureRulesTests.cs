using System.Text.RegularExpressions;
using Xunit;

namespace Callora.Core.Tests.Architecture;

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
        @"^\s{4,}(public|internal|private|protected)\s+(sealed\s+|static\s+|abstract\s+|readonly\s+|partial\s+)*(class|interface|enum|record)\s+\w",
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

    /// <summary>
    /// Dateien, die noch Minimal-API-Endpunkte mappen, statt Controller zu verwenden
    /// (CODE_STRUCTURE_RULES.md, Api-Konvention). Bestandsaufnahme, kein Freibrief: neue Einträge
    /// gehören nicht hinzugefügt, und wer eine Datei migriert, streicht sie hier.
    /// <para>
    /// Warum eine Liste statt einer Migration: 35 Dateien gegen 2 Controller ist kein Refactoring,
    /// das nebenher läuft. Eingefroren schützt sie ab sofort vor neuen Verstößen — ungeschrieben
    /// schützt sie vor gar nichts.
    /// </para>
    /// </summary>
    private static readonly HashSet<string> MinimalApiBaseline = new(StringComparer.OrdinalIgnoreCase)
    {
        NormalizePath("src/Administration/Api/AdminContextEndpoints.cs"),
        NormalizePath("src/Administration/Api/BusinessEventEndpoints.cs"),
        NormalizePath("src/Administration/Api/ContractCatalogEndpoints.cs"),
        NormalizePath("src/Administration/Api/CustomFieldEndpoints.cs"),
        NormalizePath("src/Administration/Api/EntitlementManagementEndpoints.cs"),
        NormalizePath("src/Administration/Api/EntitlementSyncEndpoints.cs"),
        NormalizePath("src/Administration/Api/FeatureEndpoints.cs"),
        NormalizePath("src/Administration/Api/FlowEndpoints.cs"),
        NormalizePath("src/Administration/Api/JobEndpoints.cs"),
        NormalizePath("src/Administration/Api/MediaEndpoints.cs"),
        NormalizePath("src/Administration/Api/NotificationEndpoints.cs"),
        NormalizePath("src/Administration/Api/PluginAdminExtensionEndpoints.cs"),
        NormalizePath("src/Administration/Api/PluginAssetEndpoints.cs"),
        NormalizePath("src/Administration/Api/PluginEndpoints.cs"),
        NormalizePath("src/Administration/Api/PluginPublicHttpEndpoints.cs"),
        NormalizePath("src/Administration/Api/PluginSurfaceApiEndpoints.cs"),
        NormalizePath("src/Administration/Api/PluginWebSocketEndpoints.cs"),
        NormalizePath("src/Administration/Api/RbacEndpoints.cs"),
        NormalizePath("src/Administration/Api/SurfaceEndpoints.cs"),
        NormalizePath("src/Administration/Api/SurfaceIdentityEndpoints.cs"),
        NormalizePath("src/Administration/Api/SurfaceThemeEndpoints.cs"),
        NormalizePath("src/Administration/Api/SystemConfigEndpoints.cs"),
        NormalizePath("src/Administration/Api/TenantEndpoints.cs"),
        NormalizePath("src/Administration/Api/ThemeEndpoints.cs"),
        NormalizePath("src/Administration/Api/UserEndpoints.cs"),
        NormalizePath("src/Administration/Api/WebhookEndpoints.cs"),
        NormalizePath("src/Administration/Api/WorkspaceEndpoints.cs"),
        NormalizePath("src/Administration/CalloraAdministrationExtensions.cs"),
        NormalizePath("src/Core/Api/AuthEndpoints.cs"),
        NormalizePath("src/Core/Infrastructure/DependencyInjection/CalloraHostCompositionExtensions.cs"),
        NormalizePath("src/Core/Infrastructure/DependencyInjection/CalloraMcpCompositionExtensions.cs"),
        NormalizePath("src/Surface.Rendering/Api/SurfaceHandoffEndpoints.cs"),
        NormalizePath("src/Surface.Rendering/Api/SurfaceRenderEndpoints.cs"),
        NormalizePath("src/Workspace/Api/WorkspacePublicEndpoints.cs"),
        NormalizePath("src/Workspace/Api/WorkspaceThemeEndpoints.cs"),
    };

    /// <summary>Domain-Dateien mit erlaubter Application-Referenz (nur Schrumpfen erlaubt).</summary>
    private static readonly HashSet<string> DomainLayerBaseline = new(StringComparer.OrdinalIgnoreCase)
    {
        // Öffentlicher Plugin-Einstiegsvertrag benötigt seinen Startkontext.
        NormalizePath("src/Core/Domain/Plugins/Contracts/IHostManagedPlugin.cs")
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
            // Generierte EF-Migrations-Artefakte wachsen mit dem Schema und
            // unterliegen wie bei den übrigen Regeln nicht dem Cap.
            .Where(file => !file.RelativePath.Contains("Migrations", StringComparison.OrdinalIgnoreCase))
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

    [Fact]
    public void MinimalApiEndpoints_StayWithinTheMigrationBaseline()
    {
        var violations = EnumerateProductionSourceFiles()
            .Where(file => !MinimalApiBaseline.Contains(file.RelativePath))
            .Where(file => file.Lines.Any(static line =>
                line.Contains("IEndpointRouteBuilder", StringComparison.Ordinal)))
            .Select(file => file.RelativePath)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Neue Minimal-API-Endpunkte außerhalb der Migrations-Baseline (Regel: Controller, keine "
            + "Minimal-API):\n" + string.Join('\n', violations));
    }

    [Fact]
    public void TheMinimalApiBaseline_OnlyShrinks()
    {
        // Ein Eintrag, der nichts mehr abdeckt, ist entweder eine gelöschte Datei oder eine bereits
        // migrierte. Beides gehört gestrichen — sonst wächst die Liste still weiter, weil niemand
        // merkt, dass ein Platz frei geworden ist.
        var actual = EnumerateProductionSourceFiles()
            .Where(file => file.Lines.Any(static line =>
                line.Contains("IEndpointRouteBuilder", StringComparison.Ordinal)))
            .Select(file => file.RelativePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var stale = MinimalApiBaseline.Where(entry => !actual.Contains(entry)).ToArray();

        Assert.True(
            stale.Length == 0,
            "Baseline-Einträge ohne Verstoß — bitte streichen:\n" + string.Join('\n', stale));
    }

    [Fact]
    public void ApplicationLayer_DoesNotDependOnAspNet()
    {
        // Die Schichtregel, die am leisesten bricht: Application definiert Ports und kennt keine
        // konkrete Technik. Ein using auf ASP.NET dort ist der erste Schritt dahin, dass ein
        // Handler nur noch über HTTP testbar ist.
        var violations = EnumerateProductionSourceFiles()
            .Where(file => file.RelativePath.Contains(
                $"{Path.DirectorySeparatorChar}Application{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => file.Lines.Any(static line =>
                line.StartsWith("using ", StringComparison.Ordinal) &&
                line.Contains("Microsoft.AspNetCore", StringComparison.Ordinal)))
            .Select(file => file.RelativePath)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Application-Schicht referenziert ASP.NET:\n" + string.Join('\n', violations));
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
                    path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
                    // Archivierter Legacy-Code (verschoben, nicht gebaut/ausgeliefert) unterliegt
                    // nicht den aktiven Produktions-Architekturregeln.
                    path.Contains($"{Path.DirectorySeparatorChar}_archive{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
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
            {
                return current.FullName;
            }

            current = current.Parent!;
        }

        throw new InvalidOperationException("Repo-Wurzel mit Callora.Host.sln nicht gefunden.");
    }

    private static string NormalizePath(string relativePath) =>
        relativePath.Replace('/', Path.DirectorySeparatorChar);
}

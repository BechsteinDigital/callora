using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// Wer eine Fläche aus einer öffentlichen Route auflöst, prüft auch ihre Sichtbarkeit.
/// <para>
/// <b>Warum als Quelltext-Regel:</b> Die Lücke entstand nicht dadurch, dass jemand die Prüfung
/// falsch schrieb, sondern dadurch, dass ein ZWEITER Einstieg dazukam und sie gar nicht hatte.
/// Der Renderpfad prüfte <c>RequiredClaims</c> von Anfang an; der Kontext-Socket löste dieselbe
/// Fläche auf und kannte nur den Access Mode. Wem die Seite mit 404 antwortete, der bekam dort
/// ein Abo auf denselben Knoten.
/// </para>
/// <para>
/// Ein Verhaltenstest belegt EINEN Pfad. Diese Regel gilt für den dritten Seam, den jemand
/// morgen hinzufügt — und genau dann, wenn niemand mehr an ADR-019 §4 denkt, ist sie es wert.
/// </para>
/// </summary>
public sealed class EverySurfaceSeamChecksVisibilityTests
{
    [Fact]
    public void EverySeamThatResolvesAPublicSurfaceAlsoChecksItsVisibility()
    {
        var offenders = SurfaceRenderingSources()
            .Where(file => file.Text.Contains("ResolveSurfaceByPublicRouteAsync", StringComparison.Ordinal))
            .Where(file => !file.Text.Contains("SurfaceVisibility.", StringComparison.Ordinal))
            .Select(file => file.Path)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Diese Einstiege lösen eine Fläche aus einer öffentlichen Route auf, ohne ihre "
            + "RequiredClaims auszuwerten — der Access Mode allein deckt das nicht ab "
            + "(SurfaceVisibility.IsReachableBy):\n" + string.Join('\n', offenders));
    }

    private static IEnumerable<(string Path, string Text)> SurfaceRenderingSources()
    {
        var root = RepositoryRoot();
        var directory = Path.Combine(root, "src", "Surface.Rendering");

        return Directory
            .EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(path => (Path.GetRelativePath(root, path), File.ReadAllText(path)));
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

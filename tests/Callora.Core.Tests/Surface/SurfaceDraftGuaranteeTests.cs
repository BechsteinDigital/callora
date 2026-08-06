using System.Text.RegularExpressions;
using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// Regel 4 des Composer-Designs: <b>Der öffentliche Renderpfad ruft niemals GetDraftAsync.</b>
/// <para>
/// Als Quelltext-Regel geprüft, nicht über einen Aufruf. Ein Verhaltenstest belegt, dass EIN Pfad
/// den Entwurf nicht holt; diese Regel muss für jeden gelten, den jemand morgen hinzufügt — und
/// genau dann, wenn niemand mehr daran denkt, ist sie es wert, erzwungen zu werden.
/// </para>
/// <para>
/// Auf einer Public-Surface säße ein Entwurfs-Leck hinter gar keiner Authentifizierung: Wer die
/// Adresse kennt, läse, was noch niemand veröffentlichen wollte.
/// </para>
/// </summary>
public sealed class SurfaceDraftGuaranteeTests
{
    [Fact]
    public void ThePublicRenderPathNeverAsksForADraft()
    {
        var offenders = PublicSurfaceSources()
            .Where(file => file.Text.Contains("GetDraftAsync", StringComparison.Ordinal))
            .Select(file => file.Path)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Der öffentliche Renderpfad darf GetDraftAsync nicht aufrufen — ein Entwurf wäre damit "
            + "von außen anforderbar:\n" + string.Join('\n', offenders));
    }

    [Fact]
    public void NoRenderPathReadsAPreviewFlagFromTheRequest()
    {
        // Es gibt kein ?preview=true und keinen Header. Der Editor baut seinen Canvas aus dem
        // Entwurfs-Dokument, das er über die Admin-API geholt hat, und ruft /surface/render nie
        // auf — genau deshalb braucht dieser Pfad keinen Schalter.
        //
        // Kommentare zählen nicht: die Regel steht in mehreren davon erklärt, und ein Test, der
        // seine eigene Begründung als Verstoß liest, wäre nicht zu halten.
        var word = new Regex(@"\bpreview\b", RegexOptions.IgnoreCase);

        var offenders = PublicSurfaceSources()
            .Where(file => Code(file.Text).Any(line => word.IsMatch(line)))
            .Select(file => file.Path)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Kein Vorschau-Schalter im öffentlichen Renderpfad:\n" + string.Join('\n', offenders));
    }

    /// <summary>Zeilen ohne Kommentare — Zeilenkommentare, Doc-Kommentare und Blockrümpfe.</summary>
    private static IEnumerable<string> Code(string text) =>
        text.Split('\n')
            .Select(line => line.TrimStart())
            .Where(line => !line.StartsWith("//", StringComparison.Ordinal))
            .Where(line => !line.StartsWith("*", StringComparison.Ordinal))
            .Where(line => !line.StartsWith("/*", StringComparison.Ordinal));

    private static IEnumerable<(string Path, string Text)> PublicSurfaceSources()
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

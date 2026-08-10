using System.Text.RegularExpressions;
using Callora.Core.Tests.Cli;
using Xunit;

namespace Callora.Core.Tests.Documentation;

/// <summary>
/// Jeder relative Link in der Dokumentation muss auf eine Datei zeigen, die es gibt.
/// </summary>
/// <remarks>
/// <para>
/// Das README verwies prominent auf <c>CONTRIBUTING.md</c> — „was du vorher wissen solltest" —
/// und beschrieb sogar deren Inhalt. Die Datei war nie im Repository: Die <c>*.md</c>-Regel in
/// <c>.gitignore</c> hatte sie verschluckt, <c>git add</c> meldete nichts, und der Commit sah
/// sauber aus. Für einen Außenstehenden war der wichtigste Link im README tot.
/// </para>
/// <para>
/// Genau diese Fehlerklasse ist unsichtbar: Sie entsteht beim Anlegen, nicht beim Ändern, und
/// niemand klickt die eigenen Links. Ein Test tut es bei jedem Lauf.
/// </para>
/// <para>
/// Geprüft werden nur relative Ziele. Externe URLs sind nicht Sache eines Testlaufs — er hätte
/// dafür Netzzugang zu brauchen, und ein fremder Server, der gerade langsam ist, darf keinen
/// Build rot machen.
/// </para>
/// </remarks>
public sealed class DocumentedLinkTargetsTests
{
    private static readonly Regex MarkdownLinkRegex = new(
        @"\[[^\]]*\]\(([^)]+)\)",
        RegexOptions.Compiled);

    private static readonly string[] Roots = ["docs-site", "docs", "ops"];

    [Fact]
    public void EveryRelativeLinkResolvesToAFileInTheRepository()
    {
        var root = ScaffoldedPluginFixture.ResolveRepositoryRoot();
        var broken = new List<string>();

        foreach (var document in EnumerateMarkdown(root))
        {
            var directory = Path.GetDirectoryName(document)!;

            foreach (Match match in MarkdownLinkRegex.Matches(File.ReadAllText(document)))
            {
                var target = match.Groups[1].Value.Trim();

                // Externe Ziele, Anker innerhalb der Seite und Mail-Links prüft dieser Test nicht.
                if (target.Length == 0
                    || target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    || target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                    || target.StartsWith('#'))
                {
                    continue;
                }

                // Ein Anker hinter dem Pfad (`datei.md#abschnitt`) betrifft die Stelle in der
                // Datei, nicht ihre Existenz.
                var path = target.Split('#')[0].Split('?')[0];
                if (path.Length == 0)
                {
                    continue;
                }

                // `/api/` erzeugt DocFX beim Doku-Build aus dem Quellcode; im Quellbaum gibt
                // es das Verzeichnis nicht (docfx/api/*.yml ist bewusst nicht versioniert).
                // Ein Link dorthin ist also richtig, obwohl er hier ins Leere zeigt.
                if (path.StartsWith("/api/", StringComparison.Ordinal))
                {
                    continue;
                }

                // VitePress löst einen führenden Schrägstrich gegen die Doku-Wurzel auf,
                // nicht gegen das Dateisystem.
                var resolved = path.StartsWith('/')
                    ? Path.Combine(root, "docs-site", path.TrimStart('/'))
                    : Path.Combine(directory, path);

                if (File.Exists(resolved) || Directory.Exists(resolved))
                {
                    continue;
                }

                // VitePress darf `../guide` schreiben und `../guide.md` meinen; ein
                // Verzeichnislink meint dessen index.md.
                if (File.Exists(resolved + ".md") || File.Exists(Path.Combine(resolved, "index.md")))
                {
                    continue;
                }

                broken.Add($"  {Path.GetRelativePath(root, document)} → {target}");
            }
        }

        Assert.True(
            broken.Count == 0,
            "Die Dokumentation verweist auf Dateien, die es nicht gibt:"
            + Environment.NewLine + string.Join(Environment.NewLine, broken));
    }

    private static IEnumerable<string> EnumerateMarkdown(string root)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*.md", SearchOption.TopDirectoryOnly))
        {
            yield return file;
        }

        foreach (var directory in Roots)
        {
            var full = Path.Combine(root, directory);
            if (!Directory.Exists(full))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(full, "*.md", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                yield return file;
            }
        }
    }
}

using System.Text.Json;
using System.Text.RegularExpressions;
using Callora.Core.Tests.Cli;
using Xunit;

namespace Callora.Core.Tests.Documentation;

/// <summary>
/// An entry type the documentation quotes for a plugin this repository ships has to be the one that
/// plugin's manifest declares.
/// </summary>
/// <remarks>
/// A wrong entry type is a specific kind of unhelpful: it looks authoritative, it is exactly what
/// someone copies when writing their own manifest, and it fails at install time with an error that
/// points at their plugin rather than at the documentation they copied.
///
/// The check is scoped to each shipped plugin's assembly root rather than to one hard-coded plugin.
/// The earlier version demanded that the README name Communication's entry type — a claim the README
/// stopped making when it was rewritten, and nothing noticed, because CI ignores markdown-only
/// changes. Scoping by assembly root also means the test goes quiet on its own once plugins move to
/// their own repositories: a claim about a plugin that is no longer here is not this repository's to
/// make.
/// </remarks>
public sealed class PluginEntryTypeDocumentationTests
{
    [Fact]
    public void DocumentationQuotesNoStaleEntryTypeForAPluginThisRepositoryShips()
    {
        var root = ScaffoldedPluginFixture.ResolveRepositoryRoot();
        var pages = DocumentationPages(root).ToArray();

        foreach (var manifestPath in ShippedManifests(root))
        {
            var declared = JsonDocument
                .Parse(File.ReadAllText(manifestPath))
                .RootElement.GetProperty("entryTypeName").GetString();

            Assert.False(string.IsNullOrWhiteSpace(declared), $"{manifestPath} declares no entryTypeName.");

            // Nur Typnamen, nicht jedes Vorkommen des Namensraums: sonst schlägt der Test auf
            // "Callora.Plugin.Communication.csproj" an. Ein Entry-Type endet konventionsgemäß
            // auf "Plugin", und genau darum geht es hier.
            var assemblyRoot = declared![..declared.LastIndexOf('.')];
            var quoted = new Regex(
                Regex.Escape(assemblyRoot) + @"\.[A-Za-z0-9_]*Plugin\b",
                RegexOptions.None,
                TimeSpan.FromSeconds(5));

            foreach (var page in pages)
            {
                foreach (Match match in quoted.Matches(File.ReadAllText(page)))
                {
                    Assert.True(
                        match.Value == declared,
                        $"{Path.GetRelativePath(root, page)} quotes '{match.Value}', but "
                        + $"{Path.GetRelativePath(root, manifestPath)} declares '{declared}'.");
                }
            }
        }
    }

    // bin/obj hold build copies of the same manifests; reading those would assert the same claim
    // several times and, worse, keep asserting it after the source manifest is gone.
    private static IEnumerable<string> ShippedManifests(string root) =>
        Directory
            .EnumerateFiles(Path.Combine(root, "custom"), "registry.json", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static IEnumerable<string> DocumentationPages(string root) =>
        Directory
            .EnumerateFiles(Path.Combine(root, "docs-site"), "*.md", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Append(Path.Combine(root, "README.md"));
}

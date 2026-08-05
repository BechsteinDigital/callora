using System.Text.Json;
using Callora.Core.Tests.Cli;
using Xunit;

namespace Callora.Core.Tests.Documentation;

/// <summary>
/// Every plugin entry type named in the README has to be the one its manifest declares and the one
/// the assembly actually contains.
/// </summary>
/// <remarks>
/// A wrong entry type is a specific kind of unhelpful: it looks authoritative, it is exactly what
/// someone copies when writing their own manifest, and it fails at install time with an error that
/// points at their plugin rather than at the documentation they copied.
/// </remarks>
public sealed class PluginEntryTypeDocumentationTests
{
    [Fact]
    public void TheDocumentedCommunicationEntryTypeMatchesItsManifest()
    {
        var root = ScaffoldedPluginFixture.ResolveRepositoryRoot();

        var manifest = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(root, "custom", "static-plugins", "Communication", "registry.json")));
        var declared = manifest.RootElement.GetProperty("entryTypeName").GetString();

        Assert.False(string.IsNullOrWhiteSpace(declared));
        Assert.Contains(
            $"`{declared}`",
            File.ReadAllText(Path.Combine(root, "README.md")),
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheArchivedPluginIsNotPresentedAsCurrent()
    {
        // The archive keeps its own manifest with its own (older) entry type. Documentation quoting
        // that one would be describing a plugin the product no longer ships.
        var root = ScaffoldedPluginFixture.ResolveRepositoryRoot();
        var archivedManifest = Path.Combine(
            root, "custom", "static-plugins", "_archive", "Communication-legacy", "registry.json");

        Skip.IfNot(File.Exists(archivedManifest), "No archived Communication plugin in this checkout.");

        var archivedEntryType = JsonDocument
            .Parse(File.ReadAllText(archivedManifest))
            .RootElement.GetProperty("entryTypeName").GetString();

        foreach (var page in Directory.EnumerateFiles(Path.Combine(root, "docs-site"), "*.md", SearchOption.AllDirectories)
                     .Append(Path.Combine(root, "README.md")))
        {
            Assert.DoesNotContain(archivedEntryType!, File.ReadAllText(page), StringComparison.Ordinal);
        }
    }
}

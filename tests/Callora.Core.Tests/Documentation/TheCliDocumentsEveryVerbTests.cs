using Callora.Core.Tests.Cli;
using Xunit;

namespace Callora.Core.Tests.Documentation;

/// <summary>
/// Every verb the CLI accepts appears in its reference page.
/// </summary>
/// <remarks>
/// A verb nobody can discover is a verb nobody uses, and <c>plugin inspect</c> exists
/// precisely for the person who does not yet know the package — the one least likely to
/// guess that the command is there.
/// </remarks>
public sealed class TheCliDocumentsEveryVerbTests
{
    private static string Reference() => File.ReadAllText(Path.Combine(
        ScaffoldedPluginFixture.ResolveRepositoryRoot(), "docs-site", "reference", "cli.md"));

    [Theory]
    [InlineData("plugin new")]
    [InlineData("plugin test-contract")]
    [InlineData("plugin inspect")]
    [InlineData("plugin sign")]
    public void EveryVerbIsInTheReference(string verb)
    {
        Assert.Contains(verb, Reference(), StringComparison.Ordinal);
    }

    [Fact]
    public void InspectStatesWhyItLoadsTheAssembly()
    {
        // The distinction a reader needs: the manifest half is parsed, the attachment half
        // is read from compiled types. Without it, "why does this need the dll" is a
        // question with no answer on the page.
        Assert.Contains("compiled types", Reference(), StringComparison.OrdinalIgnoreCase);
    }
}

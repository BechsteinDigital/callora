using Callora.Core.Application.Security;
using Callora.Core.Tests.Cli;
using Xunit;

namespace Callora.Core.Tests.Documentation;

/// <summary>
/// Keeps the declaration boundary documented where a plugin author and an operator each look.
/// </summary>
/// <remarks>
/// The namespace rule is a security boundary that only works if both sides know it exists: an
/// author who does not will pick a key outside it and be unable to install; an operator who
/// does not has no reason to doubt a key that looks like the plugin's own.
/// </remarks>
public sealed class TheNamespaceRuleIsDocumentedTests
{
    private static string Read(params string[] segments) =>
        File.ReadAllText(Path.Combine([ScaffoldedPluginFixture.ResolveRepositoryRoot(), .. segments]));

    [Fact]
    public void TheManifestGuideStatesBothRules()
    {
        var guide = Read("docs-site", "guides", "fundamentals", "registry-manifest.md");

        Assert.Contains("own namespace", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("known action", guide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PLUGIN_PERMISSION_NOT_DECLARABLE", guide, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryAcceptedActionIsNamedInTheGuide()
    {
        var guide = Read("docs-site", "guides", "fundamentals", "registry-manifest.md");

        foreach (var action in BackendPermissionActions.All)
        {
            Assert.Contains(action, guide, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ThePermissionReferenceWarnsTheOperator()
    {
        // The operator side of the same rule: they are the one who would grant a key that
        // looks like the plugin's own.
        var reference = Read("docs-site", "reference", "permissions.md");

        Assert.Contains("own namespace", reference, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("user.delete", reference, StringComparison.Ordinal);
    }
}

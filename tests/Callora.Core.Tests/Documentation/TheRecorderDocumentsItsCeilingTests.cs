using Callora.Core.Application.Diagnostics;
using Callora.Core.Application.Security;
using Callora.Core.Tests.Cli;
using Xunit;

namespace Callora.Core.Tests.Documentation;

/// <summary>
/// Keeps the recorder's operating page tied to the recorder.
/// </summary>
/// <remarks>
/// The self-disabling ceiling is the reason an operator agrees to switch this on at all. A
/// page that states the wrong number, or drops the promise entirely, turns a bounded tool
/// back into one nobody dares enable — or worse, one they enable believing it stops when it
/// does not.
/// </remarks>
public sealed class TheRecorderDocumentsItsCeilingTests
{
    private static readonly string PagePath = Path.Combine(
        ScaffoldedPluginFixture.ResolveRepositoryRoot(), "docs-site", "maintainer", "recording-a-request.md");

    [Fact]
    public void TheStatedCeilingIsTheRealOne()
    {
        var page = File.ReadAllText(PagePath);

        Assert.Contains(
            $"{PluginExecutionRecorder.MaximumWindow.TotalMinutes:0} minutes",
            page,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TheStatedCapacityIsTheRealOne()
    {
        var page = File.ReadAllText(PagePath);

        Assert.Contains($"{PluginExecutionRecorder.Capacity}", page, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePermissionIsNamedOnBothPages()
    {
        Assert.Contains(BackendPermissionKeys.DiagnosticsRecord, File.ReadAllText(PagePath), StringComparison.Ordinal);

        var permissions = File.ReadAllText(Path.Combine(
            ScaffoldedPluginFixture.ResolveRepositoryRoot(), "docs-site", "reference", "permissions.md"));
        Assert.Contains(BackendPermissionKeys.DiagnosticsRecord, permissions, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDisclosureIsSpelledOut()
    {
        // The one thing an operator must know before granting the key: recordings contain
        // SQL text, which no other monitoring endpoint exposes.
        var page = File.ReadAllText(PagePath);

        Assert.Contains("SQL command text", page, StringComparison.Ordinal);
    }
}

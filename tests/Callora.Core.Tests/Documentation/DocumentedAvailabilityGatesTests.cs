using Callora.Core.Application.Plugins;
using Callora.Core.Tests.Cli;
using Xunit;

namespace Callora.Core.Tests.Documentation;

/// <summary>
/// Keeps the availability reference honest about which entry points enforce the gate.
/// </summary>
/// <remarks>
/// <para>
/// The list matters more than most documentation, because its gaps are the interesting part:
/// an operator asking "we revoked this plugin — what is it still allowed to do?" gets the
/// answer from this page or from nowhere. Before it existed, the answer lived only as five
/// separate call sites, and two entry points that should have been on the list were not.
/// </para>
/// <para>
/// The factor names are pinned against the enum rather than the prose, so a factor added to
/// the derivation cannot quietly stay undocumented — that is exactly how a gate grows a hole
/// nobody sees.
/// </para>
/// </remarks>
public sealed class DocumentedAvailabilityGatesTests
{
    private static readonly string ReferencePath = Path.Combine(
        ScaffoldedPluginFixture.ResolveRepositoryRoot(), "docs-site", "reference", "plugin-availability.md");

    [Theory]
    [InlineData("BackgroundJobProcessor")]
    [InlineData("BusinessEventBus")]
    [InlineData("HostApplicationEventDispatcher")]
    [InlineData("PluginApiEndpointDataSource")]
    [InlineData("ContributedMcpTool")]
    public void EveryGatedEntryPointIsListed(string entryPoint)
    {
        Assert.Contains(entryPoint, File.ReadAllText(ReferencePath), StringComparison.Ordinal);
    }

    [Fact]
    public void EveryAvailabilityFactorIsDocumented()
    {
        var reference = File.ReadAllText(ReferencePath);

        foreach (var factor in Enum.GetNames<PluginAvailabilityFactor>())
        {
            Assert.Contains(factor, reference, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheDeliberateGapsAreNamed()
    {
        var reference = File.ReadAllText(ReferencePath);

        // The gaps are the reason the page exists; a rewrite that drops them turns a
        // statement of limits back into a claim of completeness.
        Assert.Contains("does not enforce", reference, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HostAdminApiRouteScope.Global", reference, StringComparison.Ordinal);
    }

    [Fact]
    public void TheParkingContractForJobsIsStated()
    {
        var reference = File.ReadAllText(ReferencePath);

        // "Parked, not failed" is the one behaviour a plugin author would otherwise guess
        // wrong: a job that vanishes from the queue and a job that waits look identical
        // from outside until the entitlement returns.
        Assert.Contains("parked", reference, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UnavailableRetryDelay", reference, StringComparison.Ordinal);
    }
}

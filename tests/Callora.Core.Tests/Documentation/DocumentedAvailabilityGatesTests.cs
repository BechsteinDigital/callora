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
    public void BothQuestionsAreDistinguished()
    {
        var reference = File.ReadAllText(ReferencePath);

        // The page's whole point. A reader who takes the platform verdict for a relaxed
        // workspace verdict will expect workspace activation to matter platform-wide, and
        // be wrong about the one place where the two genuinely differ.
        Assert.Contains("EvaluatePlatformAsync", reference, StringComparison.Ordinal);
        Assert.Contains("precondition", reference, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PluginPlatformInputs", reference, StringComparison.Ordinal);
        Assert.Contains("PluginWorkspaceInputs", reference, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDefaultTenantDecisionIsRecorded()
    {
        var reference = File.ReadAllText(ReferencePath);

        // Asking on the default tenant rather than on no tenant is the one semantic choice
        // in the platform verdict, and it is invisible from the signature. An operator
        // debugging "the grant exists but the plugin is idle" needs this paragraph.
        Assert.Contains("DefaultTenantKey", reference, StringComparison.Ordinal);
        Assert.Contains("MarketplaceEntitlementApplier", reference, StringComparison.Ordinal);
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

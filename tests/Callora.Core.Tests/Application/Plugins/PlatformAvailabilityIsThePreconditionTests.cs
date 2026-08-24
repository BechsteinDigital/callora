using Callora.Core.Application.Plugins;
using Xunit;

namespace Callora.Core.Tests.Application.Plugins;

/// <summary>
/// Availability splits into two layers, and the split is not a convenience: the four
/// platform factors are exactly the ones that must hold in <b>every</b> workspace. A
/// deinstalled, faulted, unentitled or over-budget plugin is available nowhere, so the
/// platform verdict is the <b>precondition</b> of the workspace verdict rather than a
/// weaker version of it.
/// </summary>
/// <remarks>
/// The alternative designs both cost something real. A separate derivation for the
/// platform verdict duplicates the combination that <see cref="PluginAvailability.From"/>
/// documents as canonical — two truths that drift the first time a ninth factor arrives.
/// Making the workspace factors nullable would demote "checked and true" versus "not
/// checked" from a property of the type to a convention, which is the same silent gap
/// this gate exists to close, only one level deeper. Splitting the inputs makes a
/// platform verdict that claims a workspace factor <b>unconstructible</b>.
/// </remarks>
public sealed class PlatformAvailabilityIsThePreconditionTests
{
    private static PluginPlatformInputs PlatformAllMet() =>
        new(BundledOrInstalled: true, RuntimeHealthy: true, Entitled: true, WithinFaultBudget: true);

    private static PluginWorkspaceInputs WorkspaceAllMet() =>
        new(WorkspaceEnabled: true, TenantActive: true, WorkspaceActive: true,
            RequiredCapabilitiesAvailable: true);

    [Fact]
    public void A_platform_verdict_holds_when_its_four_factors_hold()
    {
        var result = PluginAvailability.From(PlatformAllMet());

        Assert.True(result.IsAvailable);
        Assert.Empty(result.UnmetFactors);
    }

    [Theory]
    [InlineData(PluginAvailabilityFactor.BundledOrInstalled)]
    [InlineData(PluginAvailabilityFactor.RuntimeHealthy)]
    [InlineData(PluginAvailabilityFactor.Entitled)]
    [InlineData(PluginAvailabilityFactor.WithinFaultBudget)]
    public void A_platform_verdict_names_the_platform_factor_it_lost(PluginAvailabilityFactor dropped)
    {
        var result = PluginAvailability.From(DropPlatform(PlatformAllMet(), dropped));

        Assert.False(result.IsAvailable);
        Assert.Equal([dropped], result.UnmetFactors);
    }

    [Fact]
    public void A_platform_verdict_never_reports_a_workspace_factor()
    {
        // The point of the split. A platform verdict cannot claim WorkspaceEnabled either
        // way, because it never saw a workspace — the type does not carry the field.
        var result = PluginAvailability.From(
            PlatformAllMet() with { BundledOrInstalled = false, Entitled = false });

        Assert.DoesNotContain(PluginAvailabilityFactor.WorkspaceEnabled, result.UnmetFactors);
        Assert.DoesNotContain(PluginAvailabilityFactor.TenantActive, result.UnmetFactors);
        Assert.DoesNotContain(PluginAvailabilityFactor.WorkspaceActive, result.UnmetFactors);
        Assert.DoesNotContain(PluginAvailabilityFactor.RequiredCapabilitiesAvailable, result.UnmetFactors);
    }

    [Fact]
    public void A_workspace_verdict_fails_on_a_platform_factor_alone()
    {
        // The precondition relationship, asserted: nothing a workspace can be makes an
        // uninstalled plugin available in it.
        var result = PluginAvailability.From(
            PlatformAllMet() with { BundledOrInstalled = false },
            WorkspaceAllMet());

        Assert.False(result.IsAvailable);
        Assert.Equal([PluginAvailabilityFactor.BundledOrInstalled], result.UnmetFactors);
    }

    [Fact]
    public void A_workspace_verdict_reports_unmet_factors_from_both_layers()
    {
        var result = PluginAvailability.From(
            PlatformAllMet() with { Entitled = false },
            WorkspaceAllMet() with { WorkspaceActive = false });

        Assert.False(result.IsAvailable);
        Assert.Contains(PluginAvailabilityFactor.Entitled, result.UnmetFactors);
        Assert.Contains(PluginAvailabilityFactor.WorkspaceActive, result.UnmetFactors);
    }

    [Fact]
    public void A_workspace_verdict_holds_when_both_layers_hold()
    {
        var result = PluginAvailability.From(PlatformAllMet(), WorkspaceAllMet());

        Assert.True(result.IsAvailable);
        Assert.Empty(result.UnmetFactors);
    }

    [Fact]
    public void The_fault_budget_stays_optional_so_a_host_without_one_is_unchanged()
    {
        var withoutBudget = new PluginPlatformInputs(
            BundledOrInstalled: true, RuntimeHealthy: true, Entitled: true);

        Assert.True(PluginAvailability.From(withoutBudget).IsAvailable);
    }

    private static PluginPlatformInputs DropPlatform(
        PluginPlatformInputs inputs,
        PluginAvailabilityFactor factor) =>
        factor switch
        {
            PluginAvailabilityFactor.BundledOrInstalled => inputs with { BundledOrInstalled = false },
            PluginAvailabilityFactor.RuntimeHealthy => inputs with { RuntimeHealthy = false },
            PluginAvailabilityFactor.Entitled => inputs with { Entitled = false },
            PluginAvailabilityFactor.WithinFaultBudget => inputs with { WithinFaultBudget = false },
            _ => inputs
        };
}

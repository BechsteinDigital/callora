using Callora.Core.Application.Plugins;
using Xunit;

namespace Callora.Core.Tests.Application.Plugins;

/// <summary>
/// The canonical effective-availability derivation (P0-2): available only when
/// every factor holds; any single unmet factor makes the plugin unavailable and
/// is reported. The two input layers are covered by
/// <see cref="PlatformAvailabilityIsThePreconditionTests"/>.
/// </summary>
public sealed class PluginAvailabilityTests
{
    private static PluginPlatformInputs Platform() =>
        new(BundledOrInstalled: true, RuntimeHealthy: true, Entitled: true, WithinFaultBudget: true);

    private static PluginWorkspaceInputs Workspace() =>
        new(WorkspaceEnabled: true, TenantActive: true, WorkspaceActive: true,
            RequiredCapabilitiesAvailable: true);

    [Fact]
    public void From_ExceededFaultBudget_IsUnavailable_AndNamesTheFactor()
    {
        var result = PluginAvailability.From(Platform() with { WithinFaultBudget = false }, Workspace());

        Assert.False(result.IsAvailable);
        Assert.Equal(PluginAvailabilityFactor.WithinFaultBudget, Assert.Single(result.UnmetFactors));
    }

    [Fact]
    public void From_WithoutTheFaultBudgetArgument_TreatsItAsMet()
    {
        // Der Vorgabewert hält jede bestehende Ableitung gültig: Ein Host ohne Fehlerbudget
        // ändert sein Verhalten nicht, nur weil der Faktor hinzugekommen ist.
        var withoutBudget = new PluginPlatformInputs(
            BundledOrInstalled: true, RuntimeHealthy: true, Entitled: true);

        Assert.True(PluginAvailability.From(withoutBudget, Workspace()).IsAvailable);
    }

    [Fact]
    public void From_AllFactorsMet_IsAvailable()
    {
        var result = PluginAvailability.From(Platform(), Workspace());

        Assert.True(result.IsAvailable);
        Assert.Empty(result.UnmetFactors);
    }

    [Theory]
    [InlineData(PluginAvailabilityFactor.BundledOrInstalled)]
    [InlineData(PluginAvailabilityFactor.RuntimeHealthy)]
    [InlineData(PluginAvailabilityFactor.Entitled)]
    [InlineData(PluginAvailabilityFactor.WorkspaceEnabled)]
    [InlineData(PluginAvailabilityFactor.TenantActive)]
    [InlineData(PluginAvailabilityFactor.WorkspaceActive)]
    [InlineData(PluginAvailabilityFactor.RequiredCapabilitiesAvailable)]
    public void From_SingleFactorUnmet_IsUnavailableAndReportsFactor(PluginAvailabilityFactor dropped)
    {
        var (platform, workspace) = Drop(dropped);

        var result = PluginAvailability.From(platform, workspace);

        Assert.False(result.IsAvailable);
        Assert.Equal([dropped], result.UnmetFactors);
    }

    private static (PluginPlatformInputs Platform, PluginWorkspaceInputs Workspace) Drop(
        PluginAvailabilityFactor factor) =>
        factor switch
        {
            PluginAvailabilityFactor.BundledOrInstalled =>
                (Platform() with { BundledOrInstalled = false }, Workspace()),
            PluginAvailabilityFactor.RuntimeHealthy =>
                (Platform() with { RuntimeHealthy = false }, Workspace()),
            PluginAvailabilityFactor.Entitled =>
                (Platform() with { Entitled = false }, Workspace()),
            PluginAvailabilityFactor.WithinFaultBudget =>
                (Platform() with { WithinFaultBudget = false }, Workspace()),
            PluginAvailabilityFactor.WorkspaceEnabled =>
                (Platform(), Workspace() with { WorkspaceEnabled = false }),
            PluginAvailabilityFactor.TenantActive =>
                (Platform(), Workspace() with { TenantActive = false }),
            PluginAvailabilityFactor.WorkspaceActive =>
                (Platform(), Workspace() with { WorkspaceActive = false }),
            PluginAvailabilityFactor.RequiredCapabilitiesAvailable =>
                (Platform(), Workspace() with { RequiredCapabilitiesAvailable = false }),
            _ => (Platform(), Workspace())
        };
}

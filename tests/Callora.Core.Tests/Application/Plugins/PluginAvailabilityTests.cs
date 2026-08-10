using Callora.Core.Application.Plugins;
using Xunit;

namespace Callora.Core.Tests.Application.Plugins;

/// <summary>
/// The canonical effective-availability derivation (P0-2): available only when
/// every factor holds; any single unmet factor makes the plugin unavailable and
/// is reported.
/// </summary>
public sealed class PluginAvailabilityTests
{
    private static PluginAvailabilityInputs AllMet() =>
        new(BundledOrInstalled: true, RuntimeHealthy: true, Entitled: true, WorkspaceEnabled: true,
            TenantActive: true, WorkspaceActive: true, RequiredCapabilitiesAvailable: true,
            WithinFaultBudget: true);

    [Fact]
    public void From_ExceededFaultBudget_IsUnavailable_AndNamesTheFactor()
    {
        var result = PluginAvailability.From(AllMet() with { WithinFaultBudget = false });

        Assert.False(result.IsAvailable);
        Assert.Equal(PluginAvailabilityFactor.WithinFaultBudget, Assert.Single(result.UnmetFactors));
    }

    [Fact]
    public void From_WithoutTheFaultBudgetArgument_TreatsItAsMet()
    {
        // Der Vorgabewert hält jede bestehende Ableitung gültig: Ein Host ohne Fehlerbudget
        // ändert sein Verhalten nicht, nur weil der Faktor hinzugekommen ist.
        var withoutBudget = new PluginAvailabilityInputs(
            BundledOrInstalled: true, RuntimeHealthy: true, Entitled: true, WorkspaceEnabled: true,
            TenantActive: true, WorkspaceActive: true, RequiredCapabilitiesAvailable: true);

        Assert.True(PluginAvailability.From(withoutBudget).IsAvailable);
    }

    [Fact]
    public void From_AllFactorsMet_IsAvailable()
    {
        var result = PluginAvailability.From(AllMet());

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
        var result = PluginAvailability.From(Drop(AllMet(), dropped));

        Assert.False(result.IsAvailable);
        Assert.Equal([dropped], result.UnmetFactors);
    }

    private static PluginAvailabilityInputs Drop(PluginAvailabilityInputs inputs, PluginAvailabilityFactor factor) =>
        factor switch
        {
            PluginAvailabilityFactor.BundledOrInstalled => inputs with { BundledOrInstalled = false },
            PluginAvailabilityFactor.RuntimeHealthy => inputs with { RuntimeHealthy = false },
            PluginAvailabilityFactor.Entitled => inputs with { Entitled = false },
            PluginAvailabilityFactor.WorkspaceEnabled => inputs with { WorkspaceEnabled = false },
            PluginAvailabilityFactor.TenantActive => inputs with { TenantActive = false },
            PluginAvailabilityFactor.WorkspaceActive => inputs with { WorkspaceActive = false },
            PluginAvailabilityFactor.RequiredCapabilitiesAvailable => inputs with { RequiredCapabilitiesAvailable = false },
            _ => inputs
        };
}

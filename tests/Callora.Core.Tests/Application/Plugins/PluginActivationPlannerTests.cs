using Callora.Core.Application.Plugins;

namespace Callora.Core.Tests.Application.Plugins;

public sealed class PluginActivationPlannerTests
{
    private static PluginActivationNode Node(
        string id,
        bool foundation = false,
        string[]? provides = null,
        string[]? requires = null)
        => new(id, foundation, provides ?? [], requires ?? []);

    [Fact]
    public void Plan_WithNoPlugins_IsEmpty()
    {
        var plan = PluginActivationPlanner.Plan([]);

        Assert.Empty(plan.Order);
        Assert.Empty(plan.UnresolvedDependencies);
        Assert.Empty(plan.Cyclic);
    }

    [Fact]
    public void Plan_OrdersProviderBeforeDependent()
    {
        // Dialer requires communication.voice, which Communication provides.
        var plan = PluginActivationPlanner.Plan(
        [
            Node("dialer", requires: ["communication.voice"]),
            Node("communication", foundation: true, provides: ["communication.voice"]),
        ]);

        Assert.Equal(["communication", "dialer"], plan.Order);
        Assert.Empty(plan.UnresolvedDependencies);
        Assert.Empty(plan.Cyclic);
    }

    [Fact]
    public void Plan_PrefersFoundationWhenNoEdgeForcesOrder()
    {
        var plan = PluginActivationPlanner.Plan(
        [
            Node("app"),
            Node("foundation", foundation: true),
        ]);

        Assert.Equal(["foundation", "app"], plan.Order);
    }

    [Fact]
    public void Plan_ReportsMissingDependencyAsUnresolved()
    {
        var plan = PluginActivationPlanner.Plan(
        [
            Node("dialer", requires: ["communication.voice"]),
        ]);

        Assert.Contains("dialer", plan.UnresolvedDependencies);
        Assert.DoesNotContain("dialer", plan.Order);
    }

    [Fact]
    public void Plan_StrandsTransitiveDependentsOfMissingProvider()
    {
        // a needs a missing capability; b needs a capability only a would provide.
        var plan = PluginActivationPlanner.Plan(
        [
            Node("a", provides: ["a.cap"], requires: ["missing.cap"]),
            Node("b", requires: ["a.cap"]),
        ]);

        Assert.Contains("a", plan.UnresolvedDependencies);
        Assert.Contains("b", plan.UnresolvedDependencies);
        Assert.Empty(plan.Order);
    }

    [Fact]
    public void Plan_ReportsCapabilityCycle()
    {
        var plan = PluginActivationPlanner.Plan(
        [
            Node("a", provides: ["a.cap"], requires: ["b.cap"]),
            Node("b", provides: ["b.cap"], requires: ["a.cap"]),
        ]);

        Assert.Contains("a", plan.Cyclic);
        Assert.Contains("b", plan.Cyclic);
        Assert.Empty(plan.Order);
    }

    [Fact]
    public void Plan_TreatsSelfProvidedCapabilityAsSatisfied()
    {
        var plan = PluginActivationPlanner.Plan(
        [
            Node("self", provides: ["x"], requires: ["x"]),
        ]);

        Assert.Equal(["self"], plan.Order);
        Assert.Empty(plan.UnresolvedDependencies);
        Assert.Empty(plan.Cyclic);
    }
}

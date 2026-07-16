using Callora.Core.Application.Events.Business;
using Callora.Core.Application.Events.Contracts;
using Callora.Core.Tests.Support;

namespace Callora.Core.Tests.Application.Events;

/// <summary>
/// Characterizes <see cref="BusinessEventRegistry"/> merge/dedup/ordering so the
/// R1 collector-unification refactor preserves behavior.
/// </summary>
public sealed class BusinessEventRegistryTests
{
    [Fact]
    public void List_MergesHostAndPluginProviders_OrderedByEventNameOrdinal()
    {
        var host = new StubProvider(Descriptor("beta.event"), Descriptor("alpha.event"));
        var catalog = new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>
        {
            [typeof(IBusinessEventProvider)] = [new StubProvider(Descriptor("gamma.event"))]
        });
        var registry = new BusinessEventRegistry([host], catalog);

        var names = registry.ListDescriptors().Select(descriptor => descriptor.EventName).ToArray();

        Assert.Equal(["alpha.event", "beta.event", "gamma.event"], names);
    }

    [Fact]
    public void List_DedupesByEventName_CaseInsensitive_LastWins()
    {
        // Characterization: same event name (case-insensitive) is deduped and the
        // later provider wins — plugins are concatenated after host providers.
        var host = new StubProvider(new BusinessEventDescriptor("order.placed", "Host label", []));
        var catalog = new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>
        {
            [typeof(IBusinessEventProvider)] = [new StubProvider(new BusinessEventDescriptor("ORDER.PLACED", "Plugin label", []))]
        });
        var registry = new BusinessEventRegistry([host], catalog);

        var result = registry.ListDescriptors();

        Assert.Single(result);
        Assert.Equal("ORDER.PLACED", result[0].EventName);
        Assert.Equal("Plugin label", result[0].DisplayName);
    }

    [Fact]
    public void List_NoProviders_ReturnsEmpty()
    {
        var registry = new BusinessEventRegistry([], new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>()));

        Assert.Empty(registry.ListDescriptors());
    }

    private static BusinessEventDescriptor Descriptor(string name) => new(name, name, []);

    private sealed class StubProvider(params BusinessEventDescriptor[] descriptors) : IBusinessEventProvider
    {
        public IReadOnlyList<BusinessEventDescriptor> GetDescriptors() => descriptors;
    }
}

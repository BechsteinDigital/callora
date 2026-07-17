using Callora.Core.Application.Events.Contracts;
using Callora.Core.Application.Extensibility.Contracts;
using Callora.Core.Application.Flows.Contracts;
using Callora.Core.Application.Http.Contracts;
using Callora.Core.Application.Jobs.Contracts;
using Callora.Core.Domain.Extensions;
using Callora.Core.Extensibility;
using Callora.Core.Infrastructure.Extensions;

namespace Callora.Core.Tests.Application.Extensions;

/// <summary>
/// Locks the plugin extension surface (B/C): the extension-point catalogue is driven
/// by the <see cref="CalloraExtensionPoints"/> constants, and every plugin-implemented
/// contract carries <c>[CalloraExtensible]</c> so the surface stays discoverable and
/// consistent.
/// </summary>
public sealed class ExtensionSurfaceTests
{
    [Fact]
    public void Catalog_ExposesExactly_TheDeclaredConstants()
    {
        var catalogIds = BackendExtensionPointCatalog.Build()
            .Select(definition => definition.ExtensionPointId)
            .ToHashSet(StringComparer.Ordinal);

        var declared = new HashSet<string>(StringComparer.Ordinal)
        {
            CalloraExtensionPoints.WorkspaceNavigationMain,
            CalloraExtensionPoints.WorkspaceThemeDefinition,
            CalloraExtensionPoints.WorkspaceThemeSettings,
            CalloraExtensionPoints.AdminNavigationMain,
            CalloraExtensionPoints.AdminApiRoute,
        };

        Assert.Equal(declared, catalogIds);
    }

    [Theory]
    [InlineData(typeof(IBackgroundJobHandler))]
    [InlineData(typeof(IRecurringJobProvider))]
    [InlineData(typeof(IFlowActionHandler))]
    [InlineData(typeof(IRuleConditionEvaluator))]
    [InlineData(typeof(IBusinessEventListener))]
    [InlineData(typeof(IHostEventSubscriber<>))]
    [InlineData(typeof(IServiceDecorator<>))]
    [InlineData(typeof(IApiController))]
    [InlineData(typeof(IBusinessEvent))]
    [InlineData(typeof(IHostEvent))]
    public void PluginExtensionPoint_IsMarkedExtensible(Type contract)
    {
        Assert.True(
            contract.IsDefined(typeof(CalloraExtensibleAttribute), inherit: false),
            $"{contract.Name} is a plugin extension point and must carry [CalloraExtensible].");
    }
}

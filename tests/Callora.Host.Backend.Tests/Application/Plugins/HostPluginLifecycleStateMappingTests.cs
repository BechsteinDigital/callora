using Callora.Host.PluginContracts.Application.Plugins;
using Callora.Hosting.Application.Plugins;
using Xunit;

namespace Callora.Host.Backend.Tests.Application.Plugins;

/// <summary>
/// The host façade must not flatten failure states to Installed (P0-4 follow-up):
/// a faulted or pinned plugin has to stay visible as such.
/// </summary>
public sealed class HostPluginLifecycleStateMappingTests
{
    [Theory]
    [InlineData(RuntimePluginState.Installed, HostPluginState.Installed)]
    [InlineData(RuntimePluginState.Active, HostPluginState.Active)]
    [InlineData(RuntimePluginState.Inactive, HostPluginState.Inactive)]
    [InlineData(RuntimePluginState.Faulted, HostPluginState.Faulted)]
    [InlineData(RuntimePluginState.UnloadFailed, HostPluginState.UnloadFailed)]
    public void ToHostState_MapsFailureStatesVisibly(RuntimePluginState runtimeState, HostPluginState expected)
    {
        Assert.Equal(expected, HostPluginLifecycle.ToHostState(runtimeState));
    }
}

using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;
using Xunit;

namespace Callora.Core.Tests.Application.Plugins;

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

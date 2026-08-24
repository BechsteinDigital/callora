using Callora.Core.Application.Diagnostics;
using Xunit;

namespace Callora.Core.Tests.Application.Diagnostics;

/// <summary>
/// Attribution is the hard half of the recorder. Under ADR-013 several foreign plugins share
/// one process and one database connection, so nothing about a query itself says who issued
/// it — the host has to know before the query happens.
/// </summary>
/// <remarks>
/// The entry points into plugin code are known and few: a plugin HTTP route, a job whose
/// handler a plugin owns, an event withheld from unavailable plugins. Each already resolves
/// the owning plugin for the availability gate, so marking the scope there costs a lookup
/// nobody has to add.
/// </remarks>
public sealed class WhoIsRunningRightNowTests
{
    [Fact]
    public void Nobody_is_running_by_default()
    {
        Assert.Null(PluginExecutionScope.Current);
    }

    [Fact]
    public void Inside_a_scope_the_plugin_is_known()
    {
        using (PluginExecutionScope.Enter("billed-plugin"))
        {
            Assert.Equal("billed-plugin", PluginExecutionScope.Current);
        }

        Assert.Null(PluginExecutionScope.Current);
    }

    [Fact]
    public void A_nested_scope_restores_the_outer_one()
    {
        using (PluginExecutionScope.Enter("outer"))
        {
            using (PluginExecutionScope.Enter("inner"))
            {
                Assert.Equal("inner", PluginExecutionScope.Current);
            }

            // A plugin calling into another plugin's exported service must not leave the
            // first one credited with the second one's queries — or the other way round.
            Assert.Equal("outer", PluginExecutionScope.Current);
        }
    }

    [Fact]
    public async Task The_scope_survives_an_await()
    {
        using (PluginExecutionScope.Enter("billed-plugin"))
        {
            await Task.Yield();
            Assert.Equal("billed-plugin", PluginExecutionScope.Current);

            await Task.Delay(1);
            Assert.Equal("billed-plugin", PluginExecutionScope.Current);
        }
    }

    [Fact]
    public async Task Concurrent_work_does_not_bleed_between_plugins()
    {
        // The case the whole thing has to get right: a shared process serves several
        // plugins at once, and an attribution that leaks across requests is worse than
        // none — it points at the wrong culprit with full confidence.
        var seen = await Task.WhenAll(
            Observe("plugin-a"),
            Observe("plugin-b"),
            Observe("plugin-c"));

        Assert.Equal<string?[]>(["plugin-a", "plugin-b", "plugin-c"], seen);

        static async Task<string?> Observe(string pluginId)
        {
            using (PluginExecutionScope.Enter(pluginId))
            {
                await Task.Delay(Random.Shared.Next(1, 5));
                return PluginExecutionScope.Current;
            }
        }
    }
}

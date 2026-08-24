using Callora.Core.Application.Security;
using Xunit;

namespace Callora.Core.Tests.Application.Security;

/// <summary>
/// A plugin declaring the permission keys its routes require is the fix for a real gap:
/// <c>CalloraRouteAttribute.Permission</c> lets a plugin <b>demand</b> a key, and nothing
/// could <b>supply</b> one, so a purchased plugin arrived at a customer permanently answering
/// 403 with no way to grant what it asked for.
/// </summary>
/// <remarks>
/// The boundary these tests draw is the part the issue did not anticipate. Declaration is
/// self-service, so without a namespace rule a plugin could declare <c>user.delete</c> and
/// have an operator grant it in good faith, believing it to be the plugin's own. A key is
/// therefore only declarable inside the declaring plugin's own namespace.
/// </remarks>
public sealed class APluginMayOnlyDeclareItsOwnKeysTests
{
    [Theory]
    [InlineData("communication.trunk.update")]
    [InlineData("communication.read")]
    [InlineData("communication.call.execute")]
    public void A_key_inside_the_plugins_namespace_is_accepted(string key)
    {
        Assert.True(PluginPermissionKeyPolicy.IsDeclarable("communication", key, out var reason), reason);
    }

    [Theory]
    [InlineData("user.delete")]
    [InlineData("plugin.execute")]
    [InlineData("workspace.read")]
    public void A_host_key_is_refused(string key)
    {
        // The attack this closes: an operator granting what looks like the plugin's own
        // permission and handing it the host's instead.
        Assert.False(PluginPermissionKeyPolicy.IsDeclarable("communication", key, out var reason));
        Assert.Contains("communication", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Another_plugins_key_is_refused()
    {
        Assert.False(PluginPermissionKeyPolicy.IsDeclarable("communication", "composer.page.update", out _));
    }

    [Fact]
    public void A_prefix_that_only_looks_like_the_namespace_is_refused()
    {
        // "communications" starts with "communication" as a string but is a different
        // plugin. Comparing prefixes without the separator would hand it the other's keys.
        Assert.False(PluginPermissionKeyPolicy.IsDeclarable("communication", "communications.read", out _));
    }

    [Theory]
    [InlineData("communication.trunk")]
    [InlineData("communication.trunk.list")]
    public void A_key_not_ending_in_a_known_action_is_refused(string key)
    {
        // Keys are granted through role-function-action config. One that does not end in a
        // known action cannot be expressed there, so accepting it would move the dead end
        // rather than remove it.
        Assert.False(PluginPermissionKeyPolicy.IsDeclarable("communication", key, out var reason));
        Assert.Contains("action", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_bare_namespace_is_not_a_key()
    {
        // Refused for being outside the namespace rather than for lacking an action, and
        // that ordering is right: "communication" is the prefix, not something inside it.
        Assert.False(PluginPermissionKeyPolicy.IsDeclarable("communication", "communication", out var reason));
        Assert.Contains("communication.", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void The_accepted_actions_are_the_hosts_own()
    {
        foreach (var action in BackendPermissionActions.All)
        {
            Assert.True(PluginPermissionKeyPolicy.IsDeclarable("communication", $"communication.thing.{action}", out _));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_key_is_refused(string key)
    {
        Assert.False(PluginPermissionKeyPolicy.IsDeclarable("communication", key, out _));
    }
}

/// <summary>
/// Two holes the namespace rule alone does not close, found reviewing it after the fact.
/// </summary>
public sealed class TheNamespaceRuleAloneIsNotEnoughTests
{
    [Theory]
    [InlineData("user")]
    [InlineData("workspace")]
    [InlineData("plugin")]
    [InlineData("role")]
    public void A_plugin_cannot_take_a_host_function_as_its_namespace(string hostFunction)
    {
        // The hole: pluginId is only checked for being non-empty, so a plugin can call
        // itself "user" — and then "user.delete" IS inside its own namespace. An operator
        // reading the plugin's declared permissions sees what looks like the plugin's own
        // key and grants the host's. The namespace rule was the whole defence, and it was
        // defeated by choosing the namespace.
        Assert.False(
            PluginPermissionKeyPolicy.IsDeclarable(hostFunction, $"{hostFunction}.delete", out var reason));
        Assert.Contains("host", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_reserved_functions_come_from_the_host_keys_themselves()
    {
        // Derived, not listed: a hand-kept list of reserved names is one release away from
        // being wrong, and wrong here means a plugin gets a host permission.
        Assert.Contains("membership", PluginPermissionKeyPolicy.ReservedFunctions, StringComparer.Ordinal);
        Assert.Contains("snippet", PluginPermissionKeyPolicy.ReservedFunctions, StringComparer.Ordinal);
        Assert.Contains("webhook", PluginPermissionKeyPolicy.ReservedFunctions, StringComparer.Ordinal);

        // Every host key's function segment, with none missed — the property that makes a
        // hand-kept list unnecessary and this one trustworthy.
        Assert.Contains("user", PluginPermissionKeyPolicy.ReservedFunctions, StringComparer.Ordinal);
        Assert.DoesNotContain("communication", PluginPermissionKeyPolicy.ReservedFunctions, StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("Communication.Thing.Read")]
    [InlineData("communication.Thing.read")]
    [InlineData("communication.thing.READ")]
    public void A_key_that_is_not_lower_case_is_refused(string key)
    {
        // The second hole, quieter. Authorization compares claims with StringComparison
        // .Ordinal and BackendRbacPermissionCatalog emits lower case, so a key declared with
        // capitals passes the manifest and then never matches anything. That is this whole
        // issue's failure mode again: it looks right and answers 403 forever.
        Assert.False(PluginPermissionKeyPolicy.IsDeclarable("communication", key, out var reason));
        Assert.Contains("lower case", reason, StringComparison.OrdinalIgnoreCase);
    }
}

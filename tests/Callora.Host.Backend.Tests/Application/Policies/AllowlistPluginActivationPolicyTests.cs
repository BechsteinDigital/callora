using Callora.Host.Backend.Application.Policies;
namespace Callora.Host.Backend.Tests.Application.Policies;

public sealed class AllowlistPluginActivationPolicyTests
{
    [Fact]
    public async Task EvaluateAsync_AllowlistDisabled_AllowsPlugin()
    {
        var options = new BackendHostOptions
        {
            RequireAllowlistForActivation = false
        };
        var sut = new AllowlistPluginActivationPolicy(options);

        var decision = await sut.EvaluateAsync("voip");

        Assert.True(decision.IsAllowed);
        Assert.Null(decision.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_AllowlistEnabled_DeniesUnknownPlugin()
    {
        var options = new BackendHostOptions
        {
            RequireAllowlistForActivation = true,
            ActivationAllowlistPluginIds = ["voip"]
        };
        var sut = new AllowlistPluginActivationPolicy(options);

        var decision = await sut.EvaluateAsync("recording");

        Assert.False(decision.IsAllowed);
        Assert.Contains("not present in activation allowlist", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_AllowlistEnabled_AllowsKnownPlugin()
    {
        var options = new BackendHostOptions
        {
            RequireAllowlistForActivation = true,
            ActivationAllowlistPluginIds = ["voip"]
        };
        var sut = new AllowlistPluginActivationPolicy(options);

        var decision = await sut.EvaluateAsync("voip");

        Assert.True(decision.IsAllowed);
        Assert.Null(decision.Reason);
    }
}

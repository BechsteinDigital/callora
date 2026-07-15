using Callora.Core.Application.Flows;
using Callora.Core.Application.Flows.Conditions;
using Callora.Host.PluginContracts.Application.Flows;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Callora.Core.Tests.Application.Flows;

public sealed class RuleEvaluatorTests
{
    private static RuleEvaluator CreateEvaluator() => new(
        [
            new EventNameConditionEvaluator(),
            new DataFieldConditionEvaluator(),
            new WorkspaceKeyConditionEvaluator(),
            new TimeWindowConditionEvaluator()
        ],
        NullLogger<RuleEvaluator>.Instance);

    private static RuleContext Context(
        string eventName = "call.ringing",
        string workspaceKey = "test",
        DateTimeOffset? now = null,
        Dictionary<string, string>? data = null) =>
        new(eventName, workspaceKey, data ?? new Dictionary<string, string>
        {
            ["direction"] = "Inbound",
            ["target"] = "+4930123456"
        }, now ?? new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero)); // Mittwoch 10:00 UTC

    [Fact]
    public void NullConditions_AlwaysMatch()
    {
        Assert.True(CreateEvaluator().Evaluate(null, Context()));
    }

    [Fact]
    public void AndOrNot_CombineCorrectly()
    {
        var evaluator = CreateEvaluator();
        var tree = new RuleConditionNode("and", [
            new RuleConditionNode("data.field", Params: new() { ["field"] = "direction", ["value"] = "Inbound" }),
            new RuleConditionNode("or", [
                new RuleConditionNode("workspace.key", Params: new() { ["value"] = "other" }),
                new RuleConditionNode("data.field", Params: new() { ["field"] = "target", ["value"] = "+4930*" })
            ]),
            new RuleConditionNode("not", [
                new RuleConditionNode("event.name", Params: new() { ["value"] = "call.ended" })
            ])
        ]);

        Assert.True(evaluator.Evaluate(tree, Context()));
        Assert.False(evaluator.Evaluate(tree, Context(eventName: "call.ended")));
    }

    [Fact]
    public void UnknownLeafType_EvaluatesFalse()
    {
        Assert.False(CreateEvaluator().Evaluate(new RuleConditionNode("does.not.exist"), Context()));
    }

    [Fact]
    public void TimeWindow_MatchesBusinessHours_AndRejectsOutside()
    {
        var evaluator = CreateEvaluator();
        var businessHours = new RuleConditionNode("time.window", Params: new()
        {
            ["days"] = "mon,tue,wed,thu,fri",
            ["from"] = "08:00",
            ["to"] = "18:00"
        });

        Assert.True(evaluator.Evaluate(businessHours, Context(now: new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero))));
        Assert.False(evaluator.Evaluate(businessHours, Context(now: new DateTimeOffset(2026, 7, 15, 20, 0, 0, TimeSpan.Zero))));
        Assert.False(evaluator.Evaluate(businessHours, Context(now: new DateTimeOffset(2026, 7, 18, 10, 0, 0, TimeSpan.Zero)))); // Samstag
    }

    [Fact]
    public void TimeWindow_CrossingMidnight_Matches()
    {
        var evaluator = CreateEvaluator();
        var nightShift = new RuleConditionNode("time.window", Params: new()
        {
            ["from"] = "22:00",
            ["to"] = "06:00"
        });

        Assert.True(evaluator.Evaluate(nightShift, Context(now: new DateTimeOffset(2026, 7, 15, 23, 30, 0, TimeSpan.Zero))));
        Assert.True(evaluator.Evaluate(nightShift, Context(now: new DateTimeOffset(2026, 7, 15, 5, 0, 0, TimeSpan.Zero))));
        Assert.False(evaluator.Evaluate(nightShift, Context(now: new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero))));
    }
}

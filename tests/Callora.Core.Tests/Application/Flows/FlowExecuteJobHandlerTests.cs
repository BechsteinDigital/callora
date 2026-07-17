using Callora.Core.Application.Flows;
using Callora.Core.Application.Flows.Contracts;
using Callora.Core.Application.Jobs.Contracts;
using Callora.Core.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;

namespace Callora.Core.Tests.Application.Flows;

public sealed class FlowExecuteJobHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task ExecutesActionsSequentially_WithParams()
    {
        var store = new InMemoryFlowStore();
        var flow = await store.UpsertAsync(new FlowSnapshot(
            Guid.Empty,
            "test",
            "Begrüßung",
            "call.ringing",
            null,
            """[{ "type": "record.a", "params": { "value": "1" } }, { "type": "record.b" }]""",
            IsActive: true,
            Priority: 100,
            DateTimeOffset.UtcNow));

        var recorder = new RecordingFlowActionHandler("record.a");
        var recorderB = new RecordingFlowActionHandler("record.b");
        var handler = CreateHandler(store, recorder, recorderB);

        await handler.ExecuteAsync(BuildJobContext(flow.Id));

        var call = Assert.Single(recorder.Executions);
        Assert.Equal("1", call.Parameters["value"]);
        Assert.Single(recorderB.Executions);
    }

    [Fact]
    public async Task UnknownActionType_Throws()
    {
        var store = new InMemoryFlowStore();
        var flow = await store.UpsertAsync(new FlowSnapshot(
            Guid.Empty, "test", "Kaputt", "call.ringing", null,
            """[{ "type": "missing.action" }]""", true, 100, DateTimeOffset.UtcNow));
        var handler = CreateHandler(store);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.ExecuteAsync(BuildJobContext(flow.Id)));
    }

    [Fact]
    public async Task InactiveFlow_IsSkipped()
    {
        var store = new InMemoryFlowStore();
        var recorder = new RecordingFlowActionHandler("record.a");
        var flow = await store.UpsertAsync(new FlowSnapshot(
            Guid.Empty, "test", "Aus", "call.ringing", null,
            """[{ "type": "record.a" }]""", IsActive: false, 100, DateTimeOffset.UtcNow));
        var handler = CreateHandler(store, recorder);

        await handler.ExecuteAsync(BuildJobContext(flow.Id));

        Assert.Empty(recorder.Executions);
    }

    private static FlowExecuteJobHandler CreateHandler(IFlowStore store, params IFlowActionHandler[] handlers) =>
        new(
            store,
            new FlowActionRegistry(handlers, new StaticPluginCatalog([])),
            NullLogger<FlowExecuteJobHandler>.Instance);

    private static BackgroundJobExecutionContext BuildJobContext(Guid flowId) => new(
        Guid.NewGuid(),
        FlowJobs.ExecuteJobType,
        JsonSerializer.Serialize(
            new FlowExecutePayload(flowId, "call.ringing", "test", new Dictionary<string, string> { ["callId"] = "c1" }),
            JsonOptions),
        "test",
        Attempt: 1);
}

using System.Net;
using System.Text.Json;
using Callora.Host.Backend.Application.Policies;
using Callora.Host.Backend.Application.Webhooks;
using Callora.Host.Backend.Tests.Support;
using Callora.Host.PluginContracts.Application.Jobs;
using Xunit;

namespace Callora.Host.Backend.Tests.Application.Webhooks;

public sealed class WebhookDeliveryTests
{
    [Fact]
    public void Signature_IsDeterministicHmacSha256()
    {
        var first = WebhookSignature.Compute("secret", "{\"a\":1}");
        var second = WebhookSignature.Compute("secret", "{\"a\":1}");
        var different = WebhookSignature.Compute("other", "{\"a\":1}");

        Assert.StartsWith("sha256=", first);
        Assert.Equal(first, second);
        Assert.NotEqual(first, different);
    }

    [Fact]
    public async Task JobHandler_PostsSignedPayload()
    {
        var store = new InMemoryWebhookSubscriptionStore();
        var subscription = await store.CreateAsync("workspace-a", "call.ringing", "https://example.org/hook", "s3cret");
        var handler = new RecordingHttpMessageHandler();
        var jobHandler = new WebhookDeliveryJobHandler(store, new StaticHttpClientFactory(handler), TestGuard());
        var payload = JsonSerializer.Serialize(
            new WebhookDeliveryPayload(subscription.Id, "call.ringing", "{\"callId\":\"c1\"}"),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        await jobHandler.ExecuteAsync(new BackgroundJobExecutionContext(
            Guid.NewGuid(), WebhookDispatcher.DeliveryJobType, payload, "workspace-a", Attempt: 1));

        var (request, body) = Assert.Single(handler.Requests);
        Assert.Equal("https://example.org/hook", request.RequestUri!.ToString());
        Assert.Equal("{\"callId\":\"c1\"}", body);
        Assert.Equal("call.ringing", request.Headers.GetValues(WebhookSignature.EventHeaderName).Single());
        Assert.Equal(
            WebhookSignature.Compute("s3cret", body),
            request.Headers.GetValues(WebhookSignature.HeaderName).Single());
    }

    [Fact]
    public async Task JobHandler_FailedDelivery_Throws_ForQueueRetry()
    {
        var store = new InMemoryWebhookSubscriptionStore();
        var subscription = await store.CreateAsync(null, "*", "https://example.org/hook", "s3cret");
        var jobHandler = new WebhookDeliveryJobHandler(
            store,
            new StaticHttpClientFactory(new RecordingHttpMessageHandler(HttpStatusCode.BadGateway)),
            TestGuard());
        var payload = JsonSerializer.Serialize(
            new WebhookDeliveryPayload(subscription.Id, "call.ended", "{}"),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            jobHandler.ExecuteAsync(new BackgroundJobExecutionContext(
                Guid.NewGuid(), WebhookDispatcher.DeliveryJobType, payload, null, Attempt: 1)));
    }

    [Fact]
    public async Task JobHandler_DeactivatedSubscription_SkipsDeliverySilently()
    {
        var store = new InMemoryWebhookSubscriptionStore();
        var subscription = await store.CreateAsync(null, "*", "https://example.org/hook", "s3cret");
        await store.SetActiveAsync(subscription.Id, isActive: false);
        var handler = new RecordingHttpMessageHandler();
        var jobHandler = new WebhookDeliveryJobHandler(store, new StaticHttpClientFactory(handler), TestGuard());
        var payload = JsonSerializer.Serialize(
            new WebhookDeliveryPayload(subscription.Id, "call.ended", "{}"),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        await jobHandler.ExecuteAsync(new BackgroundJobExecutionContext(
            Guid.NewGuid(), WebhookDispatcher.DeliveryJobType, payload, null, Attempt: 1));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Store_ListActiveForEvent_MatchesExactWildcardAndWorkspace()
    {
        var store = new InMemoryWebhookSubscriptionStore();
        await store.CreateAsync("workspace-a", "call.ringing", "https://a.example", "s");
        await store.CreateAsync(null, "*", "https://b.example", "s");
        await store.CreateAsync("workspace-b", "call.ringing", "https://c.example", "s");
        await store.CreateAsync("workspace-a", "call.ended", "https://d.example", "s");

        var matches = await store.ListActiveForEventAsync("call.ringing", "workspace-a");

        Assert.Equal(2, matches.Count);
        Assert.Contains(matches, m => m.TargetUrl == "https://a.example");
        Assert.Contains(matches, m => m.TargetUrl == "https://b.example");
    }

    private static WebhookEgressGuard TestGuard() =>
        new(new BackendHostOptions { AllowPrivateWebhookTargets = true });
}

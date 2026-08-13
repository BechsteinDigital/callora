using Callora.Core.Application.Jobs.Contracts;
using Callora.Core.Application.Policies;
using Callora.Core.Application.Webhooks;
using Callora.Core.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;

namespace Callora.Core.Tests.Application.Webhooks;

/// <summary>
/// Die Zustell-Id, auf der ein Empfänger dedupliziert.
/// </summary>
/// <remarks>
/// <para>
/// Ohne sie kam eine Zustellung bis zu fünfmal an (das Versuchsbudget des Jobs), und der Empfänger
/// konnte eine Wiederholung nicht von einem neuen Ereignis unterscheiden: Zwei gleiche Ereignisse
/// unterscheiden sich in nichts, was über die Leitung geht. Die Doppelzustellung kam also nicht
/// erst mit der HTTP-Wiederholung dazu — sie war schon da, nur schwerer zu bemerken.
/// </para>
/// <para>
/// Die eine Eigenschaft, auf die es ankommt, ist die Stabilität über Versuche hinweg. Eine je
/// Versuch neu erzeugte Id wäre schlimmer als keine: Sie sähe aus wie eine Zusicherung und wäre
/// keine.
/// </para>
/// </remarks>
public sealed class DeliveriesCarryAStableIdTests
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task TheSamePayloadDeliveredTwiceCarriesTheSameId()
    {
        var store = new InMemoryWebhookSubscriptionStore();
        var subscription = await store.CreateAsync("workspace-a", "call.ringing", "https://example.org/hook", "s3cret");
        var handler = new RecordingHttpMessageHandler();
        var jobHandler = new WebhookDeliveryJobHandler(store, new StaticHttpClientFactory(handler), TestGuard());
        var payload = JsonSerializer.Serialize(
            new WebhookDeliveryPayload(subscription.Id, "call.ringing", "{\"callId\":\"c1\"}", Guid.NewGuid()),
            WebJsonOptions);

        // Zweimal derselbe Payload — genau das tut die Warteschlange beim Wiederholen.
        await ExecuteAsync(jobHandler, payload, attempt: 1);
        await ExecuteAsync(jobHandler, payload, attempt: 2);

        Assert.Equal(2, handler.Requests.Count);
        var ids = handler.Requests
            .Select(entry => entry.Request.Headers.GetValues(WebhookSignature.DeliveryHeaderName).Single())
            .ToArray();
        Assert.Equal(ids[0], ids[1]);
        Assert.NotEqual(Guid.Empty, Guid.Parse(ids[0]));
    }

    /// <summary>
    /// Jobs aus der Zeit vor der Id tragen sie nicht. Dann fehlt der Header, statt einen erfundenen
    /// Wert zu senden — der wäre je Versuch anders und machte aus jeder Wiederholung ein neues
    /// Ereignis.
    /// </summary>
    [Fact]
    public async Task AQueuedJobWithoutAnIdIsDeliveredWithoutTheHeader()
    {
        var store = new InMemoryWebhookSubscriptionStore();
        var subscription = await store.CreateAsync("workspace-a", "call.ringing", "https://example.org/hook", "s3cret");
        var handler = new RecordingHttpMessageHandler();
        var jobHandler = new WebhookDeliveryJobHandler(store, new StaticHttpClientFactory(handler), TestGuard());

        // Wie er in der Warteschlange steht, bevor es das Feld gab.
        var legacyPayload = "{\"subscriptionId\":\"" + subscription.Id + "\","
            + "\"eventName\":\"call.ringing\",\"bodyJson\":\"{}\"}";

        await ExecuteAsync(jobHandler, legacyPayload, attempt: 1);

        var (request, _) = Assert.Single(handler.Requests);
        Assert.False(request.Headers.Contains(WebhookSignature.DeliveryHeaderName));
    }

    /// <summary>
    /// Die Id gehört zur Zustellung, nicht zum Ereignis: Zwei Abos auf dasselbe Ereignis sind zwei
    /// Zustellungen und müssen sich unterscheiden — sonst verwürfe der zweite Empfänger seine
    /// Nachricht als Dublette, wenn beide hinter demselben Dienst hängen.
    /// </summary>
    [Fact]
    public async Task TwoSubscriptionsOnOneEventGetDifferentIds()
    {
        var store = new InMemoryWebhookSubscriptionStore();
        await store.CreateAsync("workspace-a", "call.ringing", "https://a.example", "s");
        await store.CreateAsync("workspace-a", "call.ringing", "https://b.example", "s");
        var queue = new RecordingBackgroundJobQueue();
        var services = new ServiceCollection();
        services.AddScoped<IWebhookSubscriptionStore>(_ => store);
        using var provider = services.BuildServiceProvider();
        var dispatcher = new WebhookDispatcher(
            provider.GetRequiredService<IServiceScopeFactory>(),
            queue,
            new SensitivePayloadFieldRegistry(),
            NullLogger<WebhookDispatcher>.Instance);

        await dispatcher.DispatchAsync("call.ringing", "workspace-a", new { callId = "c1" });

        Assert.Equal(2, queue.Requests.Count);
        var ids = queue.Requests
            .Select(request => JsonSerializer.Deserialize<WebhookDeliveryPayload>(request.PayloadJson, WebJsonOptions)!.DeliveryId)
            .ToArray();
        Assert.DoesNotContain(Guid.Empty, ids);
        Assert.NotEqual(ids[0], ids[1]);
    }

    private static Task ExecuteAsync(WebhookDeliveryJobHandler jobHandler, string payload, int attempt) =>
        jobHandler.ExecuteAsync(new BackgroundJobExecutionContext(
            Guid.NewGuid(), WebhookDispatcher.DeliveryJobType, payload, "workspace-a", attempt));

    private static WebhookEgressGuard TestGuard() =>
        new(new BackendHostOptions { AllowPrivateWebhookTargets = true });
}

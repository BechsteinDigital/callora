using Callora.Core.Application.Jobs.Contracts;
using Callora.Core.Application.Webhooks;
using Callora.Core.Infrastructure.Webhooks;
using Callora.Core.Tests.Support;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Calls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Callora.Core.Tests.Communication.Calls;

/// <summary>
/// A real <see cref="CallBusinessEvent"/> carries the remote telephone number in
/// <c>remoteParty</c>. Default webhook delivery must mask it (#107) — the Communication
/// manifest declares the field, and the production dispatcher/minimizer honours it.
/// </summary>
public sealed class CallEventWebhookRedactionTests
{
    private const string RemoteNumber = "+4930123456789";
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void CommunicationManifest_DeclaresEveryTelephoneField_ItsEventsEmit()
    {
        var manifestPath = ResolveManifestPath();
        var declared = RegistrySensitiveFieldSyncService.ParseSensitiveFields(File.ReadAllText(manifestPath));

        var emitted = NewEvent().ToEventData().Keys;

        // Every field the event serializes that holds a telephone number must be declared.
        Assert.Contains("remoteParty", emitted, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("remoteParty", declared, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Dispatch_WithoutSensitiveOptIn_MasksRemoteParty()
    {
        var (dispatcher, queue) = CreateDispatcher(await SubscribeAsync(includeSensitiveData: false));

        await dispatcher.DispatchAsync(CallEventTypes.Ringing, "workspace-a", NewEvent().ToEventData());

        var data = SingleDeliveryData(queue);
        var remoteParty = data["remoteParty"]!.GetValue<string>();
        Assert.NotEqual(RemoteNumber, remoteParty);
        Assert.DoesNotContain("30123456", remoteParty, StringComparison.Ordinal);
        Assert.Contains("***", remoteParty, StringComparison.Ordinal);
        // Non-sensitive fields survive, so the payload stays useful.
        Assert.Equal("call-1", data["callId"]!.GetValue<string>());
    }

    [Fact]
    public async Task Dispatch_WithExplicitSensitiveOptIn_KeepsRemoteParty()
    {
        var (dispatcher, queue) = CreateDispatcher(await SubscribeAsync(includeSensitiveData: true));

        await dispatcher.DispatchAsync(CallEventTypes.Ringing, "workspace-a", NewEvent().ToEventData());

        Assert.Equal(RemoteNumber, SingleDeliveryData(queue)["remoteParty"]!.GetValue<string>());
    }

    [Fact]
    public async Task Dispatch_WithoutTheManifestFields_LeavesRemotePartyInTheClear()
    {
        // Guards the failure this issue describes: an unregistered field is emitted in
        // the clear. The assertion documents that masking depends on the manifest sync.
        var store = new InMemoryWebhookSubscriptionStore();
        _ = await store.CreateAsync("workspace-a", CallEventTypes.Ringing, "https://example.org/hook", "s3cret");
        var queue = new RecordingBackgroundJobQueue();
        var dispatcher = new WebhookDispatcher(
            new SingleScopeFactory(store),
            queue,
            new SensitivePayloadFieldRegistry(),
            NullLogger<WebhookDispatcher>.Instance);

        await dispatcher.DispatchAsync(CallEventTypes.Ringing, "workspace-a", NewEvent().ToEventData());

        Assert.Equal(RemoteNumber, SingleDeliveryData(queue)["remoteParty"]!.GetValue<string>());
    }

    private static CallBusinessEvent NewEvent() => CallBusinessEvent.Ringing(
        "workspace-a",
        "call-1",
        CallDirection.Inbound,
        RemoteNumber,
        CallState.Ringing,
        DateTimeOffset.UnixEpoch);

    private static async Task<InMemoryWebhookSubscriptionStore> SubscribeAsync(bool includeSensitiveData)
    {
        var store = new InMemoryWebhookSubscriptionStore();
        _ = await store.CreateAsync(
            "workspace-a",
            CallEventTypes.Ringing,
            "https://example.org/hook",
            "s3cret",
            includeSensitiveData);

        return store;
    }

    private static (WebhookDispatcher Dispatcher, RecordingBackgroundJobQueue Queue) CreateDispatcher(
        InMemoryWebhookSubscriptionStore store)
    {
        // The production registry, fed from the shipped Communication manifest.
        var registry = new SensitivePayloadFieldRegistry();
        registry.RegisterPluginFields(
            "communication",
            RegistrySensitiveFieldSyncService.ParseSensitiveFields(File.ReadAllText(ResolveManifestPath())));

        var queue = new RecordingBackgroundJobQueue();
        return (
            new WebhookDispatcher(
                new SingleScopeFactory(store),
                queue,
                registry,
                NullLogger<WebhookDispatcher>.Instance),
            queue);
    }

    /// <summary>The <c>data</c> object of the single enqueued delivery.</summary>
    private static JsonObject SingleDeliveryData(RecordingBackgroundJobQueue queue)
    {
        var job = Assert.Single(queue.Requests);
        var payload = JsonSerializer.Deserialize<WebhookDeliveryPayload>(job.PayloadJson, WebJsonOptions);
        Assert.NotNull(payload);

        var body = JsonNode.Parse(payload!.BodyJson)!.AsObject();
        return body["data"]!.AsObject();
    }

    /// <summary>Locates the shipped manifest, so the test reads what deployment reads.</summary>
    private static string ResolveManifestPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; current is not null && depth < 8; depth++)
        {
            var candidate = Path.Combine(
                current.FullName,
                "custom",
                "static-plugins",
                "Communication",
                "registry.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("Communication registry.json was not found from the test base directory.");
    }

    private sealed class SingleScopeFactory(IWebhookSubscriptionStore store) : IServiceScopeFactory, IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = new SingleServiceProvider(store);

        public IServiceScope CreateScope() => this;

        public void Dispose()
        {
        }

        private sealed class SingleServiceProvider(IWebhookSubscriptionStore store) : IServiceProvider
        {
            public object? GetService(Type serviceType) =>
                serviceType == typeof(IWebhookSubscriptionStore) ? store : null;
        }
    }
}

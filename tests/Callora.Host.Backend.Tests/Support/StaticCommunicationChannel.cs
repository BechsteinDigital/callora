using Callora.Contracts.Communication;

namespace Callora.Host.Backend.Tests.Support;

/// <summary>
/// Contract-only fake channel without any protocol backing.
/// </summary>
public sealed class StaticCommunicationChannel : ICommunicationChannel
{
    private readonly List<StaticCall> _placedCalls = [];

    public StaticCommunicationChannel(
        string channelId,
        string pluginId = "test-plugin",
        string? displayName = null,
        IReadOnlyCollection<string>? capabilities = null)
    {
        ChannelId = channelId;
        PluginId = pluginId;
        DisplayName = displayName ?? channelId;
        Capabilities = capabilities ?? [CommunicationCapabilities.Voice];
    }

    public string ChannelId { get; }

    public string DisplayName { get; }

    public string PluginId { get; }

    public IReadOnlyCollection<string> Capabilities { get; }

    public IReadOnlyList<StaticCall> PlacedCalls => _placedCalls;

    public Task<ICall> PlaceCallAsync(CallTarget target, CancellationToken cancellationToken = default)
    {
        var call = new StaticCall(target);
        _placedCalls.Add(call);
        return Task.FromResult<ICall>(call);
    }
}

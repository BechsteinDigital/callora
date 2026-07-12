using System.Collections.Concurrent;
using Callora.Contracts.Communication;

namespace Callora.Host.Backend.Application.Communication.Calls;

/// <summary>
/// Observes channel registrations and feeds every inbound call into the
/// <see cref="ActiveCallRegistry"/>, making it visible to API and SSE consumers.
/// </summary>
public sealed class IncomingCallMonitor(
    CommunicationChannelRegistry channelRegistry,
    ActiveCallRegistry callRegistry) : IHostedService
{
    private readonly ConcurrentDictionary<ICommunicationChannel, EventHandler<IncomingCallEventArgs>> _handlers = new();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        channelRegistry.ChannelRegistered += AttachChannel;
        channelRegistry.ChannelUnregistered += DetachChannel;

        // Channels registered before this service started (plugin rehydration
        // order is not guaranteed) are attached from the snapshot.
        foreach (var (workspaceKey, channel) in channelRegistry.GetAllRegistrations())
        {
            AttachChannel(workspaceKey, channel);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        channelRegistry.ChannelRegistered -= AttachChannel;
        channelRegistry.ChannelUnregistered -= DetachChannel;

        foreach (var channel in _handlers.Keys.ToArray())
        {
            DetachChannel(string.Empty, channel);
        }

        return Task.CompletedTask;
    }

    private void AttachChannel(string workspaceKey, ICommunicationChannel channel)
    {
        EventHandler<IncomingCallEventArgs> handler = (_, args) =>
            callRegistry.TrackIncoming(workspaceKey, channel.ChannelId, args.Call);

        if (_handlers.TryAdd(channel, handler))
        {
            channel.IncomingCall += handler;
        }
    }

    private void DetachChannel(string workspaceKey, ICommunicationChannel channel)
    {
        if (_handlers.TryRemove(channel, out var handler))
        {
            channel.IncomingCall -= handler;
        }
    }
}

using Callora.Contracts.Communication;
using Callora.Plugins.Voip.Application.Accounts;

namespace Callora.Plugins.Voip.Application.Channels;

/// <summary>
/// Platform communication channel backed by one SIP account.
/// </summary>
public sealed class SipCommunicationChannel(SipAccountEntry account, IVoiceEngine engine) : ICommunicationChannel
{
    public string ChannelId => account.SipAccountId;

    public string DisplayName => account.DisplayName;

    public string PluginId => VoipPlugin.Id;

    public IReadOnlyCollection<string> Capabilities { get; } = [CommunicationCapabilities.Voice];

    public event EventHandler<IncomingCallEventArgs>? IncomingCall;

    public async Task<ICall> PlaceCallAsync(CallTarget target, CancellationToken cancellationToken = default)
    {
        var engineCall = await engine.PlaceCallAsync(account, target, cancellationToken).ConfigureAwait(false);
        return new SipCall(engineCall, target);
    }

    /// <summary>
    /// Surfaces one inbound engine call to subscribers. Wired per active
    /// account by <see cref="SipChannelManager"/>.
    /// </summary>
    internal void HandleIncomingEngineCall(IEngineCall engineCall)
    {
        ArgumentNullException.ThrowIfNull(engineCall);

        var call = new SipCall(engineCall, new CallTarget(engineCall.RemoteParty));
        IncomingCall?.Invoke(this, new IncomingCallEventArgs(call));
    }
}

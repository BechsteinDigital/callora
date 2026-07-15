using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Audio;
using Callora.Plugin.Communication.Application.Channels;
using SdkCallDirection = CalloraVoipSdk.Core.Domain.Calls.CallDirection;
using SdkCallState = CalloraVoipSdk.Core.Domain.Calls.CallState;

namespace Callora.Host.Backend.Tests.Support;

/// <summary>
/// Controllable engine call for adapter tests; no SIP stack involved.
/// </summary>
public sealed class FakeEngineCall : IEngineCall
{
    private readonly List<char> _sentDtmfTones = [];

    public FakeEngineCall(SdkCallState initialState = SdkCallState.Dialing)
    {
        State = initialState;
    }

    public string CallId { get; } = Guid.NewGuid().ToString("N");

    public SdkCallState State { get; private set; }

    public SdkCallDirection Direction { get; init; } = SdkCallDirection.Outbound;

    public string RemoteParty { get; init; } = "sip:caller@voice.example.org";

    public int HangupCallCount { get; private set; }

    public int AcceptCallCount { get; private set; }

    public int RejectCallCount { get; private set; }

    public IReadOnlyList<char> SentDtmfTones => _sentDtmfTones;

    public event Action<SdkCallState>? StateChanged;

    public Task AcceptAsync(CancellationToken cancellationToken = default)
    {
        AcceptCallCount++;
        RaiseState(SdkCallState.Connected);
        return Task.CompletedTask;
    }

    public Task RejectAsync(CancellationToken cancellationToken = default)
    {
        RejectCallCount++;
        RaiseState(SdkCallState.Terminated);
        return Task.CompletedTask;
    }

    public Task HangupAsync(CancellationToken cancellationToken = default)
    {
        HangupCallCount++;
        RaiseState(SdkCallState.Terminated);
        return Task.CompletedTask;
    }

    public Task SendDtmfAsync(char tone, CancellationToken cancellationToken = default)
    {
        _sentDtmfTones.Add(tone);
        return Task.CompletedTask;
    }

    public Task<ICallAudioStream> OpenAudioAsync(CancellationToken cancellationToken = default)
    {
        OpenedAudioStream = new RecordingCallAudioStream();
        return Task.FromResult<ICallAudioStream>(OpenedAudioStream);
    }

    public RecordingCallAudioStream? OpenedAudioStream { get; private set; }

    public void RaiseState(SdkCallState newState)
    {
        State = newState;
        StateChanged?.Invoke(newState);
    }
}

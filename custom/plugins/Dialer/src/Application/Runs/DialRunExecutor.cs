using Callora.Plugin.Communication.Abstractions;
using Callora.Plugins.Dialer.Application.Numbers;

namespace Callora.Plugins.Dialer.Application.Runs;

/// <summary>
/// Executes one dial run: sequentially calls every number over the first
/// voice channel of the workspace, resolved through the platform contract
/// registry — the dialer never sees SIP.
/// </summary>
public sealed class DialRunExecutor(ICommunicationChannelRegistry channelRegistry)
{
    /// <summary>
    /// Dials all numbers sequentially and returns one attempt result per number.
    /// </summary>
    public async Task<IReadOnlyList<DialAttemptResult>> ExecuteAsync(
        string workspaceKey,
        IReadOnlyList<DialNumberEntry> numbers,
        DialRunOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentNullException.ThrowIfNull(numbers);
        ArgumentNullException.ThrowIfNull(options);

        var channels = channelRegistry.GetChannelsByCapability(workspaceKey, CommunicationCapabilities.Voice);
        if (channels.Count == 0)
        {
            throw new InvalidOperationException(
                $"No voice channel is available in workspace '{workspaceKey}'.");
        }

        var channel = channels[0];
        var attempts = new List<DialAttemptResult>(numbers.Count);
        foreach (var number in numbers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempts.Add(await DialOneAsync(channel, number, options, cancellationToken).ConfigureAwait(false));
        }

        return attempts;
    }

    private static async Task<DialAttemptResult> DialOneAsync(
        ICommunicationChannel channel,
        DialNumberEntry number,
        DialRunOptions options,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        try
        {
            var call = await channel
                .PlaceCallAsync(new CallTarget(number.Number, number.DisplayName), cancellationToken)
                .ConfigureAwait(false);

            var outcome = await WaitForTerminationAsync(call, options.CallTimeout, cancellationToken).ConfigureAwait(false);
            return new DialAttemptResult(number.Number, outcome, null, startedAt, DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new DialAttemptResult(number.Number, DialAttemptOutcome.Failed, ex.Message, startedAt, DateTimeOffset.UtcNow);
        }
    }

    private static async Task<DialAttemptOutcome> WaitForTerminationAsync(
        ICall call,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var terminated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sawConnected = call.State == CallState.Connected;

        void OnStateChanged(object? sender, CallStateChangedEventArgs args)
        {
            if (args.CurrentState == CallState.Connected)
                sawConnected = true;

            if (args.CurrentState == CallState.Terminated)
                terminated.TrySetResult();
        }

        call.StateChanged += OnStateChanged;
        try
        {
            if (call.State == CallState.Terminated)
                terminated.TrySetResult();

            var timeoutTask = Task.Delay(timeout, cancellationToken);
            var completed = await Task.WhenAny(terminated.Task, timeoutTask).ConfigureAwait(false);
            if (completed != terminated.Task)
            {
                await call.HangupAsync(CancellationToken.None).ConfigureAwait(false);
                return sawConnected ? DialAttemptOutcome.Connected : DialAttemptOutcome.TimedOut;
            }

            return sawConnected ? DialAttemptOutcome.Connected : DialAttemptOutcome.NotConnected;
        }
        finally
        {
            call.StateChanged -= OnStateChanged;
        }
    }
}

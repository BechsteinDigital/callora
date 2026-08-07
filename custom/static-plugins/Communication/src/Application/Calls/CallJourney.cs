using System.Collections.Concurrent;
using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// Keeps each running call's steps until the call ends, then hands them over to be written onto its
/// history record.
/// </summary>
/// <remarks>
/// <para>In memory rather than a write per step: steps are recorded on the path handling a live call,
/// where a database round trip is a silence the caller hears. A journey is read after the fact
/// anyway, and one write at the end is the same write the history record already does.</para>
/// <para>What that costs: a call whose process dies mid-way loses its journey. The history record has
/// the same property, and inventing durability for the trail but not for the call itself would be an
/// odd place to spend it.</para>
/// </remarks>
public sealed class CallJourney : ICallJourney
{
    /// <summary>Step appended in place of everything beyond the cap, so truncation is visible.</summary>
    public const string TruncatedStep = "journey.truncated";

    /// <summary>How many steps one call may accumulate before the rest are dropped.</summary>
    public const int DefaultMaxSteps = 200;

    private readonly ConcurrentDictionary<(string WorkspaceKey, string CallId), List<CallJourneyStep>> _journeys =
        new();
    private readonly int _maxSteps;
    private readonly TimeProvider _time;

    /// <summary>Creates the journal.</summary>
    /// <param name="maxSteps">
    /// Cap per call. A flow that loops — a caller wandering a menu for an hour — must not turn one
    /// call into unbounded memory.
    /// </param>
    /// <param name="timeProvider">Clock used to stamp each step.</param>
    public CallJourney(int maxSteps = DefaultMaxSteps, TimeProvider? timeProvider = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxSteps, 1);

        _maxSteps = maxSteps;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public void Record(string workspaceKey, string callId, CallJourneyStep step)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey) || string.IsNullOrWhiteSpace(callId) || step is null)
        {
            // Recording must not throw on the call path. A malformed step is a defect in the caller,
            // not a reason to drop somebody's call.
            return;
        }

        var steps = _journeys.GetOrAdd((workspaceKey, callId), _ => []);
        lock (steps)
        {
            if (steps.Count > _maxSteps)
            {
                return;
            }

            // The cap is announced rather than applied silently: the tail is where the failure is, and
            // a journey that just stops looks like a call that just stopped.
            steps.Add(steps.Count == _maxSteps
                ? new CallJourneyStep(step.Source, TruncatedStep, "Further steps were not recorded.")
                    { At = _time.GetUtcNow() }
                : step with { At = _time.GetUtcNow() });
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<CallJourneyStep> Read(string workspaceKey, string callId)
    {
        if (!_journeys.TryGetValue((workspaceKey, callId), out var steps))
        {
            return [];
        }

        lock (steps)
        {
            return [.. steps];
        }
    }

    /// <summary>
    /// Takes a call's steps and forgets them. Called once, where the call is untracked — the one path
    /// every ending takes, so nothing is left behind for calls the process has seen.
    /// </summary>
    public IReadOnlyList<CallJourneyStep> Take(string workspaceKey, string callId)
    {
        if (!_journeys.TryRemove((workspaceKey, callId), out var steps))
        {
            return [];
        }

        lock (steps)
        {
            return [.. steps];
        }
    }
}

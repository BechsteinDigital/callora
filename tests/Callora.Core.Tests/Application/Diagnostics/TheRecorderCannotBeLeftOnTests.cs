using Callora.Core.Application.Diagnostics;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Callora.Core.Tests.Application.Diagnostics;

/// <summary>
/// A recorder that captures every query of every request is a developer tool pointed at a
/// system that matters. What makes it safe to switch on is that it switches itself off.
/// </summary>
/// <remarks>
/// Frappe's recorder does the same thing (<c>RECORDER_AUTO_DISABLE = 10 * 60</c>), and for
/// the same reason: the failure mode of a diagnostic tool is not that it is wrong, it is
/// that someone enables it during an incident and nobody remembers afterwards.
/// </remarks>
public sealed class TheRecorderCannotBeLeftOnTests
{
    [Fact]
    public void It_is_off_until_someone_turns_it_on()
    {
        var recorder = new PluginExecutionRecorder(new FakeTimeProvider());

        Assert.False(recorder.IsRecording);
    }

    [Fact]
    public void It_stops_on_its_own_when_the_window_elapses()
    {
        var time = new FakeTimeProvider();
        var recorder = new PluginExecutionRecorder(time);
        recorder.Start(new RecorderSession(Window: TimeSpan.FromMinutes(10)));

        time.Advance(TimeSpan.FromMinutes(9));
        Assert.True(recorder.IsRecording);

        time.Advance(TimeSpan.FromMinutes(1).Add(TimeSpan.FromSeconds(1)));
        Assert.False(recorder.IsRecording);
    }

    [Fact]
    public void Nothing_is_recorded_once_the_window_has_elapsed()
    {
        var time = new FakeTimeProvider();
        var recorder = new PluginExecutionRecorder(time);
        recorder.Start(new RecorderSession(Window: TimeSpan.FromMinutes(10)));
        time.Advance(TimeSpan.FromMinutes(11));

        recorder.RecordCommand("billed-plugin", "SELECT 1", TimeSpan.FromMilliseconds(5));

        Assert.Empty(recorder.Recent());
    }

    [Fact]
    public void Recording_while_off_costs_nothing_and_keeps_nothing()
    {
        var recorder = new PluginExecutionRecorder(new FakeTimeProvider());

        recorder.RecordCommand("billed-plugin", "SELECT 1", TimeSpan.FromMilliseconds(5));

        Assert.Empty(recorder.Recent());
    }

    [Fact]
    public void Stopping_is_immediate()
    {
        var recorder = new PluginExecutionRecorder(new FakeTimeProvider());
        recorder.Start(new RecorderSession(Window: TimeSpan.FromMinutes(10)));

        recorder.Stop();

        Assert.False(recorder.IsRecording);
    }

    [Fact]
    public void A_window_beyond_the_ceiling_is_clamped_rather_than_refused()
    {
        // Refusing would invite the caller to ask for the maximum every time, which teaches
        // nothing; clamping keeps the answer honest and the ceiling in one place.
        var time = new FakeTimeProvider();
        var recorder = new PluginExecutionRecorder(time);

        recorder.Start(new RecorderSession(Window: TimeSpan.FromHours(8)));
        time.Advance(PluginExecutionRecorder.MaximumWindow.Add(TimeSpan.FromSeconds(1)));

        Assert.False(recorder.IsRecording);
    }
}

using Callora.Core.Application.Diagnostics;
using Callora.Core.Infrastructure.Diagnostics;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Callora.Core.Tests.Application.Diagnostics;

/// <summary>
/// The interceptor's whole job: stamp the running plugin onto the command. Everything else
/// about it is EF Core plumbing.
/// </summary>
public sealed class CommandsAreAttributedToTheirPluginTests
{
    [Fact]
    public void A_command_issued_inside_a_plugin_scope_is_attributed_to_it()
    {
        var (recorder, interceptor) = Recording();

        using (PluginExecutionScope.Enter("billed-plugin"))
        {
            interceptor.Capture("SELECT 1", TimeSpan.FromMilliseconds(12));
        }

        var recorded = Assert.Single(recorder.Recent());
        Assert.Equal("billed-plugin", recorded.PluginId);
        Assert.Equal("SELECT 1", recorded.CommandText);
        Assert.Equal(TimeSpan.FromMilliseconds(12), recorded.Duration);
    }

    [Fact]
    public void Host_work_is_attributed_to_nobody_rather_than_guessed()
    {
        var (recorder, interceptor) = Recording();

        interceptor.Capture("SELECT 1", TimeSpan.FromMilliseconds(3));

        Assert.Null(Assert.Single(recorder.Recent()).PluginId);
    }

    [Fact]
    public void Filtering_to_one_plugin_drops_the_others()
    {
        var recorder = new PluginExecutionRecorder(new FakeTimeProvider());
        recorder.Start(new RecorderSession(TimeSpan.FromMinutes(5), PluginId: "wanted"));
        var interceptor = new RecordingDbCommandInterceptor(recorder);

        using (PluginExecutionScope.Enter("wanted"))
        {
            interceptor.Capture("SELECT 'kept'", TimeSpan.FromMilliseconds(1));
        }

        using (PluginExecutionScope.Enter("other"))
        {
            interceptor.Capture("SELECT 'dropped'", TimeSpan.FromMilliseconds(1));
        }

        // A busy host fills the ring in seconds; without the filter the request under
        // investigation has already been pushed out by the time anyone reads it.
        Assert.Equal("SELECT 'kept'", Assert.Single(recorder.Recent()).CommandText);
    }

    [Fact]
    public void Nothing_is_captured_while_the_recorder_is_off()
    {
        var recorder = new PluginExecutionRecorder(new FakeTimeProvider());
        var interceptor = new RecordingDbCommandInterceptor(recorder);

        using (PluginExecutionScope.Enter("billed-plugin"))
        {
            interceptor.Capture("SELECT 1", TimeSpan.FromMilliseconds(12));
        }

        Assert.Empty(recorder.Recent());
    }

    private static (PluginExecutionRecorder Recorder, RecordingDbCommandInterceptor Interceptor) Recording()
    {
        var recorder = new PluginExecutionRecorder(new FakeTimeProvider());
        recorder.Start(new RecorderSession(TimeSpan.FromMinutes(5)));
        return (recorder, new RecordingDbCommandInterceptor(recorder));
    }
}

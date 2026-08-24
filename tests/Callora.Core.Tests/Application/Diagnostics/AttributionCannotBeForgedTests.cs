using Callora.Core.Application.Diagnostics;
using Callora.Core.Extensibility;
using System.Reflection;
using Xunit;

namespace Callora.Core.Tests.Application.Diagnostics;

/// <summary>
/// The recorder's answer is only worth something if a plugin cannot write it.
/// </summary>
/// <remarks>
/// <para>
/// Found reviewing the recorder after writing it. <c>PluginExecutionScope.Enter</c> is
/// public and carried no governance marker, so a plugin could call
/// <c>Enter("some-other-plugin")</c> and have its own database work filed under a
/// neighbour. That defeats the recorder precisely where it is meant to be used — a support
/// case, with a customer waiting and several foreign plugins in one process.
/// </para>
/// <para>
/// Under ADR-013 the marker plus CAL0001 is the available hardness: it turns forging
/// attribution from something a plugin can do by accident into a deliberate breach of a
/// build-time rule. That is the same guarantee the rest of the internal surface has, and it
/// is the guarantee this surface was missing.
/// </para>
/// </remarks>
public sealed class AttributionCannotBeForgedTests
{
    [Theory]
    [InlineData(typeof(PluginExecutionScope))]
    [InlineData(typeof(PluginExecutionRecorder))]
    [InlineData(typeof(RecorderSession))]
    [InlineData(typeof(RecordedCommand))]
    public void EveryDiagnosticsTypeIsMarkedInternalToTheFramework(Type type)
    {
        Assert.True(
            type.GetCustomAttribute<CalloraInternalAttribute>(inherit: false) is not null,
            $"{type.Name} is public without [CalloraInternal], so a plugin may consume it and CAL0001 stays silent.");
    }

    [Fact]
    public void TheInterceptorIsMarkedToo()
    {
        var interceptor = typeof(Callora.Core.Infrastructure.Diagnostics.RecordingDbCommandInterceptor);

        Assert.NotNull(interceptor.GetCustomAttribute<CalloraInternalAttribute>(inherit: false));
    }
}

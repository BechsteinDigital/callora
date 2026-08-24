using Callora.Analyzers;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Callora.Analyzers.Tests;

/// <summary>
/// CAL0005 — the middle rung, delivered where it is useful: in the plugin author's own
/// build, in their own repository, at their own pace. Before it, the first news of a
/// removal was a plugin that would not load.
/// </summary>
public sealed class CalloraDeprecatedConsumptionAnalyzerTests
{
    private const string FrameworkSource = """
        #nullable enable
        namespace Callora.Core.Extensibility
        {
            [System.AttributeUsage(System.AttributeTargets.All, Inherited = false, AllowMultiple = false)]
            public sealed class CalloraDeprecatedAttribute : System.Attribute
            {
                public CalloraDeprecatedAttribute(string since, string errorsIn) { Since = since; ErrorsIn = errorsIn; }
                public string Since { get; }
                public string ErrorsIn { get; }
                public string? Replacement { get; init; }
            }
        }
        namespace Framework
        {
            [Callora.Core.Extensibility.CalloraDeprecated("0.9.2", "v3", Replacement = "ISurfaceSource")]
            public interface ILayoutSource { string Load(); }

            public interface IKeptSource { string Load(); }

            public sealed class Registry
            {
                [Callora.Core.Extensibility.CalloraDeprecated("0.9.2", "v3", Replacement = "Registry.Attach")]
                public void Register() { }

                public void Attach() { }
            }
        }
        """;

    [Fact]
    public async Task Calling_a_deprecated_member_warns()
    {
        const string consumer = """
            namespace Plugin
            {
                public static class Caller
                {
                    public static void Go() { new Framework.Registry().Register(); }
                }
            }
            """;

        var diagnostics = await RunAsync(consumer, frameworkAssembly: false);

        var single = Assert.Single(diagnostics);
        Assert.Equal(CalloraDeprecatedConsumptionAnalyzer.DiagnosticId, single.Id);
        Assert.Equal(Microsoft.CodeAnalysis.DiagnosticSeverity.Warning, single.Severity);
    }

    [Fact]
    public async Task The_warning_carries_the_replacement_and_the_deadline()
    {
        // A deprecation without these tells an author their code is doomed and not what to
        // do about it, which is how a warning becomes noise someone suppresses.
        const string consumer = """
            namespace Plugin
            {
                public static class Caller
                {
                    public static void Go() { new Framework.Registry().Register(); }
                }
            }
            """;

        var message = Assert.Single(await RunAsync(consumer, frameworkAssembly: false)).GetMessage();

        Assert.Contains("Register", message, StringComparison.Ordinal);
        Assert.Contains("v3", message, StringComparison.Ordinal);
        Assert.Contains("Registry.Attach", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Implementing_a_deprecated_interface_warns()
    {
        // The case that matters most for plugins: they implement contracts far more often
        // than they call them, so a rule watching only call sites would miss the majority.
        const string consumer = """
            namespace Plugin
            {
                public sealed class MyLayouts : Framework.ILayoutSource
                {
                    public string Load() => "x";
                }
            }
            """;

        var diagnostics = await RunAsync(consumer, frameworkAssembly: false);

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("ILayoutSource", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_undeprecated_member_is_silent()
    {
        const string consumer = """
            namespace Plugin
            {
                public sealed class MyLayouts : Framework.IKeptSource
                {
                    public string Load() { new Framework.Registry().Attach(); return "x"; }
                }
            }
            """;

        Assert.Empty(await RunAsync(consumer, frameworkAssembly: false));
    }

    [Fact]
    public async Task The_framework_itself_is_silent()
    {
        // The platform implements and calls its own deprecated surface for as long as it
        // ships it. Warning there would flood the host build and train everyone to ignore
        // the rule before a single plugin author ever sees it.
        const string consumer = """
            namespace Framework.More
            {
                public sealed class HostImplementation : Framework.ILayoutSource
                {
                    public string Load() { new Framework.Registry().Register(); return "x"; }
                }
            }
            """;

        Assert.Empty(await RunAsync(consumer, frameworkAssembly: true));
    }

    private static async Task<System.Collections.Immutable.ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>> RunAsync(
        string consumer,
        bool frameworkAssembly)
    {
        var reference = AnalyzerTestHarness.CompileReference(FrameworkSource, "Callora.Fake.Framework");
        return await AnalyzerTestHarness
            .RunDeprecationAsync(consumer, frameworkAssembly, reference)
            .ConfigureAwait(false);
    }
}

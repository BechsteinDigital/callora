using System.Linq;
using System.Threading.Tasks;
using Callora.Analyzers;
using Xunit;

namespace Callora.Analyzers.Tests;

public sealed class CalloraInternalConsumptionAnalyzerTests
{
    /// <summary>
    /// A stand-in framework assembly: declares the real marker attribute (matched by
    /// metadata name) plus a mix of marked and unmarked public API.
    /// </summary>
    private const string FrameworkSource = """
        #nullable enable
        namespace Callora.Core.Extensibility
        {
            [System.AttributeUsage(System.AttributeTargets.All, Inherited = false, AllowMultiple = false)]
            public sealed class CalloraInternalAttribute : System.Attribute
            {
                public CalloraInternalAttribute() { }
                public CalloraInternalAttribute(string reason) { Reason = reason; }
                public string? Reason { get; }
            }
        }
        namespace Framework
        {
            [Callora.Core.Extensibility.CalloraInternal("enforcement path")]
            public interface ISecretStore { string Read(string key); }

            public interface IPublicService { void Go(); }

            [Callora.Core.Extensibility.CalloraInternal]
            public sealed class InternalThing
            {
                public int Value;
                public void Do() { }
            }

            [Callora.Core.Extensibility.CalloraInternal("internal base")]
            public abstract class InternalBase { }

            public abstract class PublicBase { }

            public sealed class PublicThing
            {
                [Callora.Core.Extensibility.CalloraInternal("member-level")]
                public void Secret() { }
                public void Fine() { }
            }
        }
        """;

    private static string Framework() => FrameworkSource;

    [Fact]
    public async Task Injecting_a_marked_service_is_reported()
    {
        const string consumer = """
            namespace Plugin
            {
                public sealed class Handler
                {
                    private readonly Framework.ISecretStore _store;
                    public Handler(Framework.ISecretStore store) { _store = store; }
                    public string Run(string key) => _store.Read(key);
                }
            }
            """;

        var reference = AnalyzerTestHarness.CompileReference(Framework(), "Callora.Fake.Framework");
        var diagnostics = await AnalyzerTestHarness.RunAsync(consumer, frameworkAssembly: false, reference);

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d => Assert.Equal(CalloraInternalConsumptionAnalyzer.DiagnosticId, d.Id));
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("ISecretStore"));
    }

    [Fact]
    public async Task Invoking_a_marked_member_is_reported_once()
    {
        const string consumer = """
            namespace Plugin
            {
                public static class Caller
                {
                    public static void Do() { new Framework.PublicThing().Secret(); }
                }
            }
            """;

        var reference = AnalyzerTestHarness.CompileReference(Framework(), "Callora.Fake.Framework");
        var diagnostics = await AnalyzerTestHarness.RunAsync(consumer, frameworkAssembly: false, reference);

        var single = Assert.Single(diagnostics);
        Assert.Contains("Secret", single.GetMessage());
        Assert.Contains("member-level", single.GetMessage());
    }

    [Fact]
    public async Task Constructing_a_marked_type_is_reported()
    {
        const string consumer = """
            namespace Plugin
            {
                public static class Maker
                {
                    public static object Make() => new Framework.InternalThing();
                }
            }
            """;

        var reference = AnalyzerTestHarness.CompileReference(Framework(), "Callora.Fake.Framework");
        var diagnostics = await AnalyzerTestHarness.RunAsync(consumer, frameworkAssembly: false, reference);

        var single = Assert.Single(diagnostics);
        Assert.Contains("InternalThing", single.GetMessage());
    }

    [Fact]
    public async Task Constructing_a_collection_of_a_marked_type_is_reported()
    {
        // Generic type argument in operation position: the constructed type (List) is
        // not marked, but its argument is — the analyzer must unwrap it.
        const string consumer = """
            namespace Plugin
            {
                public static class Maker
                {
                    public static object Make() => new System.Collections.Generic.List<Framework.InternalThing>();
                }
            }
            """;

        var reference = AnalyzerTestHarness.CompileReference(Framework(), "Callora.Fake.Framework");
        var diagnostics = await AnalyzerTestHarness.RunAsync(consumer, frameworkAssembly: false, reference);

        var single = Assert.Single(diagnostics);
        Assert.Contains("InternalThing", single.GetMessage());
    }

    [Fact]
    public async Task Implementing_a_marked_interface_is_reported_as_CAL0002()
    {
        const string consumer = """
            namespace Plugin
            {
                public sealed class MyStore : Framework.ISecretStore
                {
                    public string Read(string key) => key;
                }
            }
            """;

        var reference = AnalyzerTestHarness.CompileReference(Framework(), "Callora.Fake.Framework");
        var diagnostics = await AnalyzerTestHarness.RunAsync(consumer, frameworkAssembly: false, reference);

        var single = Assert.Single(diagnostics);
        Assert.Equal(CalloraInternalConsumptionAnalyzer.InheritanceDiagnosticId, single.Id);
        Assert.Contains("ISecretStore", single.GetMessage());
    }

    [Fact]
    public async Task Deriving_from_a_marked_base_is_reported_as_CAL0002()
    {
        const string consumer = """
            namespace Plugin
            {
                public sealed class MyThing : Framework.InternalBase { }
            }
            """;

        var reference = AnalyzerTestHarness.CompileReference(Framework(), "Callora.Fake.Framework");
        var diagnostics = await AnalyzerTestHarness.RunAsync(consumer, frameworkAssembly: false, reference);

        var single = Assert.Single(diagnostics);
        Assert.Equal(CalloraInternalConsumptionAnalyzer.InheritanceDiagnosticId, single.Id);
        Assert.Contains("InternalBase", single.GetMessage());
    }

    [Fact]
    public async Task Implementing_a_public_interface_or_deriving_a_public_base_is_clean()
    {
        const string consumer = """
            namespace Plugin
            {
                public sealed class Ok : Framework.PublicBase, Framework.IPublicService
                {
                    public void Go() { }
                }
            }
            """;

        var reference = AnalyzerTestHarness.CompileReference(Framework(), "Callora.Fake.Framework");
        var diagnostics = await AnalyzerTestHarness.RunAsync(consumer, frameworkAssembly: false, reference);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Implementing_your_own_internal_interface_in_the_same_assembly_is_allowed()
    {
        const string selfContained = """
            #nullable enable
            namespace Callora.Core.Extensibility
            {
                [System.AttributeUsage(System.AttributeTargets.All)]
                public sealed class CalloraInternalAttribute : System.Attribute
                {
                    public CalloraInternalAttribute() { }
                    public CalloraInternalAttribute(string reason) { Reason = reason; }
                    public string? Reason { get; }
                }
            }
            namespace Own
            {
                [Callora.Core.Extensibility.CalloraInternal]
                public interface IMyInternal { void Do(); }

                public sealed class Impl : IMyInternal { public void Do() { } }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunAsync(selfContained, frameworkAssembly: false);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Implementing_a_marked_extensible_interface_is_clean()
    {
        // An extensible interface carries [CalloraExtensible], not [CalloraInternal],
        // so implementing it from a plugin is exactly what it is for.
        const string framework = """
            #nullable enable
            namespace Callora.Core.Extensibility
            {
                [System.AttributeUsage(System.AttributeTargets.All)]
                public sealed class CalloraInternalAttribute : System.Attribute { }
                [System.AttributeUsage(System.AttributeTargets.All)]
                public sealed class CalloraExtensibleAttribute : System.Attribute { }
            }
            namespace Ext
            {
                [Callora.Core.Extensibility.CalloraExtensible]
                public interface IContributor { void Contribute(); }
            }
            """;
        const string consumer = """
            namespace Plugin
            {
                public sealed class MyContributor : Ext.IContributor
                {
                    public void Contribute() { }
                }
            }
            """;

        var reference = AnalyzerTestHarness.CompileReference(framework, "Callora.Fake.Ext");
        var diagnostics = await AnalyzerTestHarness.RunAsync(consumer, frameworkAssembly: false, reference);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Framework_assemblies_may_implement_the_internal_surface()
    {
        const string consumer = """
            namespace Framework.Internals
            {
                public sealed class MyStore : Framework.ISecretStore
                {
                    public string Read(string key) => key;
                }
            }
            """;

        var reference = AnalyzerTestHarness.CompileReference(Framework(), "Callora.Fake.Framework");
        var diagnostics = await AnalyzerTestHarness.RunAsync(consumer, frameworkAssembly: true, reference);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Consuming_only_public_api_is_clean()
    {
        const string consumer = """
            namespace Plugin
            {
                public sealed class Ok
                {
                    private readonly Framework.IPublicService _svc;
                    public Ok(Framework.IPublicService svc) { _svc = svc; }
                    public void Run()
                    {
                        _svc.Go();
                        new Framework.PublicThing().Fine();
                    }
                }
            }
            """;

        var reference = AnalyzerTestHarness.CompileReference(Framework(), "Callora.Fake.Framework");
        var diagnostics = await AnalyzerTestHarness.RunAsync(consumer, frameworkAssembly: false, reference);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Framework_assemblies_may_consume_the_internal_surface()
    {
        const string consumer = """
            namespace Framework.Internals
            {
                public sealed class Handler
                {
                    private readonly Framework.ISecretStore _store;
                    public Handler(Framework.ISecretStore store) { _store = store; }
                    public string Run(string key) => _store.Read(key);
                }
            }
            """;

        var reference = AnalyzerTestHarness.CompileReference(Framework(), "Callora.Fake.Framework");
        var diagnostics = await AnalyzerTestHarness.RunAsync(consumer, frameworkAssembly: true, reference);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Consuming_your_own_internal_type_in_the_same_assembly_is_allowed()
    {
        const string selfContained = """
            #nullable enable
            namespace Callora.Core.Extensibility
            {
                [System.AttributeUsage(System.AttributeTargets.All)]
                public sealed class CalloraInternalAttribute : System.Attribute
                {
                    public CalloraInternalAttribute() { }
                    public CalloraInternalAttribute(string reason) { Reason = reason; }
                    public string? Reason { get; }
                }
            }
            namespace Own
            {
                [Callora.Core.Extensibility.CalloraInternal]
                public sealed class MyInternal { public void Do() { } }

                public static class User
                {
                    public static void Use() { new MyInternal().Do(); }
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunAsync(selfContained, frameworkAssembly: false);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Without_the_marker_attribute_nothing_is_reported()
    {
        const string consumer = """
            namespace Plugin
            {
                public static class Caller
                {
                    public static int Do() => "x".Length;
                }
            }
            """;

        // No framework reference at all → marker type is absent → analyzer is inert.
        var diagnostics = await AnalyzerTestHarness.RunAsync(consumer, frameworkAssembly: false);

        Assert.Empty(diagnostics);
    }
}

using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Callora.Analyzers.Tests;

/// <summary>
/// CAL0004 locks the extension-point-id guarantee: an argument to an
/// <c>[ExtensionPointId]</c> parameter must reference a CalloraExtensionPoints constant,
/// so a raw or mistyped id fails at compile time. A constant reference and a dynamic value
/// are allowed; a literal on an unmarked parameter is out of scope.
/// </summary>
public sealed class CalloraExtensionPointIdAnalyzerTests
{
    // The real marker + constants (matched by metadata name) and a type with a marked
    // parameter that a caller passes an id to.
    private const string Prelude = """
        #nullable enable
        namespace Callora.Core.Extensibility
        {
            [System.AttributeUsage(System.AttributeTargets.Parameter)]
            public sealed class ExtensionPointIdAttribute : System.Attribute { }
        }
        namespace Callora.Core.Domain.Extensions
        {
            public static class CalloraExtensionPoints
            {
                public const string WorkspaceNavigationMain = "workspace.navigation.main";
                public const string AdminApiRoute = "admin.api.route";
            }
        }
        namespace App
        {
            public sealed record Registration(
                [Callora.Core.Extensibility.ExtensionPointId] string ExtensionPointId,
                string Surface);
        }
        """;

    [Fact]
    public async Task Known_id_as_literal_is_reported()
    {
        var source = Prelude + """
            namespace App
            {
                public sealed class Use
                {
                    public object Make() => new Registration("workspace.navigation.main", "workspace");
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunExtensionPointIdAsync(source);

        Assert.Single(diagnostics);
        Assert.Equal("CAL0004", diagnostics[0].Id);
        Assert.Contains("constant", diagnostics[0].GetMessage());
    }

    [Fact]
    public async Task Unknown_id_as_literal_is_reported()
    {
        var source = Prelude + """
            namespace App
            {
                public sealed class Use
                {
                    public object Make() => new Registration("workspace.navigatoin.main", "workspace");
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunExtensionPointIdAsync(source);

        Assert.Single(diagnostics);
        Assert.Contains("not a known", diagnostics[0].GetMessage());
    }

    [Fact]
    public async Task Constant_reference_passes()
    {
        var source = Prelude + """
            namespace App
            {
                public sealed class Use
                {
                    public object Make() => new Registration(
                        Callora.Core.Domain.Extensions.CalloraExtensionPoints.WorkspaceNavigationMain,
                        "workspace");
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunExtensionPointIdAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Dynamic_value_is_allowed()
    {
        var source = Prelude + """
            namespace App
            {
                public sealed class Use
                {
                    public object Make(string id) => new Registration(id, "workspace");
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunExtensionPointIdAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Literal_on_unmarked_parameter_is_ignored()
    {
        // The surface argument is a raw literal but its parameter is not [ExtensionPointId].
        var source = Prelude + """
            namespace App
            {
                public sealed class Use
                {
                    public object Make() => new Registration(
                        Callora.Core.Domain.Extensions.CalloraExtensionPoints.AdminApiRoute,
                        "some-raw-surface");
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunExtensionPointIdAsync(source);

        Assert.Empty(diagnostics);
    }
}

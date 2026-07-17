using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Callora.Analyzers.Tests;

/// <summary>
/// CAL0003 locks the plugin-contract documentation guarantee: public types and members
/// on the contract surface (namespaces ending in .Contracts, Extensibility types,
/// [CalloraExtensible] members) must carry XML docs, while the internal public surface
/// stays out of scope.
/// </summary>
public sealed class CalloraContractDocumentationAnalyzerTests
{
    // The real [CalloraExtensible] marker, matched by metadata name. Fully documented so
    // it does not itself trip CAL0003 (it lives in an Extensibility namespace, i.e. on the
    // contract surface) and pollute every test's diagnostic count.
    private const string MarkerSource = """
        #nullable enable
        namespace Callora.Core.Extensibility
        {
            /// <summary>Marks a sanctioned plugin extension point.</summary>
            [System.AttributeUsage(System.AttributeTargets.All, Inherited = false, AllowMultiple = false)]
            public sealed class CalloraExtensibleAttribute : System.Attribute
            {
                /// <summary>Marks the target as extensible.</summary>
                public CalloraExtensibleAttribute() { }
                /// <summary>Marks the target as extensible, with a rationale.</summary>
                public CalloraExtensibleAttribute(string reason) { Reason = reason; }
                /// <summary>Optional rationale for the extension point.</summary>
                public string? Reason { get; }
            }
        }
        """;

    [Fact]
    public async Task Undocumented_contract_interface_and_method_are_reported()
    {
        var source = MarkerSource + """
            namespace App.Jobs.Contracts
            {
                public interface IJobHandler
                {
                    void Handle();
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunDocumentationAsync(source);

        Assert.Equal(2, diagnostics.Length); // the interface + its method
        Assert.All(diagnostics, d => Assert.Equal("CAL0003", d.Id));
    }

    [Fact]
    public async Task Documented_contract_interface_and_method_pass()
    {
        var source = MarkerSource + """
            namespace App.Jobs.Contracts
            {
                /// <summary>Handles one job type.</summary>
                public interface IJobHandler
                {
                    /// <summary>Runs the job.</summary>
                    void Handle();
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunDocumentationAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Undocumented_type_outside_the_contract_surface_is_ignored()
    {
        var source = MarkerSource + """
            namespace App.Infrastructure
            {
                public sealed class JobRepository
                {
                    public void Save() { }
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunDocumentationAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Extensible_marked_type_requires_documentation()
    {
        var source = MarkerSource + """
            namespace App.Runtime
            {
                [Callora.Core.Extensibility.CalloraExtensible("extension point")]
                public interface IPluginMigration
                {
                    void Up();
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunDocumentationAsync(source);

        // The marked type is on the surface; the type and its method both need docs.
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("CAL0003", d.Id));
    }

    [Fact]
    public async Task Extensibility_namespace_requires_documentation()
    {
        var source = MarkerSource + """
            namespace App.Core.Extensibility
            {
                public sealed class HostProtectedAttribute : System.Attribute { }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunDocumentationAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "CAL0003");
    }

    [Fact]
    public async Task Positional_record_documented_with_param_tags_passes()
    {
        var source = MarkerSource + """
            namespace App.Media.Contracts
            {
                /// <summary>Asset metadata.</summary>
                /// <param name="Id">Stable id.</param>
                /// <param name="Name">File name.</param>
                public sealed record MediaAssetInfo(System.Guid Id, string Name);
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunDocumentationAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Inheritdoc_counts_as_documentation()
    {
        var source = MarkerSource + """
            namespace App.Events.Contracts
            {
                /// <summary>Base marker.</summary>
                public interface IEvent { }

                /// <inheritdoc/>
                public interface IDomainEvent : IEvent { }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunDocumentationAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Non_public_contract_type_is_ignored()
    {
        var source = MarkerSource + """
            namespace App.Jobs.Contracts
            {
                internal interface IInternalJobHandler
                {
                    void Handle();
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunDocumentationAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Undocumented_enum_members_on_the_surface_are_reported()
    {
        var source = MarkerSource + """
            namespace App.Plugins.Contracts
            {
                /// <summary>Plugin state.</summary>
                public enum PluginState
                {
                    Installed,
                    Active,
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunDocumentationAsync(source);

        // Two undocumented enum members; the type itself is documented.
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("CAL0003", d.Id));
    }

    [Fact]
    public async Task Property_accessors_are_not_double_reported()
    {
        var source = MarkerSource + """
            namespace App.Jobs.Contracts
            {
                /// <summary>A job descriptor.</summary>
                public interface IJobDescriptor
                {
                    string Type { get; }
                }
            }
            """;

        var diagnostics = await AnalyzerTestHarness.RunDocumentationAsync(source);

        // Only the undocumented property — never its generated getter separately.
        Assert.Single(diagnostics);
        Assert.Contains("Type", diagnostics[0].GetMessage());
    }
}

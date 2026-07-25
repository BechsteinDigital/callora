using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Callora.Core.Extensibility;
using Callora.Core.Tests.Cli;
using Xunit;

namespace Callora.Core.Tests.Documentation;

/// <summary>
/// Keeps the published extension-point catalog honest: every type the platform marks
/// <see cref="CalloraExtensibleAttribute"/> must appear in docs-site/developers/extension-points.md.
/// Adding a new extension point without documenting it fails the build — so the developer-facing
/// reference can never silently fall behind the code (the whole promise of the catalog).
/// </summary>
public sealed class ExtensionPointCatalogCompletenessTests
{
    [Fact]
    public void EveryExtensionPoint_IsListedInTheCatalog()
    {
        var catalog = File.ReadAllText(CatalogPath());

        var undocumented = typeof(CalloraExtensibleAttribute).Assembly
            .GetExportedTypes()
            .Where(type => type.GetCustomAttribute<CalloraExtensibleAttribute>() is not null)
            .Select(BaseName)
            .Distinct(StringComparer.Ordinal)
            .Where(name => !catalog.Contains(name, StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            undocumented.Length == 0,
            "Extension points marked [CalloraExtensible] but missing from " +
            $"docs-site/developers/extension-points.md: {string.Join(", ", undocumented)}");
    }

    // Generic contracts surface as e.g. "IServiceDecorator`1"; the catalog lists the base name.
    private static string BaseName(Type type)
    {
        var name = type.Name;
        var tick = name.IndexOf('`', StringComparison.Ordinal);
        return tick < 0 ? name : name[..tick];
    }

    private static string CatalogPath() =>
        Path.Combine(
            ScaffoldedPluginFixture.ResolveRepositoryRoot(),
            "docs-site", "developers", "extension-points.md");
}

using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Callora.Core.Application.Jobs;
using Callora.Core.Application.Monitoring;
using Callora.Core.Application.Options;
using Callora.Core.Application.Policies;
using Callora.Core.Application.Retention;
using Callora.Core.Application.Surfaces;
using Callora.Core.Tests.Cli;
using Xunit;

namespace Callora.Core.Tests.Documentation;

/// <summary>
/// Hält die Dokumentation ehrlich über die Konfiguration, die sie beschreibt: Jeder genannte
/// Schlüssel muss existieren, und jeder genannte Vorgabewert muss der im Code sein.
/// </summary>
/// <remarks>
/// <para>
/// Der Anlass war ein Betriebs-Runbook, das den Shutdown mit einem „5-Sekunden-Budget"
/// beschrieb, während <c>PluginDrainTimeout</c> längst auf 30 Sekunden stand — und an anderer
/// Stelle ein manuelles SQL empfahl, das die Lease-Logik aushebelte und laufende Jobs ein
/// zweites Mal ausführte. Beides stand über Monate da, weil nichts es prüfen konnte: Ein
/// Compiler liest kein Markdown, und ein Review sieht die Zahl, ohne sie nachzuschlagen.
/// </para>
/// <para>
/// Die beiden Prüfungen fangen unterschiedliche Fehler. Die erste ist vollautomatisch und
/// fängt den häufigsten Fall — ein Schlüssel wird umbenannt oder entfernt, die Dokumentation
/// nennt ihn weiter. Die zweite ist kuratiert, weil sich aus Fließtext nicht zuverlässig
/// ableiten lässt, welche Zahl zu welchem Schlüssel gehört: Wer einen Vorgabewert
/// dokumentiert, bindet ihn hier an sein Feld. Das ist etwas Handarbeit an genau der Stelle,
/// an der sie sich lohnt.
/// </para>
/// </remarks>
public sealed class DocumentedConfigurationKeysTests
{
    private static readonly (string Section, Type Options)[] Sections =
    [
        ("CalloraHosting", typeof(CalloraHostingOptions)),
        ("BackendHost", typeof(BackendHostOptions)),
        ("BackgroundJobs", typeof(BackgroundJobOptions)),
        ("Retention", typeof(RetentionOptions)),
        ("Observability", typeof(ObservabilityOptions)),
        ("Callora:SurfaceApi", typeof(SurfaceApiOptions)),
        ("Callora:SurfaceIdentity", typeof(SurfaceIdentityOptions)),
    ];

    /// <summary>
    /// Dokumentierte Vorgabewerte, gebunden an ihr Feld. Wer eine Zahl in die Dokumentation
    /// schreibt, trägt sie hier ein — sonst darf sie dort nicht stehen.
    /// </summary>
    private static readonly (string Document, string Key, string Expected)[] DocumentedDefaults =
    [
        ("ops/runbooks/host-backend-operations.md", "CalloraHosting:PluginDrainTimeout", "00:00:30"),
        ("ops/runbooks/host-backend-operations.md", "Retention:CompletedJobRetention", "14.00:00:00"),
        ("ops/runbooks/host-backend-operations.md", "Retention:NotificationRetention", "90.00:00:00"),
    ];

    private static readonly string[] DocumentRoots = ["docs-site", "docs", "ops"];

    /// <summary>
    /// Schlüssel, die einem PLUGIN gehören und nur zufällig wie eine Host-Sektion heißen.
    /// Ein Plugin bringt seinen eigenen Konfigurationsraum mit; dass dessen Abschnitt
    /// ebenfalls <c>Retention</c> heißt, macht ihn nicht zu <c>RetentionOptions</c>.
    /// Der Host kann diese Schlüssel nicht prüfen — ihr Feld liegt in einem anderen
    /// Repository. Die Liste darf nur schrumpfen: Wandert eine Seite zum Plugin, fällt ihr
    /// Eintrag hier weg.
    /// </summary>
    private static readonly HashSet<string> PluginOwnedKeys = new(StringComparer.Ordinal)
    {
        "Retention:CallLogDays",
    };

    [Fact]
    public void EveryDocumentedConfigurationKeyExists()
    {
        var unknown = new List<string>();

        foreach (var (document, path) in EnumerateDocuments())
        {
            foreach (var (section, optionsType) in Sections)
            {
                // Sowohl `Section:Key` (appsettings) als auch `Section__Key` (Umgebung).
                var pattern = Regex.Escape(section).Replace("\\:", "(?::|__)", StringComparison.Ordinal)
                    + "(?::|__)([A-Za-z0-9_:]+)";

                foreach (Match match in Regex.Matches(document, pattern))
                {
                    var key = match.Groups[1].Value.TrimEnd('_', ':');
                    if (key.Length == 0 || PluginOwnedKeys.Contains($"{section}:{key}"))
                    {
                        continue;
                    }

                    if (!Resolves(optionsType, key))
                    {
                        unknown.Add($"  {path}: {section}:{key}");
                    }
                }
            }
        }

        Assert.True(
            unknown.Count == 0,
            "Die Dokumentation nennt Konfigurationsschlüssel, die es nicht gibt. Entweder wurde "
            + "das Feld umbenannt und die Seite nicht nachgezogen, oder der Schlüssel war nie da:"
            + Environment.NewLine + string.Join(Environment.NewLine, unknown.Distinct()));
    }

    [Fact]
    public void EveryDocumentedDefaultMatchesTheCode()
    {
        var root = ScaffoldedPluginFixture.ResolveRepositoryRoot();
        var mismatches = new List<string>();

        foreach (var (document, key, expected) in DocumentedDefaults)
        {
            var section = key[..key.LastIndexOf(':')];
            var property = key[(key.LastIndexOf(':') + 1)..];
            var optionsType = Sections.Single(entry =>
                string.Equals(entry.Section, section, StringComparison.Ordinal)).Options;

            var actual = DefaultValueOf(optionsType, property);
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                mismatches.Add($"  {key}: Code sagt {actual}, {document} sagt {expected}");
                continue;
            }

            // Ein Eintrag, dessen Dokument den Schlüssel gar nicht mehr erwähnt, ist tote
            // Bindung: Er prüft nichts und lullt den nächsten Leser ein.
            var text = File.ReadAllText(Path.Combine(root, document));
            if (!text.Contains(property, StringComparison.Ordinal))
            {
                mismatches.Add($"  {key}: {document} erwähnt {property} nicht (mehr) — Bindung streichen");
            }
        }

        Assert.True(
            mismatches.Count == 0,
            "Dokumentierte Vorgabewerte weichen vom Code ab:"
            + Environment.NewLine + string.Join(Environment.NewLine, mismatches));
    }

    private static IEnumerable<(string Text, string Path)> EnumerateDocuments()
    {
        var root = ScaffoldedPluginFixture.ResolveRepositoryRoot();

        foreach (var name in Directory.EnumerateFiles(root, "*.md", SearchOption.TopDirectoryOnly))
        {
            yield return (File.ReadAllText(name), Path.GetRelativePath(root, name));
        }

        foreach (var directory in DocumentRoots)
        {
            var full = Path.Combine(root, directory);
            if (!Directory.Exists(full))
            {
                continue;
            }

            foreach (var name in Directory.EnumerateFiles(full, "*.md", SearchOption.AllDirectories))
            {
                if (name.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                yield return (File.ReadAllText(name), Path.GetRelativePath(root, name));
            }
        }
    }

    /// <summary>
    /// Läuft einen Schlüsselpfad an den Optionen entlang. Rein numerische Segmente sind
    /// Array-Indizes (<c>ApiKeys:0</c>) und werden übersprungen, wobei in den Elementtyp
    /// abgestiegen wird — sonst gälte jeder dokumentierte Array-Eintrag als unbekannt.
    /// </summary>
    private static bool Resolves(Type optionsType, string key)
    {
        var current = optionsType;

        // Beide Schreibweisen kommen in derselben Datei vor und mischen sich sogar innerhalb
        // eines Schlüssels (`BackendHost__InitialOperator__Email` neben `BackendHost:ApiKeys:0`),
        // weil die eine aus appsettings und die andere aus docker-compose stammt.
        foreach (var segment in key.Replace("__", ":", StringComparison.Ordinal)
                     .Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(segment, CultureInfo.InvariantCulture, out _))
            {
                current = ElementTypeOf(current);
                if (current is null)
                {
                    return false;
                }

                continue;
            }

            var property = current.GetProperty(
                segment,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property is null)
            {
                return false;
            }

            current = property.PropertyType;
        }

        return true;
    }

    private static Type? ElementTypeOf(Type type)
    {
        if (type.IsArray)
        {
            return type.GetElementType();
        }

        if (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type))
        {
            return type.GetGenericArguments().FirstOrDefault();
        }

        return null;
    }

    private static string DefaultValueOf(Type optionsType, string property)
    {
        var instance = Activator.CreateInstance(optionsType)
            ?? throw new InvalidOperationException($"{optionsType.Name} ist nicht instanziierbar.");
        var value = optionsType
            .GetProperty(property, BindingFlags.Public | BindingFlags.Instance)
            ?.GetValue(instance);

        return value switch
        {
            null => "(null)",
            TimeSpan span => span.ToString(),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "(null)",
        };
    }
}

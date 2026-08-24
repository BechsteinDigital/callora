using Callora.Core.Application.Plugins;
using Callora.Core.Extensibility;
using Callora.Core.Tests.Cli;
using System.Reflection;
using System.Text;
using Xunit;

namespace Callora.Core.Tests.Documentation;

/// <summary>
/// Zwingt bei jeder Änderung an einer Erweiterungsfläche die Frage: Ist das ein Bruch?
/// </summary>
/// <remarks>
/// <para>
/// Der Anlass steht in #283. Am 2026-08-11 bekam <c>ISurfaceLayoutSource.GetDraftAsync</c> einen
/// Parameter, <c>contractVersion</c> blieb bei <c>v2</c>, und ein dagegen gebautes Plugin ließ sich
/// nicht mehr laden. Der zugehörige Fix macht diesen Bruch beim Laden sichtbar — dieses Gate soll
/// verhindern, dass er entsteht.
/// </para>
/// <para>
/// Warum die vorhandene <c>PublicAPI.Unshipped.txt</c> das nicht leistet, obwohl sie dieselbe
/// Signatur enthält: Sie darf sich ändern. Man zieht sie nach und ist fertig — es gibt keine Stelle,
/// an der jemand gefragt wird, ob die Änderung bestehende Plugins bricht. Diese Datei hier bindet
/// die Fläche an die Vertragsversion und macht aus dem Nachziehen eine Entscheidung.
/// </para>
/// <para>
/// Bewusst NICHT automatisch entschieden wird, OB eine Änderung ein Bruch ist. Eine neue Methode mit
/// Standardimplementierung ist keiner, ein zusätzlicher Parameter schon — das auseinanderzuhalten
/// erfordert Urteil, und ein Test, der es zu raten versucht, liegt irgendwann falsch und wird dann
/// umgangen. Er stellt die Frage und nennt den Unterschied; beantworten muss sie ein Mensch.
/// </para>
/// </remarks>
public sealed class TheExtensionSurfaceMatchesItsContractVersionTests
{
    private static readonly string BaselinePath =
        Path.Combine(ScaffoldedPluginFixture.ResolveRepositoryRoot(), "src", "Core", "ExtensionSurface.txt");

    [Fact]
    public void TheSurfaceIsUnchangedOrTheContractVersionMovedWithIt()
    {
        var current = DescribeExtensionSurface();

        // Der Weg zum Erneuern, den die Fehlermeldung nennt. Bewusst über eine Umgebungsvariable und
        // nicht automatisch: Eine Baseline, die sich beim Testlauf selbst nachzieht, stellt keine
        // Frage mehr — sie protokolliert nur noch, was ohnehin passiert ist.
        if (Environment.GetEnvironmentVariable("CALLORA_WRITE_EXTENSION_SURFACE") == "1")
        {
            File.WriteAllText(BaselinePath, current);
            return;
        }

        var baseline = File.ReadAllText(BaselinePath).ReplaceLineEndings("\n");

        // Der eine Fall, der kein Urteil braucht: Was als deprecated angekündigt war und jetzt
        // fehlt, IST ein Bruch — die Ankündigung hat es gesagt. Alles andere geht weiterhin an
        // einen Menschen.
        var verdict = ExtensionSurfaceComparison.Compare(baseline, current);
        Assert.False(
            verdict.RequiresContractVersionBump,
            $"Entfernt, obwohl nur angekündigt und die contractVersion steht:\n  - " +
            string.Join("\n  - ", verdict.DeprecatedRemovals) +
            "\n\nEine Ankündigung ist ein Versprechen: Das Member trägt bis zu der Vertragsversion, " +
            "die sie nennt. Vorher entfernen heißt, die contractVersion mitzuziehen.");

        Assert.True(
            string.Equals(current, baseline, StringComparison.Ordinal),
            BuildMessage(current, baseline));
    }

    /// <summary>
    /// Die Fläche ist nicht leer. Ohne diese Prüfung bestünde der Test oben auch dann, wenn die
    /// Suche nach Erweiterungspunkten nichts mehr findet — und eine leere Baseline, die zu einer
    /// leeren Fläche passt, ist die stillste Art, ein Gate abzuschalten.
    /// </summary>
    [Fact]
    public void TheSurfaceIsNotEmpty()
    {
        var lines = DescribeExtensionSurface()
            .Split('\n')
            .Where(static line => line.Length > 0 && !line.StartsWith('#'))
            .ToArray();

        Assert.True(lines.Length > 50, $"Nur {lines.Length} Erweiterungsflächen gefunden — das ist zu wenig, um zu stimmen.");
    }

    /// <summary>
    /// Beschreibt jede mit <see cref="CalloraExtensibleAttribute"/> markierte Fläche so, wie ein
    /// Plugin sie sieht: bei einem markierten Typ dessen gesamte öffentliche Oberfläche, bei einem
    /// markierten Member nur dieses.
    /// </summary>
    internal static string DescribeExtensionSurface(Assembly? assembly = null)
    {
        assembly ??= typeof(CalloraExtensibleAttribute).Assembly;
        var signatures = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var type in assembly.GetExportedTypes())
        {
            if (type.GetCustomAttribute<CalloraExtensibleAttribute>(inherit: false) is not null)
            {
                foreach (var member in PublicMembersOf(type))
                {
                    signatures.Add(Describe(member) + DeprecationSuffix(member, type));
                }

                continue;
            }

            // Ein markiertes Member auf einem unmarkierten Typ ist ebenfalls eine zugesagte Fläche.
            foreach (var member in PublicMembersOf(type)
                         .Where(static member => member.GetCustomAttribute<CalloraExtensibleAttribute>(inherit: false) is not null))
            {
                signatures.Add(Describe(member) + DeprecationSuffix(member, member.DeclaringType));
            }
        }

        var builder = new StringBuilder();
        builder.Append("# Erweiterungsflächen, gegen die Plugins bauen. Erzeugt von ")
            .Append(nameof(TheExtensionSurfaceMatchesItsContractVersionTests))
            .Append('\n');
        builder.Append("# contractVersion: ").Append(CurrentContractVersion()).Append('\n');
        foreach (var signature in signatures)
        {
            builder.Append(signature).Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>
    /// Hängt die Deprecations-Ankündigung an die Signatur — als Suffix, nicht als Präfix.
    /// </summary>
    /// <remarks>
    /// Die Datei ist sortiert. Ein Präfix hätte die Zeile beim Statuswechsel an eine andere
    /// Stelle geschoben, und aus einer Ankündigung wäre ein Diff über die halbe Datei
    /// geworden. So bleibt sie stehen, wo sie war, und der Diff zeigt genau die eine Zeile.
    /// Ein am TYP markierter Zustand gilt für alle seine Member — sonst müsste man jedes
    /// einzeln markieren, um einen Typ zu verabschieden.
    /// </remarks>
    private static string DeprecationSuffix(MemberInfo member, Type? declaringType)
    {
        var announcement = member.GetCustomAttribute<CalloraDeprecatedAttribute>(inherit: false)
            ?? declaringType?.GetCustomAttribute<CalloraDeprecatedAttribute>(inherit: false);

        return announcement is null
            ? string.Empty
            : $"  # deprecated since {announcement.Since}, error in {announcement.ErrorsIn}";
    }

    private static IEnumerable<MemberInfo> PublicMembersOf(Type type) =>
        type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(static member => member is not MethodInfo method || !method.IsSpecialName);

    private static string Describe(MemberInfo member) => member switch
    {
        MethodInfo method =>
            $"{Name(method.ReturnType)} {method.DeclaringType?.FullName}.{method.Name}({Parameters(method)})",
        PropertyInfo property =>
            $"{Name(property.PropertyType)} {property.DeclaringType?.FullName}.{property.Name} {{ {(property.CanRead ? "get; " : "")}{(property.CanWrite ? "set; " : "")}}}",
        EventInfo eventInfo =>
            $"event {Name(eventInfo.EventHandlerType)} {eventInfo.DeclaringType?.FullName}.{eventInfo.Name}",
        ConstructorInfo constructor =>
            $"{constructor.DeclaringType?.FullName}.ctor({Parameters(constructor)})",
        FieldInfo field =>
            $"{Name(field.FieldType)} {field.DeclaringType?.FullName}.{field.Name}",
        _ => $"{member.MemberType} {member.DeclaringType?.FullName}.{member.Name}",
    };

    // Der Parametername gehört dazu: Er ist Teil dessen, was ein Aufrufer benannt verwendet, und
    // eine Umbenennung bricht jeden, der es tut.
    private static string Parameters(MethodBase method) =>
        string.Join(", ", method.GetParameters().Select(static p => $"{Name(p.ParameterType)} {p.Name}"));

    private static string Name(Type? type)
    {
        if (type is null)
        {
            return "void";
        }

        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }

        var definition = type.GetGenericTypeDefinition().FullName ?? type.Name;
        var arguments = string.Join(", ", type.GetGenericArguments().Select(Name));
        return $"{definition[..definition.IndexOf('`', StringComparison.Ordinal)]}<{arguments}>";
    }

    private static string CurrentContractVersion() =>
        PluginContractVersionPolicy.GetAll()
            .Where(static policy => policy.Status == PluginContractSupportStatus.Supported)
            .Select(static policy => policy.ContractVersion)
            .OrderByDescending(static version => version, StringComparer.Ordinal)
            .First();

    private static string BuildMessage(string current, string baseline)
    {
        var currentLines = current.Split('\n');
        var baselineLines = baseline.Split('\n');
        var added = currentLines.Except(baselineLines, StringComparer.Ordinal).Where(static l => l.Length > 0).ToArray();
        var removed = baselineLines.Except(currentLines, StringComparer.Ordinal).Where(static l => l.Length > 0).ToArray();

        var builder = new StringBuilder();
        builder.AppendLine("Eine Erweiterungsfläche hat sich geändert, gegen die Plugins bauen.");
        builder.AppendLine();
        if (removed.Length > 0)
        {
            builder.AppendLine("Nicht mehr vorhanden oder anders:");
            foreach (var line in removed)
            {
                builder.Append("  - ").AppendLine(line);
            }
        }

        if (added.Length > 0)
        {
            builder.AppendLine("Neu:");
            foreach (var line in added)
            {
                builder.Append("  + ").AppendLine(line);
            }
        }

        builder.AppendLine();
        builder.AppendLine("Die Frage, die dieser Test stellt: Bricht das ein Plugin, das gegen die bisherige");
        builder.AppendLine("Fassung gebaut wurde?");
        builder.AppendLine();
        builder.AppendLine("  Ja  — etwa ein zusätzlicher Parameter, ein geänderter Typ, ein entferntes Member:");
        builder.AppendLine("        Dann gehört contractVersion in PluginContractVersionPolicy hochgezogen und die");
        builder.AppendLine("        bisherige auf Deprecated gesetzt. Erst danach diese Datei erneuern.");
        builder.AppendLine("  Nein — etwa eine neue Methode mit Standardimplementierung, ein neuer Typ:");
        builder.AppendLine("        Dann genügt es, src/Core/ExtensionSurface.txt zu erneuern.");
        builder.AppendLine();
        builder.AppendLine("Erneuern lässt sich die Datei mit:");
        builder.AppendLine("  dotnet test --filter FullyQualifiedName~TheExtensionSurfaceMatchesItsContractVersion \\");
        builder.AppendLine("    -e CALLORA_WRITE_EXTENSION_SURFACE=1");
        return builder.ToString();
    }
}

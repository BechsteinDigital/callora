using Callora.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Callora.Core.Tests.Infrastructure.Persistence;

/// <summary>
/// Jede workspace-gebundene Tabelle wird beim Purge eines Workspaces angefasst.
/// <para>
/// <b>Warum als Regel und nicht je Tabelle:</b> Der Purge war nicht falsch geschrieben, er war
/// unvollständig — SurfaceSessions, SurfaceHandoffTickets, SessionResumeTickets und
/// IntegrationCredentials kamen NACH ihm dazu, jede mit einer <c>workspace_key</c>-Spalte und
/// ohne Fremdschlüssel, der etwas kaskadiert hätte. Niemand bemerkte es, weil nichts danach fragt.
/// Ein Test je Tabelle hätte dasselbe Loch gehabt: Er wäre für die nächste Tabelle nicht
/// geschrieben worden.
/// </para>
/// <para>
/// Die Ausnahmen sind absichtlich einzeln begründet und dürfen nur schrumpfen. Wer eine hinzufügt,
/// erklärt, warum diese Zeilen einen gelöschten Workspace überleben sollen.
/// </para>
/// </summary>
public sealed class WorkspacePurgeReachesEveryWorkspaceBoundTableTests
{
    /// <summary>
    /// Tabellen mit <c>WorkspaceKey</c>, die der Purge bewusst stehen lässt.
    /// </summary>
    private static readonly Dictionary<string, string> DeliberatelyOutOfScope = new(StringComparer.Ordinal)
    {
        // Der Workspace selbst — er wird als Entität entfernt (Workspaces.Remove), nicht per
        // ExecuteDelete über seinen eigenen Schlüssel.
        ["Workspaces"] = "wird als Entität entfernt",

        // Idempotenz-Log der Marktplatz-Events. Diese Zeilen MÜSSEN einen Workspace überleben:
        // Sie sind der Grund, warum ein wiederholt zugestelltes Event nicht ein zweites Mal
        // verarbeitet wird. Gelöscht würde ein Replay nach der Löschung erneut greifen.
        ["MarketplaceEntitlementEvents"] = "Idempotenzschlüssel — ein Replay darf nicht erneut greifen",

        // Entitlements sind Lizenzentscheidungen ("darf benutzt werden"), ausdrücklich getrennt
        // von der Aktivierung ("ist eingeschaltet", PLAT-253). Ob eine bezahlte Lizenz die
        // Löschung eines Workspaces überlebt, ist eine Abrechnungsfrage und keine des Purges —
        // sie gehört entschieden, bevor hier gelöscht wird.
        ["PluginEntitlements"] = "Lizenzentscheidung, offen — nicht still mitlöschen"
    };

    [Fact]
    public void EveryWorkspaceBoundTableIsPurgedOrDeliberatelyExcluded()
    {
        var source = PurgeServiceSource();

        var missing = WorkspaceBoundSets()
            .Where(name => !DeliberatelyOutOfScope.ContainsKey(name))
            .Where(name => !source.Contains($"dbContext.{name}", StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            missing.Length == 0,
            "Diese Tabellen tragen einen WorkspaceKey, werden vom Purge aber nicht angefasst — "
            + "ihre Zeilen überleben den Workspace, und ein gleichnamiger neuer erbt sie:\n"
            + string.Join('\n', missing));
    }

    /// <summary>
    /// Kein Eintrag ohne Verstoß: Wird eine Ausnahme überflüssig — weil der Purge die Tabelle
    /// doch räumt oder es sie nicht mehr gibt —, fällt sie hier auf statt stehen zu bleiben.
    /// </summary>
    [Fact]
    public void NoStaleExclusions()
    {
        var sets = WorkspaceBoundSets().ToHashSet(StringComparer.Ordinal);

        var stale = DeliberatelyOutOfScope.Keys
            .Where(name => !sets.Contains(name))
            .ToArray();

        Assert.True(
            stale.Length == 0,
            "Diese Ausnahmen beschreiben keine workspace-gebundene Tabelle mehr:\n" + string.Join('\n', stale));
    }

    /// <summary>
    /// Workspace-gebunden heißt nicht nur „hat eine WorkspaceKey-Spalte".
    /// </summary>
    /// <remarks>
    /// Die Konfiguration bindet über <c>Scope</c>/<c>ScopeKey</c> („workspace" plus Schlüssel),
    /// und die Snippet-Overrides tun es seit ADR-024 genauso. Diese Tabellen fielen durch das
    /// Raster, obwohl sie dasselbe Problem haben — genau die Lücke, gegen die dieser Test
    /// geschrieben wurde, nur eine Spaltenbenennung weiter.
    /// </remarks>
    private static IEnumerable<string> WorkspaceBoundSets() =>
        typeof(HostPersistenceDbContext)
            .GetProperties()
            .Where(property => property.PropertyType.IsGenericType
                               && property.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Where(property => IsWorkspaceBound(property.PropertyType.GetGenericArguments()[0]))
            .Select(property => property.Name);

    private static bool IsWorkspaceBound(Type entity) =>
        entity.GetProperty("WorkspaceKey") is not null
        || (entity.GetProperty("Scope") is not null && entity.GetProperty("ScopeKey") is not null);

    private static string PurgeServiceSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Callora.Host.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(
            directory!.FullName,
            "src", "Core", "Infrastructure", "Persistence", "WorkspaceDataPurgeService.cs"));
    }
}

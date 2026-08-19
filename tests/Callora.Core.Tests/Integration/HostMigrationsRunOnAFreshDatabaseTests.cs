using Callora.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Callora.Core.Tests.Integration;

/// <summary>
/// Führt die Host-Migrationen gegen eine LEERE Postgres-Datenbank aus — den Weg, den jede
/// neue Installation geht.
/// </summary>
/// <remarks>
/// Jeder andere Integrationstest gegen das Host-Schema benutzt <c>EnsureCreatedAsync</c>.
/// Das baut das Schema aus dem Modell und überspringt die Migrationen vollständig; ein
/// Fehler in einer Migration ist für diese Tests unsichtbar.
///
/// <para>
/// Genau so blieb ein Fehler stehen, der jede Neuinstallation blockierte:
/// <c>AddWorkspaceSectionLayoutDefinitions</c> fügte eine Spalte zu einer Tabelle hinzu,
/// die keine Migration anlegt. Auf gewachsenen Entwicklungsdatenbanken existierte sie aus
/// einem früheren <c>EnsureCreated</c>-Lauf, also lief alles. Eine frische Datenbank brach
/// beim Start ab: <c>42P01, relation "workspace_section_layout_definitions" does not
/// exist</c>. Gefunden wurde das erst beim ersten Start des Produktions-Skeletts.
/// </para>
///
/// <para>
/// Dieser Test kostet einen Container und rund eine halbe Minute. Er deckt dafür etwas ab,
/// das keine schnelle Zusicherung erreichen kann: dass die Schritte in ihrer echten
/// Reihenfolge auf leerem Grund durchlaufen.
/// </para>
/// </remarks>
[Trait("Category", "Slow")]
[Collection(PostgresCollection.Name)]
public sealed class HostMigrationsRunOnAFreshDatabaseTests(PostgresFixture postgres)
{

    // Eine Datenbank je TEST, nicht je Aufruf: xUnit erzeugt die Klasse für jede
    // Testmethode neu, also ist dieses Feld pro Test frisch. Ohne das bekäme jeder
    // Kontext innerhalb eines Tests eine eigene Datenbank — was ein Test, der zwei
    // gleichzeitige Verbindungen gegeneinander laufen lässt, sofort bemerkt: Der
    // Schreiber landet in der einen, die Leser suchen in der anderen.
    private string? _database;

    private async Task<string> DatabaseAsync() =>
        _database ??= await postgres.CreateDatabaseAsync();
    [SkippableFact]
    public async Task EveryMigrationAppliesToAnEmptyDatabase()
    {
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");

        var options = new DbContextOptionsBuilder<HostPersistenceDbContext>()
            .UseNpgsql(await DatabaseAsync())
            .Options;

        await using var context = new HostPersistenceDbContext(options);

        // Kein Assert.DoesNotThrow: Schlägt eine Migration fehl, ist die Ausnahme selbst
        // die Aussage — mit Migrations-Id, SQL-Zustand und Tabellenname.
        await context.Database.MigrateAsync();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    [SkippableFact]
    public async Task TheMigratedSchemaMatchesTheModel()
    {
        Skip.IfNot(postgres.Available, "Docker/Postgres container not available.");

        var options = new DbContextOptionsBuilder<HostPersistenceDbContext>()
            .UseNpgsql(await DatabaseAsync())
            .Options;

        await using var context = new HostPersistenceDbContext(options);
        await context.Database.MigrateAsync();

        // Die zweite Hälfte derselben Frage: Die Migrationen laufen durch — aber erzeugen
        // sie auch, was das Modell behauptet? Eine Entität ohne CreateTable fällt hier auf,
        // selbst wenn keine spätere Migration sie anfasst und der Lauf deshalb grün bleibt.
        var missing = new List<string>();
        foreach (var entity in context.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            if (table is null)
            {
                continue;
            }

            var schema = entity.GetSchema() ?? "public";
            var exists = await TableExistsAsync(context, schema, table);
            if (!exists)
            {
                missing.Add($"{schema}.{table}");
            }
        }

        Assert.True(
            missing.Count == 0,
            "Das Modell erklärt Tabellen, die keine Migration anlegt — eine Neuinstallation "
            + "bricht darauf ab: " + string.Join(", ", missing.Distinct().Order()));
    }

    private static async Task<bool> TableExistsAsync(
        HostPersistenceDbContext context, string schema, string table)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables "
            + "WHERE table_schema = @schema AND table_name = @table)";

        var schemaParameter = command.CreateParameter();
        schemaParameter.ParameterName = "schema";
        schemaParameter.Value = schema;
        command.Parameters.Add(schemaParameter);

        var tableParameter = command.CreateParameter();
        tableParameter.ParameterName = "table";
        tableParameter.Value = table;
        command.Parameters.Add(tableParameter);

        return await command.ExecuteScalarAsync() is true;
    }
}

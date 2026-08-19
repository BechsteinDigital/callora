using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Callora.Core.Tests.Integration;

/// <summary>
/// Ein Postgres für alle Integrationstests.
/// </summary>
/// <remarks>
/// <para>
/// Vorher baute jede Testklasse ihren eigenen <c>PostgreSqlContainer</c> über
/// <see cref="IAsyncLifetime"/> — elf Klassen, elf Container-Starts pro Lauf, jeder mit
/// Image-Prüfung, Start und Health-Wait. Das ist die teuerste Sekunde im Testlauf und sie
/// wurde elfmal bezahlt, für exakt dieselbe Sache.
/// </para>
/// <para>
/// Isolation kommt jetzt aus einer eigenen DATENBANK je Aufrufer statt aus einem eigenen
/// Container. <c>CREATE DATABASE</c> kostet Millisekunden, ein Containerstart Sekunden, und
/// die Trennung ist dieselbe: Zwei Tests sehen die Tabellen des anderen nicht.
/// </para>
/// <para>
/// Ohne Docker bleibt <see cref="Available"/> false und die Tests überspringen sich per
/// <c>Skip.IfNot</c> — wie zuvor. Ein Entwickler ohne Docker soll den Rest der Suite
/// fahren können, statt eine rote Ausgabe zu sehen, an der er nichts ändern kann.
/// </para>
/// </remarks>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:16-alpine").Build();

    /// <summary>Ob der Container läuft. False heißt: kein Docker, Tests überspringen.</summary>
    public bool Available { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            await _postgres.StartAsync();
            Available = true;
        }
        catch (Exception)
        {
            Available = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (Available)
        {
            await _postgres.DisposeAsync();
        }
    }

    /// <summary>
    /// Legt eine leere Datenbank an und gibt ihren Verbindungsstring zurück.
    /// </summary>
    /// <remarks>
    /// Der Name trägt ein GUID-Suffix, weil zwei Klassen derselben Collection zwar
    /// nacheinander laufen, ein Test aber mehrfach eine frische Datenbank anfordern darf —
    /// etwa um einen Migrationslauf auf wirklich leerem Stand zu prüfen.
    /// </remarks>
    public async Task<string> CreateDatabaseAsync()
    {
        var name = $"test_{Guid.NewGuid():N}";

        await using (var admin = new NpgsqlConnection(_postgres.GetConnectionString()))
        {
            await admin.OpenAsync();
            await using var create = admin.CreateCommand();
            // Der Name ist ein selbst erzeugtes GUID, kein Eingabewert — und ein
            // Datenbankname lässt sich ohnehin nicht parametrisieren.
            create.CommandText = $"CREATE DATABASE \"{name}\"";
            await create.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        {
            Database = name,
        }.ConnectionString;
    }
}

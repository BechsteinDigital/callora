using Xunit;

namespace Callora.Core.Tests.Integration;

/// <summary>
/// Bindet alle Tests, die ein echtes Postgres brauchen, an dasselbe
/// <see cref="PostgresFixture"/> — und damit an denselben Container.
/// </summary>
/// <remarks>
/// Eine Collection läuft nicht parallel zu einer anderen, und Klassen innerhalb einer
/// Collection laufen nacheinander. Das ist hier kein Verlust: Die Wartezeit lag im
/// Containerstart, nicht in den Abfragen, und der fällt jetzt einmal an statt elfmal.
/// </remarks>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}

using Callora.Core.Application.Policies;
using Callora.Core.Infrastructure.Persistence;
using Xunit;

namespace Callora.Core.Tests.Infrastructure.Persistence;

/// <summary>
/// Der Advisory-Lock-Schlüssel für Plugin-Migrationen muss über Prozessgrenzen hinweg
/// derselbe sein — sonst schützt der Lock nichts.
/// </summary>
/// <remarks>
/// <para>
/// Der Schlüssel entstand aus <c>StringComparer.Ordinal.GetHashCode(pluginId)</c>. Der
/// String-Hash ist in .NET seit Core je PROZESS randomisiert: Zwei Host-Instanzen berechneten
/// für dieselbe Plugin-Id verschiedene Schlüssel, nahmen verschiedene Advisory Locks und
/// migrierten dasselbe Plugin-Schema gleichzeitig. Nachgemessen mit zwei fsi-Prozessen für
/// <c>"comm"</c>: -1859540138 gegen -1761266879.
/// </para>
/// <para>
/// Ein Lock, der bei jedem Prozessstart woanders hinzeigt, ist schlimmer als keiner: Er
/// erzeugt die Zusage von Serialisierung, ohne sie zu halten, und der Schaden zeigt sich erst
/// beim gleichzeitigen Start zweier Instanzen — also genau dann, wenn niemand zusieht.
/// </para>
/// <para>
/// Die erwarteten Werte stehen absichtlich als Literale hier. Berechnete der Test sie selbst,
/// prüfte er die Implementierung gegen sich selbst; mit dem alten, randomisierten Hash wäre er
/// in nahezu jedem Lauf rot — was er auch war.
/// </para>
/// </remarks>
public sealed class MigrationLockKeyIsDeterministicTests
{
    [Theory]
    [InlineData("comm", 5786093385840558915L)]
    [InlineData("communication", 5786093388266826132L)]
    public void TheKeyIsTheSameInEveryProcess(string pluginId, long expected)
    {
        Assert.Equal(expected, Provider().GetMigrationLockKey(pluginId));
    }

    [Theory]
    [InlineData("Comm")]
    [InlineData("COMM")]
    [InlineData("  comm  ")]
    public void CaseAndPaddingDoNotChangeTheKey(string pluginId)
    {
        // Plugin-Ids werden überall ohne Rücksicht auf Groß-/Kleinschreibung verglichen
        // (RuntimePluginHost führt seine Wörterbücher mit OrdinalIgnoreCase). Ein Lock, der
        // "Comm" und "comm" trennte, ließe zwei Instanzen desselben Plugins nebeneinander
        // migrieren — dieselbe Wirkung wie gar kein Lock.
        Assert.Equal(Provider().GetMigrationLockKey("comm"), Provider().GetMigrationLockKey(pluginId));
    }

    [Fact]
    public void DifferentPluginsGetDifferentKeys()
    {
        var provider = Provider();

        Assert.NotEqual(provider.GetMigrationLockKey("comm"), provider.GetMigrationLockKey("composer"));
    }

    private static NpgsqlPluginDbContextProvider Provider() =>
        new(new BackendHostOptions { DatabaseConnectionString = "Host=localhost;Database=x" });
}

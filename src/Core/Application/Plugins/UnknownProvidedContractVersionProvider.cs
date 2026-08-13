using Callora.Core.Extensibility;
using Semver;

namespace Callora.Core.Application.Plugins;

/// <summary>
/// Ein Versionsanbieter, der nichts kennt — für Zusammenstellungen ohne echten Anbieter.
/// <para>
/// Er existiert, damit <see cref="PluginLifecycleService"/> kein Infrastructure-Objekt mehr selbst
/// erzeugen muss. Die Application-Schicht darf laut CODE_STRUCTURE_RULES nur die Domäne kennen;
/// der frühere Rückfall baute an dieser Stelle einen <c>LoadedContractVersionProvider</c> aus
/// Infrastructure — sichtbar nur daran, dass der Typ voll qualifiziert dastand.
/// </para>
/// <para>
/// Sicherheitlich ist das kein Nachlassen: Ein nicht aufgelöster Contract wird vom Gate
/// übersprungen, weil seine Anwesenheit Sache des Aktivierungsplaners ist
/// (<see cref="PluginDependencyVersionGate"/>). Und der Rückfall ist im Host unerreichbar — die
/// Composition-Root registriert immer ein echtes Gate, dessen Anbieter zusätzlich die
/// Shared-Contract-Registry liest. Er greift nur, wenn jemand den Dienst von Hand baut.
/// </para>
/// </summary>
[CalloraInternal("Fallback for the install gate — not a plugin contract")]
public sealed class UnknownProvidedContractVersionProvider : IProvidedContractVersionProvider
{
    /// <summary>Die eine Instanz; sie hält keinen Zustand.</summary>
    public static UnknownProvidedContractVersionProvider Instance { get; } = new();

    private UnknownProvidedContractVersionProvider()
    {
    }

    /// <inheritdoc />
    public SemVersion? Resolve(string contractId) => null;
}

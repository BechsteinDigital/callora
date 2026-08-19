namespace Callora.Host.Cli.Application;

/// <summary>Ein Befund aus <c>callora plugin test-contract</c>.</summary>
/// <remarks>
/// <para>
/// <paramref name="IsWarning"/> trennt „das lässt sich nicht installieren" von „das lässt sich
/// installieren, sollte aber nicht so bleiben". Die Unterscheidung stammt nicht von hier: Der
/// Installer der Laufzeit kennt sie bereits (PLUGIN_CONTRACT_VERSION_DEPRECATED ist ein
/// Warncode, kein Fehlercode). Ohne sie hätte das Prüfwerkzeug nur die Wahl zwischen härter
/// urteilen als der Host — ein Plugin zurückweisen, das sich installieren lässt — und den
/// Hinweis ganz zu verschlucken.
/// </para>
/// </remarks>
internal sealed record PluginContractTestIssue(
    string Code,
    string Message,
    string Remediation,
    bool IsWarning = false);

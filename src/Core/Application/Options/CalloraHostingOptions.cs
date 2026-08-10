namespace Callora.Core.Application.Options;

/// <summary>
/// Options for plugin hosting in host applications.
/// </summary>
public sealed class CalloraHostingOptions
{
    /// <summary>
    /// Enables automatic plugin discovery/load from <see cref="PluginDirectory"/>.
    /// </summary>
    public bool AutoLoadPlugins { get; set; }

    /// <summary>
    /// Directory scanned for Application-tier runtime plugins when
    /// <see cref="AutoLoadPlugins"/> is enabled.
    /// </summary>
    public string PluginDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "custom", "plugins");

    /// <summary>
    /// Directory scanned for bundled System/Foundation-tier plugins. Scanned
    /// before <see cref="PluginDirectory"/>, so foundation plugins load first.
    /// </summary>
    public string StaticPluginDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "custom", "static-plugins");

    /// <summary>
    /// Automatically activates installed plugins marked as active in runtime state.
    /// </summary>
    public bool AutoActivateInstalledPlugins { get; set; } = true;

    /// <summary>
    /// Grace period the runtime-capability registry waits before a health-derived capability loss
    /// takes effect, damping transient flaps (a channel that briefly reconnects should not deactivate
    /// dependents). Return to satisfied is always immediate. <see cref="TimeSpan.Zero"/> flips a loss
    /// immediately (no damping).
    /// </summary>
    public TimeSpan RuntimeCapabilityGracePeriod { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Zahl zugerechneter Fehler innerhalb von <see cref="PluginFaultWindow"/>, ab der ein Plugin
    /// als nicht mehr verfügbar gilt (<see cref="Plugins.PluginFaultRegistry"/>). 0 schaltet das
    /// Budget ab: Fehler werden dann nur gezählt, nie geahndet.
    /// </summary>
    /// <remarks>
    /// Der Vorgabewert ist bewusst nicht scharf. Ein Plugin, das gelegentlich wirft — eine
    /// Gegenstelle antwortet nicht, ein Aufrufer schickt Unsinn —, soll nicht ausfallen; erst ein
    /// Plugin, das reihenweise wirft, kostet die anderen im selben Prozess etwas. Wer schneller
    /// abriegeln will, setzt den Wert herunter, statt das Fenster zu verkürzen: Ein kurzes Fenster
    /// macht das Budget vergesslich, eine niedrige Schwelle macht es empfindlich.
    /// </remarks>
    public int PluginFaultThreshold { get; set; } = 10;

    /// <summary>
    /// Gleitendes Fenster, über das <see cref="PluginFaultThreshold"/> zählt. Läuft es ohne neue
    /// Fehler leer, ist das Plugin ohne Eingriff wieder verfügbar.
    /// </summary>
    public TimeSpan PluginFaultWindow { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How long the host waits for a plugin implementing
    /// <see cref="Domain.Plugins.Contracts.IDrainablePlugin"/> to run its outstanding work dry before
    /// stopping it anyway (ADR-018 §2.1). <see cref="TimeSpan.Zero"/> skips draining entirely.
    /// </summary>
    /// <remarks>
    /// The default matches ASP.NET Core's shutdown timeout, because on process shutdown that timeout
    /// bounds the wait regardless of what is configured here. Raising this value without raising
    /// <c>HostOptions.ShutdownTimeout</c> only helps a deactivation through the operator API, not a
    /// restart.
    /// </remarks>
    public TimeSpan PluginDrainTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Longest a session resume promise may hold (ADR-018 §2.2). A plugin asking for more is clamped
    /// to this.
    /// </summary>
    /// <remarks>
    /// This is the line between a reconnect window and a bearer credential. Fifteen minutes covers a
    /// tunnel, a WiFi handover and a host restart; a window measured in hours mostly covers a stolen
    /// token.
    /// </remarks>
    public TimeSpan SessionResumeMaxLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Largest resume payload a plugin may store, in UTF-8 bytes. Issuing a larger one is refused
    /// rather than truncated.
    /// </summary>
    /// <remarks>
    /// A resume payload carries identity (which session, which participant, which role), not session
    /// state. The limit is what keeps the ticket table from becoming a document store.
    /// </remarks>
    public int SessionResumeMaxPayloadBytes { get; set; } = 4096;
}

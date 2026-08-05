using Callora.Core.Extensibility;

namespace Callora.Core.Domain.Plugins.Contracts;

/// <summary>
/// Optional companion to <see cref="IHostManagedPlugin"/> for plugins that carry long-running work
/// a stop would cut through — live calls, conference sessions, open media sockets.
/// </summary>
/// <remarks>
/// <para>
/// Implementing this contract buys one thing: the host asks the plugin to run dry before it stops it.
/// A plugin without it is stopped exactly as before, so this is additive for everything that has no
/// work worth waiting for.
/// </para>
/// <para>
/// The host owns the deadline (<see cref="Options.CalloraHostingOptions.PluginDrainTimeout"/>), the
/// plugin owns the meaning. That split is deliberate: only the plugin can tell which work is "new"
/// and must be refused, and only the host can decide how long an operator's deactivation may take.
/// </para>
/// <para>
/// Draining runs <i>before</i> exports are withdrawn and before <see cref="IHostManagedPlugin.StopAsync"/>,
/// because work that is still running may still need them.
/// </para>
/// </remarks>
[CalloraExtensible("Plugin lifecycle — implement alongside IHostManagedPlugin to run outstanding work dry before stop (ADR-018 §2.1)")]
public interface IDrainablePlugin
{
    /// <summary>
    /// Stops accepting new work and returns once the outstanding work has finished.
    /// </summary>
    /// <param name="cancellationToken">
    /// Carries the host's drain deadline. Cancellation means the deadline expired, not that draining
    /// was pointless: the plugin should return promptly and leave the rest to
    /// <see cref="IHostManagedPlugin.StopAsync"/>. The host stops the plugin either way — a drain may
    /// delay a deactivation, never prevent it.
    /// </param>
    ValueTask DrainAsync(CancellationToken cancellationToken = default);
}

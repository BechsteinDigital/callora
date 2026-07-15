namespace Callora.Core.Application.Plugins;

/// <summary>
/// Verifies that a collectible plugin <see cref="System.Runtime.Loader.AssemblyLoadContext"/>
/// was actually released after <c>Unload()</c>. Calling <c>Unload()</c> only signals
/// intent: if anything still roots a type from the context (a static event
/// subscription, an in-flight request, an undisposed handle) the context stays
/// alive and its assemblies remain pinned. This runs a bounded collection loop
/// and reports whether the context was released, so a stuck plugin surfaces as a
/// failure instead of appearing cleanly inactive.
/// </summary>
internal static class AssemblyLoadContextUnload
{
    /// <summary>Default number of collection passes before giving up.</summary>
    internal const int DefaultMaxAttempts = 10;

    /// <summary>
    /// Runs up to <paramref name="maxAttempts"/> collection passes and returns
    /// true once the referenced load context has been collected, or false if it
    /// is still alive (pinned) after the final attempt.
    /// </summary>
    internal static bool WaitForCollection(WeakReference contextReference, int maxAttempts = DefaultMaxAttempts)
    {
        ArgumentNullException.ThrowIfNull(contextReference);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);

        for (var attempt = 0; attempt < maxAttempts && contextReference.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        return !contextReference.IsAlive;
    }
}

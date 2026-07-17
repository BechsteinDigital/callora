namespace Callora.Core.Application.Lifecycle;

/// <summary>
/// The closed vocabulary of plugin lifecycle action codes. These strings form a
/// contract between the producer (<see cref="PluginLifecycleReporter"/>, which
/// stamps them onto audit entries and <c>PluginLifecycleChangedEvent</c>s) and
/// the consumers that branch on them (the lifecycle event subscribers). Sharing
/// one constant per action makes that contract compiler-checked instead of a
/// silent string match.
/// </summary>
public static class PluginLifecycleActions
{
    /// <summary>A plugin was installed.</summary>
    public const string Install = "plugin.install";

    /// <summary>A plugin was updated to a new version.</summary>
    public const string Update = "plugin.update";

    /// <summary>A plugin was activated.</summary>
    public const string Activate = "plugin.activate";

    /// <summary>A plugin was deactivated.</summary>
    public const string Deactivate = "plugin.deactivate";

    /// <summary>A plugin was uninstalled.</summary>
    public const string Uninstall = "plugin.uninstall";

    /// <summary>A failed update was rolled back to the previous version.</summary>
    public const string Rollback = "plugin.rollback";
}

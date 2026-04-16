namespace Callora.Host.Backend.Domain.Plugins;

/// <summary>
/// Aggregate root representing one installed extension/plugin in the host.
/// </summary>
public sealed class PluginInstallation
{
    private PluginInstallation()
    {
    }

    public Guid Id { get; private set; }

    public string PluginId { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string AssemblyPath { get; private set; } = string.Empty;

    public string? EntryTypeName { get; private set; }

    public PluginInstallationState State { get; private set; }

    public DateTimeOffset InstalledAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static PluginInstallation CreateInstalled(
        string pluginId,
        string displayName,
        string assemblyPath,
        string? entryTypeName,
        DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        return new PluginInstallation
        {
            Id = Guid.NewGuid(),
            PluginId = pluginId,
            DisplayName = displayName,
            AssemblyPath = assemblyPath,
            EntryTypeName = entryTypeName,
            State = PluginInstallationState.Installed,
            InstalledAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    public void ApplyInstallMetadata(
        string displayName,
        string assemblyPath,
        string? entryTypeName,
        DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);

        DisplayName = displayName;
        AssemblyPath = assemblyPath;
        EntryTypeName = entryTypeName;
        State = PluginInstallationState.Installed;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkActivated(DateTimeOffset nowUtc)
    {
        EnsureNotUninstalled();
        State = PluginInstallationState.Active;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkDeactivated(DateTimeOffset nowUtc)
    {
        EnsureNotUninstalled();
        State = PluginInstallationState.Inactive;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkUninstalled(DateTimeOffset nowUtc)
    {
        State = PluginInstallationState.Uninstalled;
        UpdatedAtUtc = nowUtc;
    }

    private void EnsureNotUninstalled()
    {
        if (State == PluginInstallationState.Uninstalled)
            throw new InvalidOperationException($"Plugin '{PluginId}' is already uninstalled.");
    }
}

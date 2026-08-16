namespace Callora.Core.Domain.Plugins;

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

    /// <summary>
    /// Der Pfad, wie er in der Datenbank steht — relativ zu einer Plugin-Wurzel, wenn er unter
    /// einer liegt, sonst absolut. Siehe <c>IPluginAssemblyPathPortability</c>.
    /// </summary>
    public string StoredAssemblyPath { get; private set; } = string.Empty;

    /// <summary>
    /// Der Pfad im Dateisystem dieses Prozesses.
    /// </summary>
    /// <remarks>
    /// Aufgelöst wird an der Grenze, an der die Zeile gelesen wird (dem Repository); ohne
    /// Auflösung gilt der gespeicherte Wert. Der Rückfall ist Absicht: Bestand aus der Zeit vor
    /// #307 steht dort absolut und funktioniert damit unverändert weiter.
    /// </remarks>
    public string AssemblyPath => _resolvedAssemblyPath ?? StoredAssemblyPath;

    private string? _resolvedAssemblyPath;

    public string? EntryTypeName { get; private set; }

    public PluginInstallationState State { get; private set; }

    public DateTimeOffset InstalledAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Capability codes this plugin provides, encoded via <see cref="CapabilityListCodec"/>.</summary>
    public string? ProvidedCapabilities { get; private set; }

    /// <summary>Capability codes this plugin requires, encoded via <see cref="CapabilityListCodec"/>.</summary>
    public string? RequiredCapabilities { get; private set; }

    /// <summary>
    /// Capability codes this plugin provides only while a runtime condition holds (health-derived,
    /// see the runtime-capability mechanism), encoded via <see cref="CapabilityListCodec"/>. Unlike
    /// <see cref="ProvidedCapabilities"/> these are not unconditionally provided.
    /// </summary>
    public string? ConditionalCapabilities { get; private set; }

    public static PluginInstallation CreateInstalled(
        string pluginId,
        string displayName,
        string storedAssemblyPath,
        string? entryTypeName,
        DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(storedAssemblyPath);

        return new PluginInstallation
        {
            Id = Guid.NewGuid(),
            PluginId = pluginId,
            DisplayName = displayName,
            StoredAssemblyPath = storedAssemblyPath,
            EntryTypeName = entryTypeName,
            State = PluginInstallationState.Installed,
            InstalledAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    public void ApplyInstallMetadata(
        string displayName,
        string storedAssemblyPath,
        string? entryTypeName,
        DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(storedAssemblyPath);

        DisplayName = displayName;
        StoredAssemblyPath = storedAssemblyPath;
        _resolvedAssemblyPath = null;
        EntryTypeName = entryTypeName;
        State = PluginInstallationState.Installed;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Hinterlegt den Dateipfad, unter dem die Assembly in diesem Prozess liegt. Ändert den
    /// gespeicherten Wert nicht — die Zeile bleibt damit unverändert, auch wenn sie getrackt ist.
    /// </summary>
    public void ResolveAssemblyPath(string fileSystemPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileSystemPath);

        _resolvedAssemblyPath = fileSystemPath;
    }

    public void SetCapabilities(
        IReadOnlyList<string>? providedCapabilities,
        IReadOnlyList<string>? requiredCapabilities,
        IReadOnlyList<string>? conditionalCapabilities,
        DateTimeOffset nowUtc)
    {
        ProvidedCapabilities = CapabilityListCodec.Join(providedCapabilities);
        RequiredCapabilities = CapabilityListCodec.Join(requiredCapabilities);
        ConditionalCapabilities = CapabilityListCodec.Join(conditionalCapabilities);
        UpdatedAtUtc = nowUtc;
    }

    public IReadOnlyList<string> GetProvidedCapabilities() => CapabilityListCodec.Split(ProvidedCapabilities);

    public IReadOnlyList<string> GetRequiredCapabilities() => CapabilityListCodec.Split(RequiredCapabilities);

    public IReadOnlyList<string> GetConditionalCapabilities() => CapabilityListCodec.Split(ConditionalCapabilities);

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
        {
            throw PluginInstallationException.AlreadyUninstalled(PluginId);
        }
    }
}

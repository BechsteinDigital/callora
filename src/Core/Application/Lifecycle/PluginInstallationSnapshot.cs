namespace Callora.Core.Application.Lifecycle;

public sealed record PluginInstallationSnapshot(
    string PluginId,
    string DisplayName,
    string AssemblyPath,
    string? EntryTypeName,
    int State,
    DateTimeOffset InstalledAtUtc,
    DateTimeOffset UpdatedAtUtc);

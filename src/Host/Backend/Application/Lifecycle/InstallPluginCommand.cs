namespace Callora.Host.Backend.Application.Lifecycle;

public sealed record InstallPluginCommand(
    string AssemblyPath,
    string? EntryTypeName = null,
    string? RequestedBy = null);
